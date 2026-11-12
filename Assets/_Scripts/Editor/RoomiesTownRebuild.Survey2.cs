#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static partial class RoomiesTownRebuild
{
    /// <summary>参照切れの確認（シーンは変えない）：素材のない描画物・メッシュのない MeshFilter・外れたスクリプト。開いているシーンすべて。</summary>
    [MenuItem("Roomies/Art/Town/Check References")]
    internal static void CheckReferences()
    {
        int noMat = 0, noMesh = 0, missing = 0, renderers = 0;
        var sb = new StringBuilder();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    renderers++;
                    if (r is ParticleSystemRenderer) continue;
                    if (r.sharedMaterials.Any(m => m == null)) { noMat++; sb.AppendLine("NOMAT " + Path(r.transform)); }
                }
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh == null) { noMesh++; sb.AppendLine("NOMESH " + Path(mf.transform)); }
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                    if (n > 0) { missing += n; sb.AppendLine("MISSING " + Path(t)); }
                }
            }
        }
        sb.Insert(0, $"renderers {renderers} noMaterial {noMat} noMesh {noMesh} missingScripts {missing}\n");
        Directory.CreateDirectory("Temp/RoomiesTown");
        File.WriteAllText("Temp/RoomiesTown/check.txt", sb.ToString());
        Debug.Log("[Town] check: " + sb.ToString().Split('\n')[0]);
    }

    /// <summary>家と屋根の調査（シーンは変えない）。Temp/RoomiesTown/survey2.txt。</summary>
    [MenuItem("Roomies/Art/Town/Survey Houses")]
    internal static void SurveyHouses()
    {
        var sb = new StringBuilder();
        var scene = SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            var rs = root.GetComponentsInChildren<Renderer>(true);
            sb.AppendLine($"ROOT {root.name} children {root.transform.childCount} renderers {rs.Length} prefab {PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root)}");
            foreach (Transform c in root.transform)
            {
                var r2 = c.GetComponentsInChildren<Renderer>(true);
                sb.AppendLine($"   {c.name} ({c.childCount}) ren {r2.Length}");
            }
        }
        var back = GameObject.Find("Roomies_Backdrop");
        if (back != null)
            foreach (var t in back.GetComponentsInChildren<Transform>(true).Where(t => t.name == "House"))
            {
                var b = new Bounds(t.position, Vector3.zero);
                foreach (var r in t.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                sb.AppendLine($"BHOUSE {t.position.x:F0},{t.position.z:F0} dist {new Vector2(t.position.x, t.position.z).magnitude:F0} rotY {t.eulerAngles.y:F0} size {b.size.x:F1}x{b.size.y:F1}x{b.size.z:F1} kids {t.childCount}");
            }
        if (back != null)
            foreach (Transform vis in back.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Visual"))
                foreach (Transform t in vis)
                {
                    float d = new Vector2(t.position.x, t.position.z).magnitude;
                    if (d > 130 || t.name == "House") continue;
                    sb.AppendLine($"BOBJ {Path(t)} {t.position.x:F0},{t.position.y:F0},{t.position.z:F0} dist {d:F0} kids {t.childCount}");
                }
        Directory.CreateDirectory("Temp/RoomiesTown");
        File.WriteAllText("Temp/RoomiesTown/survey2.txt", sb.ToString());
        Debug.Log("[Town] survey2 written");
    }
}
#endif
