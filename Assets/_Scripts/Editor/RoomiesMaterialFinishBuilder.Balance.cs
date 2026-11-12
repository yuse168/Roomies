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
        ("Title key light", 4.5f),       // タイトル：顔の明かり（強すぎると青いキャラが白く飛ぶ）
        ("Bounce - rug under pendant", 3f),
        ("Bounce - sofa", 1f),
        ("Bounce - kitchen counter", 1f),
        ("Bounce - lantern floor", 1.1f),
        ("Bounce - fairy floor", .7f),
    };
    const float NightAmbient = .3f, NightReflection = .35f;

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
                    light.intensity = row.intensity; EditorUtility.SetDirty(light); n++;
                }
            foreach (var day in roots.SelectMany(g => g.GetComponentsInChildren<DayManager>(true)))
                SetFloats(day, ("nightAmbientIntensity", NightAmbient), ("nightReflectionIntensity", NightReflection));
            foreach (var menu in roots.SelectMany(g => g.GetComponentsInChildren<MainMenuRoomShowcase>(true)))
                SetFloats(menu, ("ambientIntensity", NightAmbient), ("reflectionIntensity", NightReflection));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Material Finish] light balance: {n} lights in {scene.name}");
        }
    }

    static void SetFloats(Object target, params (string name, float value)[] values)
    {
        var so = new SerializedObject(target);
        foreach (var v in values) { var p = so.FindProperty(v.name); if (p != null) p.floatValue = v.value; }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
