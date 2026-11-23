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

    // ================================================================ NPC の会話（店の人・警察）
    /// <summary>
    /// 店の人と警察に話しかけて、吹き出しが出るか・警察が立ち止まるか・悪いことをしたときの流れが残っているかを確かめ、写真を撮る。
    /// 夜にするために、日の進みをその場で夜へ切り替える（確認のときはセーブの控えを取ること）。結果は Temp/RoomiesCasino/npc_check.txt。
    /// </summary>
    [MenuItem("Roomies/Casino/Demo/NPC Talk Check (Play Mode)")]
    static void NpcCheck()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Casino] Play Mode で実行してください"); return; }
        var runner = NewRunner();
        runner.StartCoroutine(NpcRoutine(runner));
    }

    static IEnumerator NpcRoutine(Runner runner)
    {
        Directory.CreateDirectory(OutDir);
        if (Directory.Exists(OutDir + "/npc")) Directory.Delete(OutDir + "/npc", true);
        File.Delete("Temp/RoomiesCasino/npc_check.txt");
        var log = new System.Text.StringBuilder();
        int ok = 0, ng = 0;
        void Expect(bool good, string what) { if (good) ok++; else ng++; log.AppendLine((good ? "OK  " : "NG  ") + what); }
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var cc = MyController();
        Vector3 home = cc.transform.position;
        // 撮る間は、シェーダーの用意を待ってから描く（待たないと、用意の済んでいない物が水色のべた塗りで写る）
        bool asyncBefore = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;

        // ---- 入口の右の配達の箱の値札：遊んでいる人の目からは、正しい向きで読める（値札は、見ている人のほうを向く）
        var box = Object.FindObjectsByType<DeliveryItem>().OrderBy(d => (d.transform.position - new Vector3(13f, 0, -7f)).sqrMagnitude).FirstOrDefault();
        if (box != null)
        {
            log.AppendLine($"    delivery box '{box.name}' at {box.transform.position:F1}");
            Vector3 stand = box.transform.position + Vector3.forward * 2.4f; stand.y = .05f;
            Teleport(cc, stand);
            cc.transform.rotation = Quaternion.LookRotation(Vector3.back);
            yield return new WaitForSeconds(1.0f);
            Camera bcam = NewCamera(runner.transform, stand + Vector3.up * 1.62f + Vector3.back * .4f, box.transform.position + Vector3.up * .9f, 55);
            yield return new WaitForEndOfFrame();
            Shoot(bcam, "npc", "price_tag_in_game");
            Object.Destroy(bcam.gameObject);
            var tag = box.GetComponentInChildren<TMPro.TMP_Text>(true);
            Vector3 toEye = stand + Vector3.up * 1.62f - tag.transform.position; toEye.y = 0;
            // 文字の表は、文字の -Z の側。それが見ている人のほうを向いていれば、裏返しには見えない
            Expect(tag != null && tag.enabled && Vector3.Dot(-tag.transform.forward, toEye.normalized) > .7f, $"delivery box price tag '{tag.text}' faces the player (not mirrored)");
        }
        else log.AppendLine("    (no delivery box in the scene)");

        // ---- 店の人（3 階：鍵を売らず、話すだけ）
        foreach (var clerk in Object.FindObjectsByType<CasinoClerk>().OrderBy(c => c.transform.position.y))
        {
            int floor = clerk.transform.position.y > RoomiesTownRebuild.KF3 ? 3 : clerk.transform.position.y > RoomiesTownRebuild.KF2 ? 2 : 1;
            var bubble = clerk.GetComponent<NpcSpeechBubble>();
            Vector3 feet = clerk.transform.position + clerk.transform.forward * 1.9f; feet.y = RoomiesTownRebuild.KFloorY(floor);
            Teleport(cc, feet);
            cc.transform.rotation = Quaternion.LookRotation(-clerk.transform.forward);
            yield return new WaitForSeconds(.8f);
            Expect(bubble != null && !bubble.IsShowing, $"clerk {floor}F: no bubble before talking");
            string label = clerk.Label;
            var lines = new System.Collections.Generic.HashSet<string>();
            for (int k = 0; k < 4; k++)
            {
                clerk.Interact();
                yield return new WaitForSeconds(.6f);
                if (bubble != null && bubble.IsShowing) lines.Add(bubble.CurrentLine);
                if (k == 0)
                {
                    Vector3 head = clerk.transform.position + Vector3.up * 1.7f;
                    // 話しかけている人の目の位置から撮る（看板に隠れずに、全文が読めるかを見るため）
                    Camera cam = NewCamera(runner.transform, feet + Vector3.up * 1.62f - clerk.transform.forward * .4f, clerk.transform.position + Vector3.up * 1.4f - clerk.transform.right * .45f, 62);
                    yield return new WaitForEndOfFrame();
                    Shoot(cam, "npc", $"clerk_{floor}f");
                    Object.Destroy(cam.gameObject);
                }
                yield return new WaitForSeconds(.5f);
            }
            Expect(lines.Count >= 1, $"clerk {floor}F ('{label}'): a bubble appears when talked to");
            if (label == "店の人と話す") Expect(lines.Count >= 2, $"clerk {floor}F: the lines vary ({lines.Count} different lines in 4 talks)");
            log.AppendLine("      " + string.Join(" / ", lines));
        }

        // ---- 警察：夜にして、巡回に出ている 1 人を使う
        var dm = DayManager.Instance;
        if (!dm.IsNight) dm.ServerApplySavedState(dm.CurrentDay, 1, 0, 0);
        yield return new WaitForSeconds(1.5f);
        SmugglingPolicePatrol officer = null;
        float wait = 0;
        while (officer == null && wait < 8f)
        {
            officer = Object.FindObjectsByType<SmugglingPolicePatrol>().FirstOrDefault(o => o.IsOnDuty);
            yield return new WaitForSeconds(.2f); wait += .2f;
        }
        Expect(officer != null, "police: an officer is on patrol at night");
        if (officer != null)
        {
            var talk = officer.GetComponent<PoliceTalk>();
            var bubble = officer.GetComponent<NpcSpeechBubble>();
            var stateField = typeof(SmugglingPolicePatrol).GetField("aiState", flags);
            string State() => stateField.GetValue(officer).ToString();
            var smuggler = cc.GetComponentInParent<SmugglingPlayer>();

            // 近づくと、立ち止まってこちらを向く
            Vector3 p0 = officer.transform.position;
            Teleport(cc, new Vector3(p0.x, p0.y, p0.z) + officer.transform.right * 2.2f - Vector3.up * p0.y + Vector3.up * .05f);
            yield return new WaitForSeconds(1.2f);
            Vector3 stop0 = officer.transform.position;
            yield return new WaitForSeconds(1.0f);
            float moved = (officer.transform.position - stop0).magnitude;
            Vector3 toMe = cc.transform.position - officer.transform.position; toMe.y = 0;
            float angle = Vector3.Angle(officer.transform.forward, toMe);
            Expect(State() == "Greet" && moved < .05f, $"police: stops when an innocent player comes close (state {State()}, moved {moved:F2}m)");
            Expect(angle < 25f, $"police: turns to face the player (angle {angle:F0})");
            Expect(talk != null && talk.CanInteract() && talk.Label == "警察と話す", "police: can be talked to");

            // 話しかけている人の肩ごしに見る（吹き出しは、その人のカメラのほうを向く）
            Vector3 eye = cc.transform.position + Vector3.up * .75f + toMe.normalized * 1.1f + Vector3.Cross(Vector3.up, toMe.normalized) * .55f;
            Camera pcam = NewCamera(runner.transform, eye, officer.transform.position + Vector3.up * 1.75f, 60);
            yield return new WaitForEndOfFrame();
            Shoot(pcam, "npc", "police_1_stopped");

            var lines = new System.Collections.Generic.HashSet<string>();
            for (int k = 0; k < 4 && talk != null; k++)
            {
                talk.Interact();
                yield return new WaitForSeconds(.6f);
                if (bubble.IsShowing) lines.Add(bubble.CurrentLine);
                if (k == 0) { yield return new WaitForEndOfFrame(); Shoot(pcam, "npc", "police_2_talking"); }
                yield return new WaitForSeconds(.4f);
            }
            Expect(lines.Count >= 2, $"police: says different random lines ({lines.Count} different lines in 4 talks)");
            log.AppendLine("      " + string.Join(" / ", lines));
            Expect(smuggler != null && !smuggler.IsJailed && !smuggler.HasBag, "police: talking does not arrest an innocent player");

            // 離れると、巡回に戻る
            Teleport(cc, home);
            yield return new WaitForSeconds(1.0f);
            Vector3 q0 = officer.transform.position;
            yield return new WaitForSeconds(2.0f);
            Expect(State() != "Greet" && (officer.transform.position - q0).magnitude > .3f, $"police: goes back to patrol when the player leaves (state {State()}, moved {(officer.transform.position - q0).magnitude:F2}m)");

            // 悪いこと（紙袋を運ぶ）をしているときの流れは、前と同じ：見つけて追いかけ、捕まえる
            if (smuggler != null)
            {
                var jobState = typeof(SmugglingPlayer).GetField("state", flags).GetValue(smuggler);
                var valueProp = jobState.GetType().GetProperty("Value");
                valueProp.SetValue(jobState, System.Enum.Parse(valueProp.PropertyType, "CarryingBag"));
                yield return new WaitForSeconds(.3f);
                Expect(talk != null && !talk.CanInteract(), "police: no talk prompt while carrying a bag");
                // 懐中電灯の先に立つ（警察は歩き続けるので、見つかるまで 0.3 秒ごとに前へ立ち直す）
                var lightField = typeof(SmugglingPolicePatrol).GetField("flashlight", flags);
                var torch = lightField.GetValue(officer) as Light;
                float t = 0; bool chased = false; float nextPlace = 0;
                while (t < 8f && !smuggler.IsJailed)
                {
                    if (State() == "Surprised" || State() == "Chase") chased = true;
                    if (!chased && t >= nextPlace)
                    {
                        Vector3 dir = torch != null ? torch.transform.forward : officer.transform.forward; dir.y = 0;
                        Vector3 front = officer.transform.position + dir.normalized * 4f; front.y = .05f;
                        Teleport(cc, front);
                        nextPlace = t + .3f;
                    }
                    yield return new WaitForSeconds(.1f); t += .1f;
                }
                log.AppendLine($"      flashlight forward {(torch != null ? torch.transform.forward.ToString("F2") : "none")}, officer forward {officer.transform.forward:F2}");
                Expect(chased || smuggler.IsJailed, $"police: still spots and chases a bag carrier (state {State()}, jailed {smuggler.IsJailed})");
                t = 0; while (t < 14f && !smuggler.IsJailed) { yield return new WaitForSeconds(.2f); t += .2f; }
                Expect(smuggler.IsJailed, "police: still arrests the bag carrier");
            }
            Object.Destroy(pcam.gameObject);
        }

        ShaderUtil.allowAsyncCompilation = asyncBefore;
        File.WriteAllText("Temp/RoomiesCasino/npc_check.txt", $"npc check: ok {ok}, ng {ng}\n" + log);
        Debug.Log("[Casino] npc check written");
        Object.Destroy(runner.gameObject);
    }
}
#endif
