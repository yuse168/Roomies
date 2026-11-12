#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 5 明暗のメリハリ：主の光（吊りランプ）を少し上げ、補助の光（ちょうちん・入口・電飾・照り返し）と夜の環境光を下げる。
/// 明かりの強さは下の表で決める（名前の前方一致）。環境光は DayManager / MainMenuRoomShowcase のインスペクターの値を書き換える。
/// Build Room で明かりを作り直したあとも RoomiesRestyleKit.End() からかけ直す。
/// </summary>
internal static partial class RoomiesMaterialFinishBuilder
{
    // 明かりの名前（前方一致）, 強さ
    static readonly (string name, float intensity)[] LightLevels =
    {
        ("Pendant light", 18f),          // 主の光
        ("Lantern light", 4.5f),
        ("Kitchen light", 4f),
        ("Entry fill", .8f),
        ("Fairy glow", 1.5f),
        ("Title key light", 3.5f),       // タイトル：顔の明かり（強すぎると青いキャラが白く飛ぶ）
        // 照り返し（天井と壁が明るくなりすぎないよう、2026/11/13 の段階4で約0.6倍に）
        ("Bounce - rug under pendant", 1.8f),
        ("Bounce - sofa", .6f),
        ("Bounce - kitchen counter", .6f),
        ("Bounce - lantern floor", .7f),
        ("Bounce - fairy floor", .4f),
    };
    // 明かりの色（オレンジに寄りすぎないよう、補助の光を少し白へ。吊りランプと電飾は暖色のまま）
    static readonly (string name, Color color)[] LightColors =
    {
        ("Lantern light", new Color(1f, .86f, .7f)), ("Kitchen light", new Color(1f, .87f, .7f)), ("Entry fill", new Color(1f, .9f, .78f)),
        ("Bounce - rug under pendant", new Color(1f, .8f, .68f)), ("Bounce - kitchen counter", new Color(1f, .86f, .72f)),
    };
    const float NightAmbient = .3f, NightReflection = .35f;
    // 夜の月明かり（町が真っ暗にならないよう。部屋の中は窓から少し入るだけ）
    const float NightSun = .25f;
    // 町の街灯（町のプレハブ RoomiesNeighborhood の中の明かり）：名前（前方一致）, 強さ, 届く距離, 色
    static readonly (string name, float intensity, float range, Color color)[] TownLights =
    {
        ("Street accent", 4f, 10f, new Color(1f, .82f, .6f)),
    };

    [MenuItem("Roomies/Art/Restyle/Material Finish/5 Light Balance")]
    internal static void ApplyLightBalance()
    {
        foreach (var scene in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray())
        {
            if (!scene.isLoaded) continue;
            var roots = scene.GetRootGameObjects();
            if (!roots.Any(g => g.name == "Roomies_RoomRestyle")) continue;
            int n = 0;
            foreach (var root in roots.Where(g => g.name == "Roomies_RoomRestyle" || g.name == BounceRootName))
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                {
                    var row = LightLevels.FirstOrDefault(l => light.name.StartsWith(l.name));
                    if (row.name == null) continue;
                    light.intensity = row.intensity;
                    var tint = LightColors.FirstOrDefault(l => light.name.StartsWith(l.name));
                    if (tint.name != null) light.color = tint.color;
                    EditorUtility.SetDirty(light); n++;
                }
            foreach (var day in roots.SelectMany(g => g.GetComponentsInChildren<DayManager>(true)))
                SetFloats(day, ("nightAmbientIntensity", NightAmbient), ("nightReflectionIntensity", NightReflection), ("nightSunIntensity", NightSun));
            foreach (var menu in roots.SelectMany(g => g.GetComponentsInChildren<MainMenuRoomShowcase>(true)))
                SetFloats(menu, ("ambientIntensity", NightAmbient), ("reflectionIntensity", NightReflection));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Material Finish] light balance: {n} lights in {scene.name}");
        }
        ApplyTownLights();
        // 朝と夜の切り替えの「夜の強さ」を今の値にそろえる（GameRoom だけ）
        BuildTimeOfDay();
    }

    /// <summary>町のプレハブの街灯の強さ・色をそろえる（鉱山は触らない）。</summary>
    internal static void ApplyTownLights()
    {
        const string prefab = "Assets/_Prefabs/Environment/RoomiesNeighborhood.prefab";
        var root = PrefabUtility.LoadPrefabContents(prefab);
        try
        {
            int n = 0;
            foreach (var light in root.GetComponentsInChildren<Light>(true))
            {
                if (InMining(light.transform)) continue;
                var row = TownLights.FirstOrDefault(t => light.name.StartsWith(t.name));
                if (row.name == null) continue;
                light.intensity = row.intensity; light.range = row.range; light.color = row.color; n++;
            }
            PrefabUtility.SaveAsPrefabAsset(root, prefab);
            Debug.Log($"[Material Finish] town lights: {n}");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void SetFloats(Object target, params (string name, float value)[] values)
    {
        var so = new SerializedObject(target);
        foreach (var v in values) { var p = so.FindProperty(v.name); if (p != null) p.floatValue = v.value; }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
