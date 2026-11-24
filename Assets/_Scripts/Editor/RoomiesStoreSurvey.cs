#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// コンビニ（CORNER STORE）のまわりに、いま何が置いてあるかを書き出す（調査用。何も変えない）。
/// Roomies/Store/Survey → Temp/RoomiesStore/survey.txt
/// </summary>
internal static class RoomiesStoreSurvey
{
    // 調べる範囲（建物と、その前の歩道）
    static readonly Bounds Area = new Bounds(new Vector3(26.1f, 4f, -12.5f), new Vector3(17f, 12f, 15f));

    [MenuItem("Roomies/Store/Survey")]
    static void Survey()
    {
        var sb = new StringBuilder();
        foreach (var scene in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s.isLoaded))
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!Area.Intersects(r.bounds) || r.bounds.size.magnitude > 60f) continue;
                    var b = r.bounds;
                    sb.AppendLine($"REN {Path(r.transform)} | mat {(r.sharedMaterial != null ? r.sharedMaterial.name : "-")} | min {b.min.x:F1},{b.min.y:F1},{b.min.z:F1} max {b.max.x:F1},{b.max.y:F1},{b.max.z:F1}{(r.gameObject.activeInHierarchy ? "" : " (off)")}");
                }
                foreach (var c in root.GetComponentsInChildren<Collider>(true))
                {
                    if (!Area.Intersects(c.bounds) || c.bounds.size.magnitude > 60f) continue;
                    var b = c.bounds;
                    sb.AppendLine($"{(c.isTrigger ? "TRG" : "COL")} {Path(c.transform)} | min {b.min.x:F1},{b.min.y:F1},{b.min.z:F1} max {b.max.x:F1},{b.max.y:F1},{b.max.z:F1}");
                }
                foreach (var l in root.GetComponentsInChildren<Light>(true))
                    if (Area.Contains(l.transform.position)) sb.AppendLine($"LGT {Path(l.transform)} | {l.type} {l.intensity:F1} range {l.range:F1} at {l.transform.position:F1}");
                foreach (var m in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (m != null && Area.Contains(m.transform.position) && !(m is TMPro.TMP_Text)) sb.AppendLine($"SCR {Path(m.transform)} | {m.GetType().Name} at {m.transform.position:F1}");
                foreach (var t in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    if (Area.Contains(t.transform.position)) sb.AppendLine($"TXT {Path(t.transform)} | '{t.text}' at {t.transform.position:F1}");
            }
        Directory.CreateDirectory("Temp/RoomiesStore");
        File.WriteAllText("Temp/RoomiesStore/survey.txt", sb.ToString());
        Debug.Log("[Store] survey written: Temp/RoomiesStore/survey.txt");
    }

    static string Path(Transform t)
    {
        var s = t.name;
        for (var x = t.parent; x != null; x = x.parent) s = x.name + "/" + s;
        return s;
    }
}
#endif
