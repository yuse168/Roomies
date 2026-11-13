#if UNITY_EDITOR
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>作り直し2の調査（シーンは変えない）。Temp/RoomiesTown/survey3.txt。</summary>
internal static class RoomiesRebuild2Survey
{
    [MenuItem("Roomies/Art/Town/Survey Gimmicks")]
    static void Run()
    {
        var sb = new StringBuilder();
        var scene = SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name.StartsWith("Roomies_Mining") || root.name == "Roomies_Backdrop" || root.name == "Roomies_Grass") continue;
            var rs = root.GetComponentsInChildren<Renderer>(true);
            var b = rs.Length > 0 ? rs.Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; }) : new Bounds(root.transform.position, Vector3.zero);
            var comps = string.Join(",", root.GetComponentsInChildren<MonoBehaviour>(true).Select(m => m ? m.GetType().Name : "MISSING").Distinct());
            sb.AppendLine($"{root.name} pos {root.transform.position} bounds {b.min:F1}-{b.max:F1} scripts [{comps}]");
        }
        foreach (var n in new[] { "Shared house - lounge and terrace", "Community Jail", "Workplaces - facades and loading details", "Street - sidewalks and wayfinding", "Entry stairs" })
        {
            var env = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Roomies_EnvironmentArt"); var gt = env != null ? env.transform.Find(n) : null; var g = gt != null ? gt.gameObject : null;
            if (g == null) continue;
            sb.AppendLine("== " + n);
            foreach (Transform c in g.transform)
            {
                var r = c.GetComponent<Renderer>(); var col = c.GetComponent<Collider>();
                var b = r ? r.bounds : col ? col.bounds : new Bounds(c.position, Vector3.zero);
                var comps = string.Join(",", c.GetComponents<Component>().Select(x => x.GetType().Name).Where(x => x != "Transform"));
                sb.AppendLine($"  {c.name} [{comps}] {b.min:F2}-{b.max:F2} mat {(r ? r.sharedMaterial?.name : "")} kids {c.childCount}");
            }
        }
        System.IO.File.WriteAllText("Temp/RoomiesTown/survey3.txt", sb.ToString());
    }
}
#endif
