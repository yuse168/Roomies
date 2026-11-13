#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 重さの計測（Play Mode・ホストで GameRoom に入ってから）：ゲームの中で毎フレーム UnityStats を読み、
/// 位置（出てくる位置・通り・カジノの前）ごとに「全部出す」「作り直し2を隠す」「自然物を隠す」を交互に 4 往復ずつ測る。
/// 結果は Temp/RoomiesTown/perf_ingame.txt（各 60 フレームの中央値）。
/// </summary>
internal static partial class RoomiesTownRebuild
{
    [MenuItem("Roomies/Art/Town/Perf - Measure In Game (Play Mode)")]
    static void PerfInGame()
    {
        if (!EditorApplication.isPlaying) return;
        var cc = Object.FindObjectsByType<CharacterController>().FirstOrDefault(c => c.GetComponentInParent<PlayerMovement>() != null);
        if (cc == null) { Debug.LogWarning("[Town] no player"); return; }
        var runner = new GameObject("Perf Runner").AddComponent<GimmickCheckRunner>();
        runner.StartCoroutine(PerfRun(cc, runner));
    }

    static readonly string[] Rebuild2Groups = { "Town jail", "Town main house", "Town casino", "Town road" };

    static void SetRebuild2(bool on)
    {
        var env = GameObject.Find("Roomies_EnvironmentArt");
        if (env == null) return;
        foreach (var n in Rebuild2Groups) { var t = env.transform.Find(n); if (t != null) t.gameObject.SetActive(on); }
    }

    static IEnumerator PerfRun(CharacterController cc, MonoBehaviour runner)
    {
        var sb = new StringBuilder();
        Vector3 home = cc.transform.position; var homeRot = cc.transform.rotation;
        var places = new (string name, Vector3 pos, Vector3 look)[]
        {
            ("spawn", home, home + homeRot * Vector3.forward),
            ("street", new Vector3(12f, 1.15f, 5.2f), new Vector3(20f, 1.15f, -3f)),
            ("casino", new Vector3(8f, 1.15f, -5.5f), new Vector3(8f, 1.15f, -15f)),
        };
        var modes = new[] { "all", "no_rebuild2", "no_nature" };
        foreach (var place in places)
        {
            Teleport(cc, place.pos);
            cc.transform.rotation = Quaternion.LookRotation(new Vector3(place.look.x - place.pos.x, 0, place.look.z - place.pos.z));
            for (int w = 0; w < 30; w++) yield return null;
            var res = modes.ToDictionary(m => m, m => (ft: new List<float>(), tri: new List<float>(), dc: new List<float>()));
            for (int round = 0; round < 4; round++)
                foreach (var m in modes)
                {
                    SetRebuild2(m != "no_rebuild2");
                    NatureToggleRaw(m != "no_nature");
                    for (int w = 0; w < 10; w++) yield return null;
                    for (int f = 0; f < 60; f++)
                    {
                        yield return null;
                        res[m].ft.Add(UnityStats.renderTime * 1000f);
                        res[m].tri.Add(UnityStats.triangles);
                        res[m].dc.Add(UnityStats.drawCalls);
                    }
                }
            SetRebuild2(true); NatureToggleRaw(true);
            foreach (var m in modes)
            {
                float Med(List<float> l) { var s = l.OrderBy(x => x).ToList(); return s[s.Count / 2]; }
                sb.AppendLine($"{place.name,-7} {m,-12} render med {Med(res[m].ft):F2} ms  mean {res[m].ft.Average():F2} ms  tris {Med(res[m].tri):F0}  draws {Med(res[m].dc):F0}");
            }
        }
        Teleport(cc, home); cc.transform.rotation = homeRot;
        System.IO.Directory.CreateDirectory("Temp/RoomiesTown");
        System.IO.File.WriteAllText("Temp/RoomiesTown/perf_ingame.txt", sb.ToString());
        Debug.Log("[Town] perf in game written");
        Object.Destroy(runner.gameObject);
    }

    /// <summary>NatureToggle と同じ（Play Mode のチェックとログなし）。</summary>
    static void NatureToggleRaw(bool on)
    {
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).GetRootGameObjects())
            {
                if (root.name == "Roomies_Grass") root.SetActive(on);
                if (root.name == "Roomies_EnvironmentArt") { var t = root.transform.Find("Town trees"); if (t != null) t.gameObject.SetActive(on); }
                if (root.name == "Roomies_Backdrop")
                {
                    foreach (var name in new[] { "Mountains", "Outer terrain" }) { var t = root.transform.Find(name); if (t != null) t.gameObject.SetActive(on); }
                    foreach (var lod in root.GetComponentsInChildren<LODGroup>(true)) lod.gameObject.SetActive(on);
                }
            }
    }
}
#endif
