#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// 4 接地感：URP にはライトごとの Contact Shadows がないので、同じ効果を次で出す。
/// ・SSAO を直接光にも効かせる（PC_Renderer の SSAO の値。アセットに保存済み）
/// ・影を落とすランプの影のずれ（バイアス）を小さくして、物と影のすき間をなくす
/// ・ちょうちん（タイトルは顔を照らす明かり）にも影を付ける
/// 値は下の表。Build Room で明かりを作り直したあとも RoomiesRestyleKit.End() からかけ直す。
/// </summary>
internal static partial class RoomiesMaterialFinishBuilder
{
    // 影を落とす明かり：名前, 影の解像度（0 低 / 1 中 / 2 高）, 影の濃さ, バイアス, 法線バイアス
    static readonly (string name, int tier, float strength, float bias, float normalBias)[] ShadowLights =
    {
        ("Pendant light", 1, .9f, .01f, .08f),
        ("Lantern light", 1, .75f, .01f, .08f),
        ("Title key light", 1, .8f, .01f, .08f),
    };

    [MenuItem("Roomies/Art/Restyle/Material Finish/4 Contact Shadows")]
    internal static void ApplyContactShadows()
    {
        foreach (var scene in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray())
        {
            if (!scene.isLoaded) continue;
            var room = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Roomies_RoomRestyle");
            if (room == null) continue;
            int n = 0;
            foreach (var light in room.GetComponentsInChildren<Light>(true))
            {
                var row = ShadowLights.FirstOrDefault(s => s.name == light.name);
                if (row.name == null) continue;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = row.strength;
                light.shadowBias = row.bias; light.shadowNormalBias = row.normalBias; light.shadowNearPlane = .05f;
                var data = light.GetComponent<UniversalAdditionalLightData>() ?? light.gameObject.AddComponent<UniversalAdditionalLightData>();
                var so = new SerializedObject(data);
                so.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = row.tier;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(light); EditorUtility.SetDirty(data);
                n++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Material Finish] contact shadows: {n} lights in {scene.name}");
        }
    }
}
#endif
