#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 町の作り直し（2026/11/13）。配置ツール。鉱山（Roomies_Mining）は触らない。
/// </summary>
internal static partial class RoomiesTownRebuild
{
    /// <summary>町の見た目と当たり判定の一覧を Temp/RoomiesTown/survey.txt に書く（調査用。シーンは変えない）。</summary>
    [MenuItem("Roomies/Art/Town/Survey")]
    internal static void Survey()
    {
        var sb = new StringBuilder();
        foreach (var scene in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s.isLoaded))
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name.StartsWith("Roomies_Mining") || root.name.StartsWith("Roomies_RoomRestyle")) continue;
                foreach (var c in root.GetComponentsInChildren<Collider>(true))
                {
                    var b = c.bounds;
                    sb.AppendLine($"{(c.isTrigger ? "TRG" : "COL")}{Path(c.transform)} [{c.GetType().Name}] min {b.min.x:F1},{b.min.y:F1},{b.min.z:F1} max {b.max.x:F1},{b.max.y:F1},{b.max.z:F1}");
                }
                if (root.name == "Demo" || root.name == "Roomies_EnvironmentArt")
                    foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var b = r.bounds;
                        if (b.size.magnitude < 2.5f && root.name != "Demo") continue;
                        sb.AppendLine($"REN {Path(r.transform)} mat {r.sharedMaterial?.name} min {b.min.x:F1},{b.min.y:F1},{b.min.z:F1} max {b.max.x:F1},{b.max.y:F1},{b.max.z:F1}");
                    }
            }
        Directory.CreateDirectory("Temp/RoomiesTown");
        File.WriteAllText("Temp/RoomiesTown/survey.txt", sb.ToString());
        Debug.Log("[Town] survey written");
    }

    static string Path(Transform t)
    {
        var s = t.name;
        for (var x = t.parent; x != null; x = x.parent) s = x.name + "/" + s;
        return s;
    }
}
#endif
