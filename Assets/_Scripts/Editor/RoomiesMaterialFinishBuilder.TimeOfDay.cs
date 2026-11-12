#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// 6 朝と夜の明かり：GameRoom に「Roomies_TimeOfDay」を置く（RoomTimeOfDayLighting・朝の窓の光・朝の画面の仕上げ）。
/// 夜の明かりの強さは今の値（5 Light Balance の表）をそのまま使い、朝の強さは下の表の倍率で決める。
/// 朝の画面の仕上げは Assets/Settings/RoomiesRestyleVolumeMorning.asset（値は下の MorningProfile）。
/// </summary>
internal static partial class RoomiesMaterialFinishBuilder
{
    const string TimeRootName = "Roomies_TimeOfDay";
    const string MorningProfilePath = "Assets/Settings/RoomiesRestyleVolumeMorning.asset";

    // 朝の明かり：名前（前方一致）→ 夜の強さに掛ける倍率（0 で消す）
    static readonly (string name, float morningScale)[] MorningScale =
    {
        ("Pendant light", 0), ("Lantern light", 0), ("Kitchen light", 0), ("Entry fill", 0), ("Fairy glow", .5f), ("Bounce - ", 0),
        ("Street accent", 0),   // 町の街灯（朝は消す）
    };
    // 朝の窓の光：位置, 強さ（夜は消える）
    static readonly (string name, Vector3 pos, float intensity)[] MorningWindows =
    {
        ("Morning window light - west", new Vector3(14.0f, FloorY + 1.8f, 18.2f), 3.6f),
        ("Morning window light - north", new Vector3(17.6f, FloorY + 1.8f, 22.7f), 3.2f),
        ("Morning window light - east", new Vector3(21.8f, FloorY + 1.8f, 21.0f), 3.0f),
    };
    // 夜の町の補助の光（月明かり色。店の正面が黒くつぶれないように。朝は消える）：名前, 位置, 強さ, 届く距離
    static readonly (string name, Vector3 pos, float intensity, float range)[] NightTownFill =
    {
        ("Night town fill - store front", new Vector3(26.0f, 4.0f, -2.5f), 1.6f, 16f),
        ("Night town fill - street west", new Vector3(4.0f, 4.0f, -2.5f), 1.3f, 16f),
        ("Night town fill - street east", new Vector3(46.0f, 4.0f, -2.5f), 1.3f, 16f),
    };
    static readonly Color NightTownFillColor = new Color(.62f, .7f, 1f);
    static readonly Color MorningWindowColor = new Color(.96f, .97f, 1f);

    [MenuItem("Roomies/Art/Restyle/Material Finish/6 Time Of Day Lighting")]
    internal static void BuildTimeOfDay()
    {
        var morningProfile = MorningProfile();
        foreach (var scene in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray())
        {
            if (!scene.isLoaded || scene.GetRootGameObjects().All(g => g.GetComponentInChildren<DayManager>(true) == null)) continue;
            var roots = scene.GetRootGameObjects();
            var room = roots.FirstOrDefault(g => g.name == "Roomies_RoomRestyle");
            var nightVolume = roots.Select(g => g.GetComponent<Volume>()).FirstOrDefault(v => v != null && v.sharedProfile != null && v.sharedProfile.name == "RoomiesRestyleVolume");
            if (room == null || nightVolume == null) { Debug.LogWarning("[Time Of Day] room or night volume not found in " + scene.name); continue; }

            var root = roots.FirstOrDefault(g => g.name == TimeRootName);
            if (root == null) { root = new GameObject(TimeRootName); SceneManager.MoveGameObjectToScene(root, scene); }
            for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

            var levels = new List<RoomTimeOfDayLighting.LampLevel>();
            var bounceRoot = roots.FirstOrDefault(g => g.name == BounceRootName);
            var townRoot = roots.FirstOrDefault(g => g.name == "Roomies_EnvironmentArt");
            var lights = room.GetComponentsInChildren<Light>(true)
                .Concat(bounceRoot != null ? bounceRoot.GetComponentsInChildren<Light>(true) : new Light[0])
                .Concat(townRoot != null ? townRoot.GetComponentsInChildren<Light>(true).Where(l => !InMining(l.transform)) : new Light[0]);
            foreach (var light in lights)
            {
                var row = MorningScale.FirstOrDefault(m => light.name.StartsWith(m.name));
                if (row.name == null) continue;
                levels.Add(new RoomTimeOfDayLighting.LampLevel { light = light, night = light.intensity, morning = light.intensity * row.morningScale });
            }
            foreach (var w in MorningWindows)
            {
                var go = new GameObject(w.name); go.transform.SetParent(root.transform, false); go.transform.position = w.pos;
                var light = go.AddComponent<Light>();
                light.type = LightType.Point; light.color = MorningWindowColor; light.range = 6.5f; light.intensity = 0; light.shadows = LightShadows.None;
                light.enabled = false;
                levels.Add(new RoomTimeOfDayLighting.LampLevel { light = light, night = 0, morning = w.intensity });
            }

            foreach (var f in NightTownFill)
            {
                var go = new GameObject(f.name); go.transform.SetParent(root.transform, false); go.transform.position = f.pos;
                var light = go.AddComponent<Light>();
                light.type = LightType.Point; light.color = NightTownFillColor; light.range = f.range; light.intensity = f.intensity; light.shadows = LightShadows.None;
                levels.Add(new RoomTimeOfDayLighting.LampLevel { light = light, night = f.intensity, morning = 0 });
            }

            var volGo = new GameObject("Roomies_PostProcess_Morning"); volGo.transform.SetParent(root.transform, false);
            var morningVolume = volGo.AddComponent<Volume>();
            morningVolume.isGlobal = true; morningVolume.priority = 2; morningVolume.weight = 0; morningVolume.sharedProfile = morningProfile;

            var tod = root.GetComponent<RoomTimeOfDayLighting>() ?? root.AddComponent<RoomTimeOfDayLighting>();
            var so = new SerializedObject(tod);
            var arr = so.FindProperty("lamps"); arr.arraySize = levels.Count;
            for (int i = 0; i < levels.Count; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("light").objectReferenceValue = levels[i].light;
                e.FindPropertyRelative("night").floatValue = levels[i].night;
                e.FindPropertyRelative("morning").floatValue = levels[i].morning;
            }
            so.FindProperty("nightVolume").objectReferenceValue = nightVolume;
            so.FindProperty("morningVolume").objectReferenceValue = morningVolume;
            so.ApplyModifiedPropertiesWithoutUndo();
            nightVolume.weight = 1;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Time Of Day] {scene.name}: {levels.Count} lights");
        }
    }

    /// <summary>朝の画面の仕上げ（夜より青白く、コントラストと周辺減光は控えめ）。</summary>
    static VolumeProfile MorningProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(MorningProfilePath);
        if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, MorningProfilePath); }
        T Get<T>() where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var c)) return c;
            c = profile.Add<T>(true); c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(c, profile); return c;
        }
        Get<Tonemapping>().mode.Override(TonemappingMode.ACES);
        var color = Get<ColorAdjustments>(); color.postExposure.Override(.8f); color.contrast.Override(6); color.saturation.Override(12); color.colorFilter.Override(Color.white);
        var white = Get<WhiteBalance>(); white.temperature.Override(-3); white.tint.Override(0);
        var split = Get<SplitToning>(); split.shadows.Override(new Color(.47f, .49f, .55f)); split.highlights.Override(new Color(.55f, .54f, .52f)); split.balance.Override(0);
        var smh = Get<ShadowsMidtonesHighlights>(); smh.shadows.Override(new Vector4(1, 1, 1.02f, 0)); smh.midtones.Override(new Vector4(1, 1, 1, 0)); smh.highlights.Override(new Vector4(1, 1, 1, -.05f));
        var bloom = Get<Bloom>(); bloom.threshold.Override(1.1f); bloom.intensity.Override(.3f); bloom.scatter.Override(.65f); bloom.tint.Override(Color.white);
        var vignette = Get<Vignette>(); vignette.intensity.Override(.12f); vignette.smoothness.Override(.4f); vignette.color.Override(new Color(.04f, .04f, .06f));
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }
}
#endif
