#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 3 光の回り方：ベイクなしで「照り返し」を足す。
/// ・床やソファからの照り返しを、上向きの影なしスポットライトで足す（天井・壁の上・家具の裏が少し明るくなる）
/// ・部屋の中を写した反射プローブを置く（つやのある物に外の空ではなく部屋が映る）。ベイクは数秒
/// ・吊りランプの影の解像度を下げて、影のふちを柔らかくする
/// ライトマップは使わない（部屋のメッシュにライトマップ用 UV がない／昼と夜を DayManager が切り替えるため）。
/// 置く物はシーンの「Roomies_LightBounce」。位置や強さを直したら下の表もそろえること。
/// </summary>
internal static partial class RoomiesMaterialFinishBuilder
{
    const string BounceRootName = "Roomies_LightBounce";
    const string ProbeDir = "Assets/_Art/Restyle/Probes/";

    // 照り返し：名前, 位置, 向き（オイラー）, 色, 強さ, 届く距離, 角度
    static readonly (string name, Vector3 pos, Vector3 rot, Color color, float intensity, float range, float angle)[] Bounces =
    {
        ("Bounce - rug under pendant", new Vector3(16.5f, FloorY + .08f, 19.85f), new Vector3(-90, 0, 0), new Color(1f, .7f, .55f), 4.5f, 5.5f, 150),
        ("Bounce - sofa", new Vector3(16.5f, FloorY + .55f, 20.6f), new Vector3(-35, 0, 0), new Color(1f, .82f, .4f), 1.6f, 3.5f, 140),
        ("Bounce - kitchen counter", new Vector3(18.9f, FloorY + .95f, 22.6f), new Vector3(-90, 0, 0), new Color(1f, .78f, .55f), 1.6f, 3.5f, 150),
        ("Bounce - lantern floor", new Vector3(17.3f, FloorY + .08f, 15.7f), new Vector3(-90, 0, 0), new Color(1f, .72f, .5f), 1.8f, 4.5f, 150),
        ("Bounce - fairy floor", new Vector3(14.2f, FloorY + .08f, 18.5f), new Vector3(-90, 0, 0), new Color(1f, .7f, .48f), 1.2f, 4f, 150),
    };

    [MenuItem("Roomies/Art/Restyle/Material Finish/3 Light Bounce")]
    internal static void ApplyLightBounce()
    {
        Directory.CreateDirectory(ProbeDir);
        foreach (var scene in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray())
        {
            if (!scene.isLoaded) continue;
            var room = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Roomies_RoomRestyle");
            if (room == null) continue;

            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == BounceRootName).ToArray()) Object.DestroyImmediate(old);
            var root = new GameObject(BounceRootName);
            SceneManager.MoveGameObjectToScene(root, scene);

            foreach (var b in Bounces)
            {
                var go = new GameObject(b.name);
                go.transform.SetParent(root.transform, false);
                go.transform.SetPositionAndRotation(b.pos, Quaternion.Euler(b.rot));
                var light = go.AddComponent<Light>();
                light.type = LightType.Spot; light.color = b.color; light.intensity = b.intensity; light.range = b.range;
                light.spotAngle = b.angle; light.innerSpotAngle = 30; light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.Auto; light.lightmapBakeType = LightmapBakeType.Realtime;
            }

            // 吊りランプの影を柔らかく（影の解像度を中にする。PCF のぼかしが広く見える）
            foreach (var light in room.GetComponentsInChildren<Light>(true).Where(l => l.name == "Pendant light"))
            {
                var data = light.GetComponent<UniversalAdditionalLightData>() ?? light.gameObject.AddComponent<UniversalAdditionalLightData>();
                var so = new SerializedObject(data);
                so.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium;
                so.ApplyModifiedPropertiesWithoutUndo();
                light.shadowStrength = .85f;
                light.shadowNearPlane = .08f;
                EditorUtility.SetDirty(light); EditorUtility.SetDirty(data);
            }

            // 部屋の反射プローブ（箱に合わせて映す）
            var probeGo = new GameObject("Room reflection probe");
            probeGo.transform.SetParent(root.transform, false);
            probeGo.transform.position = new Vector3(17.93f, FloorY + 1.35f, 18.68f);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.size = new Vector3(9.4f, 2.7f, 9.4f);
            probe.boxProjection = true; probe.resolution = 128; probe.importance = 2; probe.blendDistance = .5f;
            probe.hdr = true; probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.mode = ReflectionProbeMode.Custom;
            string path = ProbeDir + scene.name + "_room_reflection.exr";
            if (Lightmapping.BakeReflectionProbe(probe, path))
            {
                AssetDatabase.ImportAsset(path);
                probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            }
            else Debug.LogWarning("[Material Finish] reflection probe bake failed: " + path);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Material Finish] light bounce placed in {scene.name}");
        }
    }
}
#endif
