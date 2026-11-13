#if UNITY_EDITOR
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>作り直し2の調査（シーンは変えない）。Temp/RoomiesTown/survey3.txt。</summary>
internal static class RoomiesRebuild2Survey
{
    static string PathOf(Transform t) { var s = t.name; for (var x = t.parent; x != null; x = x.parent) s = x.name + "/" + s; return s; }

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
        foreach (var sm in Object.FindObjectsByType<SlotMachine>())
        {
            var t = sm.transform;
            sb.AppendLine($"SLOT {PathOf(t)} pos {t.position} fwd {t.forward} right {t.right} lossy {t.lossyScale}");
            foreach (var c in t.GetComponentsInChildren<Collider>(true)) sb.AppendLine($"   col {PathOf(c.transform)} {c.bounds.min:F3}-{c.bounds.max:F3}");
            foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true)) sb.AppendLine($"   ren {PathOf(r.transform)} {r.bounds.min:F3}-{r.bounds.max:F3} rot {r.transform.rotation.eulerAngles}");
            foreach (var rt in t.GetComponentsInChildren<RectTransform>(true))
            {
                var c4 = new Vector3[4]; rt.GetWorldCorners(c4);
                sb.AppendLine($"   ui {rt.name} {c4[0]:F3} {c4[2]:F3}");
            }
            break;
        }
        foreach (var bj in Object.FindObjectsByType<BlackjackTable>())
        {
            var t = bj.transform;
            sb.AppendLine($"BJ {t.position} fwd {t.forward}");
            foreach (var c in t.GetComponentsInChildren<Collider>(true)) sb.AppendLine($"   col {PathOf(c.transform)} {c.bounds.min:F3}-{c.bounds.max:F3} trig {c.isTrigger}");
        }
        foreach (var r in Object.FindObjectsByType<MeshRenderer>())
            if (r.name == "pCube2" || r.name.Contains("Casino_Carpet") || r.name == "pCube8") sb.AppendLine($"FLOOR {PathOf(r.transform)} {r.bounds.min:F3}-{r.bounds.max:F3} enabled {r.enabled} mat {r.sharedMaterial?.name}");
        System.IO.File.WriteAllText("Temp/RoomiesTown/survey3.txt", sb.ToString());
    }
}
#endif
