#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 確認用：スロットの演出とブラックジャックの 1 回ぶんを、コマ送りの写真にする（Play Mode で、町に入ってから実行）。
///   Roomies/Casino/Demo/Capture Slot Frames … 外れ・小当たり・リーチ外れ・大当たり・7 ぞろいの演出だけを順に流して撮る（お金は動かない）
///   Roomies/Casino/Demo/Capture Blackjack Frames … 本当に 4 回遊んで撮る（お金が動く。確認のときはセーブの控えを取ること）
/// 写真は Temp/RoomiesCasino/frames/ の下。終わると done.txt を書く。ゲームには何も残さない。
/// </summary>
internal static class RoomiesCasinoDemo
{
    const string OutDir = "Temp/RoomiesCasino/frames";
    const int Width = 960, Height = 540;

    class Runner : MonoBehaviour { }

    static Runner NewRunner()
    {
        var go = new GameObject("Casino Demo") { hideFlags = HideFlags.DontSave };
        return go.AddComponent<Runner>();
    }

    static Camera NewCamera(Transform parent, Vector3 position, Vector3 lookAt, float fov)
    {
        var go = new GameObject("Casino Demo Camera") { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = fov; cam.nearClipPlane = .05f; cam.farClipPlane = 200f;
        cam.enabled = false;          // 自分で Render を呼ぶときだけ描く
        var main = Camera.main;
        if (main != null)
        {
            cam.clearFlags = main.clearFlags; cam.backgroundColor = main.backgroundColor; cam.cullingMask = main.cullingMask;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
        }
        return cam;
    }

    static void Shoot(Camera cam, string folder, string name)
    {
        var rt = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        cam.targetTexture = rt;
        cam.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        tex.Apply();
        RenderTexture.active = previous;
        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        Directory.CreateDirectory($"{OutDir}/{folder}");
        File.WriteAllBytes($"{OutDir}/{folder}/{name}.png", tex.EncodeToPNG());
        Object.Destroy(tex);
    }

    static CharacterController MyController()
    {
        var nm = Unity.Netcode.NetworkManager.Singleton;
        var player = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
        return player != null ? player.GetComponentInChildren<CharacterController>() : null;
    }

    static void Teleport(CharacterController cc, Vector3 feet)
    {
        cc.enabled = false; cc.transform.position = feet + Vector3.up * (cc.height / 2 + .08f); Physics.SyncTransforms(); cc.enabled = true;
    }

    // ================================================================ スロット
    [MenuItem("Roomies/Casino/Demo/Capture Slot Frames (Play Mode)")]
    static void CaptureSlot()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Casino] Play Mode で実行してください"); return; }
        var runner = NewRunner();
        runner.StartCoroutine(SlotRoutine(runner));
    }

    static IEnumerator SlotRoutine(Runner runner)
    {
        Directory.CreateDirectory(OutDir);
        if (Directory.Exists(OutDir + "/slot")) Directory.Delete(OutDir + "/slot", true);
        File.Delete(OutDir + "/slot_done.txt");
        var slot = Object.FindObjectsByType<SlotMachine>().OrderBy(s => s.transform.position.y).ThenBy(s => s.name).First();
        var cc = MyController();
        if (cc != null) Teleport(cc, slot.transform.position + slot.transform.forward * 2.2f + slot.transform.right * 1.2f);
        yield return new WaitForSeconds(.5f);

        Vector3 screen = slot.transform.TransformPoint(new Vector3(0, 1.3f, .37f));
        Camera cam = NewCamera(runner.transform, screen + slot.transform.forward * 1.55f + Vector3.up * .05f, screen, 50);

        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        MethodInfo spin = typeof(SlotMachine).GetMethod("SpinRoutine", flags);
        var screenComp = slot.GetComponent<SlotScreen>();

        // (名前, 左, 中, 右, 結果の文字, 当たりの額, 段, 撮る長さ, フィーバーの背景)
        var cases = new (string name, int a, int b, int c, string message, int reward, int tier, float seconds, bool fever)[]
        {
            ("1_miss", 0, 1, 2, "MISS", 0, 0, 1.9f, false),
            ("2_small_win", 0, 0, 0, "WIN +10", 10, 1, 2.6f, false),
            ("3_reach_miss", 3, 3, 1, "MISS", 0, 0, 3.8f, false),
            ("4_big_win", 3, 3, 3, "WIN +50", 50, 3, 5.6f, false),
            ("5_jackpot", 4, 4, 4, "FEVER +100", 100, 4, 7.0f, false),
            ("6_fever_spin", 2, 2, 2, "FEVER WIN +60", 60, 2, 4.6f, true),
        };
        foreach (var k in cases)
        {
            screenComp.SetFever(k.fever);
            yield return new WaitForSeconds(.7f);
            slot.StartCoroutine((IEnumerator)spin.Invoke(slot, new object[] { k.a, k.b, k.c, k.message, k.reward, k.tier }));
            float t = 0; int frame = 0;
            while (t < k.seconds)
            {
                yield return new WaitForEndOfFrame();
                if (t >= frame * .1f) { Shoot(cam, "slot/" + k.name, $"f_{frame:D3}"); frame++; }
                t += Time.deltaTime;
            }
        }
        screenComp.SetFever(false);
        File.WriteAllText(OutDir + "/slot_done.txt", "done");
        Debug.Log("[Casino] slot frames written");
        Object.Destroy(runner.gameObject);
    }

    // ================================================================ ブラックジャック
    [MenuItem("Roomies/Casino/Demo/Capture Blackjack Frames (Play Mode)")]
    static void CaptureBlackjack()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Casino] Play Mode で実行してください"); return; }
        var runner = NewRunner();
        runner.StartCoroutine(BlackjackRoutine(runner));
    }

    static IEnumerator BlackjackRoutine(Runner runner)
    {
        Directory.CreateDirectory(OutDir);
        if (Directory.Exists(OutDir + "/bj")) Directory.Delete(OutDir + "/bj", true);
        File.Delete(OutDir + "/bj_done.txt");
        var bj = Object.FindObjectsByType<BlackjackTable>().OrderBy(b => b.transform.position.y).First();
        var view = bj.GetComponent<BlackjackTableView>();
        var cc = MyController();
        Teleport(cc, bj.transform.position + bj.transform.forward * 1.9f + bj.transform.right * 1.3f);
        yield return new WaitForSeconds(.5f);
        var earning = cc.GetComponentInParent<PlayerEarning>();
        ulong me = cc.GetComponentInParent<Unity.Netcode.NetworkObject>().OwnerClientId;

        // 遊ぶ人の目の高さから、台を見下ろす
        Vector3 eye = bj.transform.TransformPoint(new Vector3(0, 1.72f, 1.5f)), look = bj.transform.TransformPoint(new Vector3(0, .95f, .1f));
        Camera cam = NewCamera(runner.transform, eye, look, 58);
        var log = new System.Text.StringBuilder();

        for (int round = 0; round < 4; round++)
        {
            float w = 0; while (!bj.IsIdle && w < 12f) { yield return new WaitForSeconds(.1f); w += .1f; }
            yield return new WaitForSeconds(.6f);
            string folder = $"bj/round_{round + 1}";
            float t = 0, nextAct = 2.4f; int frame = 0; bool acted = false, started = false; string hands = "";
            int hits = round == 1 ? 1 : 0;      // 2 回目は 1 枚引く
            while (t < 14f)
            {
                yield return new WaitForEndOfFrame();
                if (t >= frame * .15f) { Shoot(cam, folder, $"f_{frame:D3}"); frame++; }
                t += Time.deltaTime;
                if (!started && t > .3f) { started = true; bj.Interact(earning); }
                // 配り終わってから操作する
                if (started && !acted && t > nextAct && bj.CanLocalPlayerStand(me) && !view.IsBusy)
                {
                    if (hits > 0) { hits--; bj.Interact(earning); nextAct = t + 1.2f; }
                    else { acted = true; bj.Stand(); }
                }
                if (view.LandedPlayer.Length > 0) hands = $"player '{view.LandedPlayer}', dealer '{view.LandedDealer}'";
                if (started && t > 3f && bj.IsIdle) break;
            }
            log.AppendLine($"round {round + 1}: frames {frame}, {hands}");
        }
        File.WriteAllText(OutDir + "/bj_done.txt", log.ToString());
        Debug.Log("[Casino] blackjack frames written");
        Object.Destroy(runner.gameObject);
    }

    // ================================================================ 重さ：スロットの画面・ランプを切ったときとの比べ
    [MenuItem("Roomies/Casino/Demo/Perf - Slot Screen Cost (Play Mode)")]
    static void PerfSlot()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Casino] Play Mode で実行してください"); return; }
        var runner = NewRunner();
        runner.StartCoroutine(PerfRoutine(runner));
    }

    static IEnumerator PerfRoutine(Runner runner)
    {
        Directory.CreateDirectory(OutDir);
        File.Delete(OutDir + "/perf_done.txt");
        var cc = MyController();
        Teleport(cc, new Vector3(8f, .1f, -5.5f));
        cc.transform.rotation = Quaternion.LookRotation(Vector3.back);
        for (int w = 0; w < 40; w++) yield return null;

        var slots = Object.FindObjectsByType<SlotMachine>();
        var canvases = slots.SelectMany(s => s.GetComponentsInChildren<Canvas>(true)).ToArray();
        var lamps = slots.Select(s => s.transform.Find("Anchor_Lamps")).Where(t => t != null).Select(t => t.gameObject).ToArray();
        var views = Object.FindObjectsByType<BlackjackTableView>();
        string[] modes = { "all", "no_slot_canvas", "no_slot_lamps", "no_both" };
        var res = modes.ToDictionary(m => m, m => (ft: new System.Collections.Generic.List<float>(), dc: new System.Collections.Generic.List<float>()));
        for (int round = 0; round < 5; round++)
            foreach (string m in modes)
            {
                foreach (var c in canvases) c.enabled = m != "no_slot_canvas" && m != "no_both";
                foreach (var l in lamps) l.SetActive(m != "no_slot_lamps" && m != "no_both");
                for (int w = 0; w < 10; w++) yield return null;
                for (int f = 0; f < 60; f++)
                {
                    yield return null;
                    res[m].ft.Add(UnityStats.renderTime * 1000f);
                    res[m].dc.Add(UnityStats.drawCalls);
                }
            }
        foreach (var c in canvases) c.enabled = true;
        foreach (var l in lamps) l.SetActive(true);
        var sb = new System.Text.StringBuilder();
        float Med(System.Collections.Generic.List<float> l) { var o = l.OrderBy(x => x).ToList(); return o[o.Count / 2]; }
        foreach (string m in modes) sb.AppendLine($"{m,-16} render med {Med(res[m].ft):F2} ms  draws {Med(res[m].dc):F0}");
        File.WriteAllText(OutDir + "/perf_done.txt", sb.ToString());
        Object.Destroy(runner.gameObject);
    }
}
#endif
