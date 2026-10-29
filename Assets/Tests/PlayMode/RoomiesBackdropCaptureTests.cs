using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static RoomiesPlayHarness;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

/// <summary>
/// 遠景（Roomies_Backdrop / V2b）の見た目と重さを、入れる前と後で同じ条件で撮る・測るための道具。
/// ふだんのテストでは動かさない（Explicit）。名前を指定したときだけ動く。
/// 環境変数：ROOMIES_CAPTURE_DIR（保存先）、ROOMIES_CAPTURE_TAG（ファイル名の頭。before / after など）
/// </summary>
[TestFixture, Explicit("遠景の撮影・計測用。名前を指定したときだけ動く"), Category("Capture")]
public class RoomiesBackdropCaptureTests : IPrebuildSetup, IPostBuildCleanup
{
    readonly RoomiesPlayLoopTests startScene = new();
    public void Setup() => startScene.Setup();
    public void Cleanup() => startScene.Cleanup();

    // 同じ位置・向きで撮る（位置は GameRoom の座標。家は x13〜23・z14〜23.5、床 3.3）
    static readonly (string name, Vector3 pos, Vector3 lookAt)[] Shots =
    {
        ("A_roof_west",  new Vector3(18f, 12f, 18f), new Vector3(-200f, 20f, 18f)),   // 家の屋根の高さから西（近い高架ごし）
        ("B_roof_east",  new Vector3(18f, 12f, 18f), new Vector3(300f, 20f, 18f)),
        ("C_roof_north", new Vector3(18f, 12f, 18f), new Vector3(18f, 20f, 300f)),
        ("D_eye_south",  new Vector3(18f, 1.6f, 0f), new Vector3(18f, 6f, -300f)),    // 地面の目の高さから南
        ("E_high_wide",  new Vector3(0f, 260f, 120f), new Vector3(0f, 0f, -150f)),    // 上空（ふだんは見えない。並びの確認用）
    };

    static string Tag => Environment.GetEnvironmentVariable("ROOMIES_CAPTURE_TAG") ?? "shot";

    [UnityTest]
    public IEnumerator CaptureDayNightAndMeasure()
    {
        LogAssert.ignoreFailingMessages = true;
        BackupRealSave();
        try
        {
            yield return BootHost();
            var cam = FindGameCamera();
            Assert.That(cam, Is.Not.Null);
            string report = "";

            // 遠景の状態
            var oldBg = GameObject.Find("Roomies_Backdrop");
            var v2b = GameObject.Find("Roomies_Backdrop_V2b");
            report += $"Roomies_Backdrop active={(oldBg != null && oldBg.activeInHierarchy)} / Roomies_Backdrop_V2b active={(v2b != null && v2b.activeInHierarchy)}\n";

            // 撮影用のカメラ（プレイヤーのカメラと同じ画角・far）
            var shotCam = new GameObject("CaptureCamera").AddComponent<Camera>();
            shotCam.CopyFrom(cam);
            shotCam.enabled = false;
            report += $"camera fov={shotCam.fieldOfView} far={shotCam.farClipPlane}\n";

            yield return new WaitForSecondsRealtime(1f);
            foreach (var s in Shots) Shoot(shotCam, s, "day");
            report += Measure(shotCam, Shots[0], "day");

            // 電車が走っているか（BackdropTrain の位置が変わるか）
            var trains = Object.FindObjectsByType(GameType("BackdropTrain"), FindObjectsInactive.Exclude).Cast<Component>().ToArray();
            var p0 = trains.Select(t => t.transform.position).ToArray();
            yield return new WaitForSeconds(3f);
            int moved = trains.Where((t, i) => (t.transform.position - p0[i]).sqrMagnitude > 0.01f).Count();
            report += $"BackdropTrain active={trains.Length} moved_in_3s={moved} : {string.Join(", ", trains.Select(t => t.transform.root.name + "/" + t.name))}\n";

            // 夜
            yield return GoToNight();
            yield return new WaitForSecondsRealtime(1f);
            foreach (var s in Shots.Take(2).Concat(Shots.Skip(4))) Shoot(shotCam, s, "night");
            report += Measure(shotCam, Shots[0], "night");

            // ふつうのフレーム（プレイヤーのカメラで描く）の平均
            int frames = 0; float t0 = Time.realtimeSinceStartup;
            while (frames < 300) { frames++; yield return null; }
            float avg = (Time.realtimeSinceStartup - t0) / frames;
            report += $"player-camera frames: avg {avg * 1000f:F2} ms ({1f / avg:F1} fps, batchmode)\n";

            Debug.Log("[BackdropReport]\n" + report);
            TestContext.WriteLine(report);
            if (!string.IsNullOrEmpty(CaptureDir)) File.WriteAllText(Path.Combine(CaptureDir, Tag + "_report.txt"), report);
            Object.Destroy(shotCam.gameObject);
        }
        finally
        {
            if (Network != null) { Network.Shutdown(); Object.Destroy(Network.gameObject); }
            RestoreRealSave();
        }
    }

    static void Place(Camera c, (string name, Vector3 pos, Vector3 lookAt) s)
    {
        c.transform.position = s.pos;
        c.transform.rotation = Quaternion.LookRotation(s.lookAt - s.pos, Vector3.up);
    }

    static void Shoot(Camera c, (string name, Vector3 pos, Vector3 lookAt) s, string time)
    {
        Place(c, s);
        // 遠景だけ見たいので UI は写さない（別のカメラで撮る：Overlay は切り替えずに済むよう、一時的に UI を隠す）
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).Where(cv => cv.isRootCanvas && cv.enabled).ToArray();
        foreach (var cv in canvases) cv.enabled = false;
        try { Capture($"{Tag}_{time}_{s.name}", 1600, 900, c); }
        finally { foreach (var cv in canvases) if (cv != null) cv.enabled = true; }
    }

    /// <summary>同じ向きで 60回描いた時間の平均（CPU 側の描画の重さの目安）。三角形・描画数はエディターの統計から。</summary>
    static string Measure(Camera c, (string name, Vector3 pos, Vector3 lookAt) s, string time)
    {
        Place(c, s);
        var rt = RenderTexture.GetTemporary(1600, 900, 24);
        c.targetTexture = rt;
        var px = new Texture2D(1, 1, TextureFormat.RGB24, false);
        void RenderAndWait()
        {
            c.Render();
            // 1px 読み戻して GPU の描き終わりまで待つ（CPU の命令を出す時間だけでなく GPU の時間も入れる）
            var prev = RenderTexture.active; RenderTexture.active = rt;
            px.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false);
            RenderTexture.active = prev;
        }
        for (int i = 0; i < 5; i++) RenderAndWait(); // 温める
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 60; i++) RenderAndWait();
        sw.Stop();
        Object.Destroy(px);
        // 見えている描画物の数（遠景だけの目安）
        var planes = GeometryUtility.CalculateFrustumPlanes(c);
        var visible = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
            .Where(r => r.enabled && GeometryUtility.TestPlanesAABB(planes, r.bounds)).ToArray();
        long tris = 0;
        foreach (var r in visible)
            if (r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null && mf.sharedMesh.isReadable)
                tris += mf.sharedMesh.triangles.LongLength / 3;
        c.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        double ms = sw.Elapsed.TotalMilliseconds / 60;
        return $"{time} {s.name}: {ms:F2} ms/frame（≒{1000 / ms:F0} fps、描画＋GPU待ち、60回平均、1600x900） visible renderers={visible.Length} readable tris≈{tris}\n";
    }
}
