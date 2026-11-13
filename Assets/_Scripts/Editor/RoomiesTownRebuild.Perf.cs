#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 重さの切り分け用（Play Mode 中だけ。保存はされない）：町の作り直しの部品と遠景を一時的に隠す・戻す。
/// </summary>
internal static partial class RoomiesTownRebuild
{
    static readonly string[] PerfTargets = { FacadeGroup, HouseGroup, RooftopGroup, PropGroup, NightLightGroup };

    [MenuItem("Roomies/Art/Town/Perf - Hide Town Rebuild (Play Mode)")]
    static void PerfHide() => PerfToggle(false);

    [MenuItem("Roomies/Art/Town/Perf - Show Town Rebuild (Play Mode)")]
    static void PerfShow() => PerfToggle(true);

    /// <summary>
    /// 町を向いたカメラで、町の作り直しを出したとき・隠したときの描画時間を比べる（Play しない。シーンは保存しない）。
    /// 各視点で 60 回描いて、1 回ごとに GPU の終わりを待つ（1 画素読む）。結果は Temp/RoomiesTown/bench.txt。
    /// </summary>
    [MenuItem("Roomies/Art/Town/Perf - Bench Town Views")]
    static void BenchTownViews()
    {
        var views = new (string name, Vector3 pos, Vector3 target, float fov)[]
        {
            ("street", new Vector3(6, 2, 2), new Vector3(20, 3.2f, 14), 60),
            ("shops", new Vector3(15, 2, 7.5f), new Vector3(26, 3.2f, -8), 60),
            ("overview", new Vector3(-2, 11, -14), new Vector3(16, 1.5f, 10), 55),
            ("west", new Vector3(-11, 2, -1), new Vector3(-22, 3.6f, -9), 58),
            ("rooftops", new Vector3(6, 13, 2), new Vector3(24, 6, -13), 55),
        };
        var go = new GameObject("Town Bench Camera") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        var data = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        data.renderPostProcessing = true; data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        cam.allowHDR = true; cam.farClipPlane = 400; cam.nearClipPlane = .05f;
        var rt = new RenderTexture(1920, 1080, 24); cam.targetTexture = rt;
        var px = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        var sb = new System.Text.StringBuilder();
        var total = new System.Collections.Generic.Dictionary<string, double> { ["show"] = 0, ["hide"] = 0 };
        try
        {
            // 視点ごとに「出す・隠す」を 6 回ずつ 12 往復する（時間とともに変わるぶれが両方に同じだけ乗るように）
            foreach (var v in views)
            {
                cam.transform.position = v.pos; cam.transform.LookAt(v.target); cam.fieldOfView = v.fov;
                double show = 0, hide = 0;
                for (int k = 0; k < 12; k++)
                    foreach (bool on in new[] { k % 2 == 0, k % 2 != 0 })
                    {
                        SetTownVisible(on);
                        for (int i = 0; i < 2; i++) cam.Render();
                        RenderTexture.active = rt; px.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); RenderTexture.active = null;
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        for (int i = 0; i < 6; i++)
                        {
                            cam.Render();
                            RenderTexture.active = rt; px.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); RenderTexture.active = null;
                        }
                        if (on) show += sw.Elapsed.TotalMilliseconds; else hide += sw.Elapsed.TotalMilliseconds;
                    }
                show /= 72; hide /= 72;
                total["show"] += show; total["hide"] += hide;
                sb.AppendLine($"{v.name}: show {show:F2} ms, hide {hide:F2} ms, diff {show - hide:+0.00;-0.00} ms");
            }
        }
        finally
        {
            SetTownVisible(true);
            RenderTexture.active = null;
            Object.DestroyImmediate(go); Object.DestroyImmediate(rt); Object.DestroyImmediate(px);
        }
        sb.Insert(0, $"avg per view: show {total["show"] / views.Length:F2} ms, hide {total["hide"] / views.Length:F2} ms\n");
        System.IO.Directory.CreateDirectory("Temp/RoomiesTown");
        System.IO.File.WriteAllText("Temp/RoomiesTown/bench.txt", sb.ToString());
        Debug.Log("[Town] bench: " + sb.ToString().Split('\n')[0]);
    }

    static string benchSet = "all";

    [MenuItem("Roomies/Art/Town/Perf - Bench Split (Backdrop, Parts, Lights)")]
    static void BenchSplit()
    {
        var all = new System.Text.StringBuilder();
        foreach (var set in new[] { "backdrop", "parts", "lights", "b:Ground beyond the fence", "b:Outer town", "b:Mountains" })
        {
            benchSet = set;
            BenchTownViews();
            all.AppendLine("== " + set).Append(System.IO.File.ReadAllText("Temp/RoomiesTown/bench.txt"));
        }
        benchSet = "all";
        System.IO.File.WriteAllText("Temp/RoomiesTown/bench_split.txt", all.ToString());
    }

    static void SetTownVisible(bool on)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            foreach (var root in SceneManager.GetSceneAt(i).GetRootGameObjects())
            {
                if (root.name == "Roomies_Backdrop" && (benchSet == "all" || benchSet == "backdrop" || benchSet.StartsWith("b:")))
                    foreach (Transform c in root.transform)
                        if (BackdropGroups.Contains(c.name) && (!benchSet.StartsWith("b:") || benchSet == "b:" + c.name)) c.gameObject.SetActive(on);
                if (root.name == "Roomies_EnvironmentArt")
                    foreach (var g in PerfTargets)
                    {
                        if (benchSet == "parts" && g == NightLightGroup || benchSet == "lights" && g != NightLightGroup || benchSet == "backdrop" || benchSet.StartsWith("b:")) continue;
                        var t = root.transform.Find(g); if (t != null) t.gameObject.SetActive(on);
                    }
            }
    }

    [MenuItem("Roomies/Art/Town/Perf - Hide Nature (Play Mode)")]
    static void PerfHideNature() => NatureToggle(false);

    [MenuItem("Roomies/Art/Town/Perf - Show Nature (Play Mode)")]
    static void PerfShowNature() => NatureToggle(true);

    /// <summary>自然物（町の木・草の束・遠景の木・山・起伏のある地面）を一時的に隠す・戻す。Play Mode 中だけ。</summary>
    static void NatureToggle(bool on)
    {
        if (!EditorApplication.isPlaying) return;
        int n = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            foreach (var root in SceneManager.GetSceneAt(i).GetRootGameObjects())
            {
                if (root.name == "Roomies_Grass") { root.SetActive(on); n++; }
                if (root.name == "Roomies_EnvironmentArt") { var t = root.transform.Find("Town trees"); if (t != null) { t.gameObject.SetActive(on); n++; } }
                if (root.name == "Roomies_Backdrop")
                {
                    foreach (var name in new[] { "Mountains", "Outer terrain" }) { var t = root.transform.Find(name); if (t != null) { t.gameObject.SetActive(on); n++; } }
                    foreach (var lod in root.GetComponentsInChildren<LODGroup>(true)) { lod.gameObject.SetActive(on); n++; }
                }
            }
        Debug.Log($"[Town] nature toggle {(on ? "show" : "hide")}: {n}");
    }

    static void PerfToggle(bool on)
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("[Town] perf toggle works only in Play Mode"); return; }
        int n = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            foreach (var root in SceneManager.GetSceneAt(i).GetRootGameObjects())
            {
                if (root.name == "Roomies_Backdrop") { foreach (Transform c in root.transform) if (BackdropGroups.Contains(c.name)) { c.gameObject.SetActive(on); n++; } }
                if (root.name == "Roomies_EnvironmentArt") foreach (var g in PerfTargets) { var t = root.transform.Find(g); if (t != null) { t.gameObject.SetActive(on); n++; } }
            }
        Debug.Log($"[Town] perf toggle {(on ? "show" : "hide")}: {n} groups");
    }
}
#endif
