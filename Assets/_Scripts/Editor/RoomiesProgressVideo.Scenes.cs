#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 制作進捗の動画の場面（2026/11/24）。Roomies/Video/Record Progress (Play Mode)。
/// 撮る場面は Temp/RoomiesVideo/scenes.txt（1 行 1 つ。無ければ全部）。撮り直したい場面だけを書けば、その場面だけ撮る。
/// 場面ごとに Temp/RoomiesVideo/clips/〇〇.mp4（映像）と .f32（音）。字幕・つなぎは ArtSource の外（Leica の作業フォルダ）の Python でやる。
/// </summary>
internal static partial class RoomiesCasinoDemo
{
    static readonly string[] AllScenes =
    {
        "c1_casino_day", "c1_casino_night", "c2_casino_hall", "c2_clerk", "c2_elevator", "c2_upstairs", "c2_police",
        "c3_slot", "c4_blackjack", "c4_hilo", "c5_store_day", "c5_store_night", "c6_store_shop", "c7_store_job",
    };

    [MenuItem("Roomies/Video/Record Progress (Play Mode)")]
    static void VideoRecord()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Video] Play Mode で実行してください"); return; }
        var runner = NewRunner();
        runner.StartCoroutine(VideoRecordRoutine(runner));
    }

    static IEnumerator VideoRecordRoutine(Runner runner)
    {
        Directory.CreateDirectory(VideoDir);
        File.Delete(VideoDir + "/record_done.txt");
        var want = File.Exists(VideoDir + "/scenes.txt")
            ? File.ReadAllLines(VideoDir + "/scenes.txt").Select(l => l.Trim()).Where(l => l.Length > 0).ToHashSet()
            : AllScenes.ToHashSet();
        int prevSize = SetGameViewSize(VW, VH);
        bool asyncBefore = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        var r = MakeRig(runner);
        var log = new System.Text.StringBuilder();
        var balance = GameBalance.Current;
        int customersBefore = balance.storeJobCustomers;
        // 「今日のおねがい」の達成の知らせが途中で出ないように、達成済みにしておく
        if (DailyRequestManager.Instance != null)
            ((NetworkVariable<bool>)typeof(DailyRequestManager).GetField("done", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(DailyRequestManager.Instance)).Value = true;
        var money = SharedMoneyManager.Instance;
        if (money.CurrentMoney < 20000) money.TryAdd(20000, SharedMoneyReason.DebugGrant, "progress video");
        for (int i = 0; i < 20; i++) yield return null;

        foreach (string scene in AllScenes)
        {
            if (!want.Contains(scene)) continue;
            log.AppendLine("== " + scene);
            IEnumerator routine = null;
            switch (scene)
            {
                case "c1_casino_day": routine = CasinoOutside(r, false); break;
                case "c1_casino_night": routine = CasinoOutside(r, true); break;
                case "c2_casino_hall": routine = CasinoHall(r); break;
                case "c2_clerk": routine = CasinoClerkTalk(r); break;
                case "c2_elevator": routine = CasinoElevatorRide(r, log); break;
                case "c2_upstairs": routine = CasinoUpstairs(r, log); break;
                case "c2_police": routine = PoliceTalkShot(r, log); break;
                case "c3_slot": routine = SlotShot(r); break;
                case "c4_blackjack": routine = BlackjackShot(r, log); break;
                case "c4_hilo": routine = HiLoShot(r, log); break;
                case "c5_store_day": routine = StoreOutside(r, false); break;
                case "c5_store_night": routine = StoreOutside(r, true); break;
                case "c6_store_shop": routine = StoreShopping(r, log); break;
                case "c7_store_job": routine = StoreJob(r, log, balance); break;
            }
            yield return routine;
            if (r.rec.Active) r.rec.End();
            UseCine(r, false);
        }
        balance.storeJobCustomers = customersBefore;
        ShaderUtil.allowAsyncCompilation = asyncBefore;
        RestoreGameViewSize(prevSize);
        File.WriteAllText(VideoDir + "/record_done.txt", r.rec.Log + "\n" + log);
        Object.Destroy(runner.gameObject);
    }

    // ---------------------------------------------------------------- 道具
    static CasinoElevatorButton ElevatorButton(CasinoElevator ev, int cabinFloor, CasinoElevatorButton.Kind kind, int index)
    {
        var floor = ev.transform.Find($"Floor_{cabinFloor + 1}F");
        foreach (var b in floor.GetComponentsInChildren<CasinoElevatorButton>(true))
        {
            var so = new SerializedObject(b);
            if (so.FindProperty("kind").enumValueIndex == (int)kind && (kind == CasinoElevatorButton.Kind.KeySlot || so.FindProperty("floor").intValue == index)) return b;
        }
        return null;
    }

    static IEnumerator Settle(int frames = 20) { for (int i = 0; i < frames; i++) yield return null; }

    static IEnumerator WaitElevator(CasinoElevator ev)
    {
        yield return Hold(1f);
        for (float t = 0; t < 12f && !ev.IsIdle; t += Time.deltaTime) yield return null;
    }

    // ================================================================ 1. カジノの外観
    static IEnumerator CasinoOutside(Rig r, bool night)
    {
        SetTime(night ? 1 : 0);
        Teleport(r.cc, new Vector3(-6f, .05f, 4f));      // プレイヤーは画面の外
        yield return Settle(40);
        UseCine(r, true);
        r.rec.Begin(night ? "c1_casino_night" : "c1_casino_day");
        if (!night)
            yield return Move(r, new Vector3(7.8f, 1.8f, 7.5f), new Vector3(7.8f, 7.2f, -8f), new Vector3(7.8f, 2.4f, 1.0f), new Vector3(7.8f, 8.2f, -8f), 6.5f, 62);
        else
            yield return Move(r, new Vector3(15.5f, 1.5f, 1.5f), new Vector3(7.8f, 6.8f, -8f), new Vector3(1.0f, 2.2f, 1.0f), new Vector3(7.8f, 7.4f, -8f), 6.5f, 60);
        r.rec.End();
    }

    // ================================================================ 2. カジノの中
    static IEnumerator CasinoHall(Rig r)
    {
        SetTime(0);
        Teleport(r.cc, new Vector3(-6f, .05f, 4f));
        yield return Settle(30);
        UseCine(r, true);
        r.rec.Begin("c2_casino_hall");
        // 入口から入って、1 階の中を見回す（丸みのある家具・台・明かり）
        yield return Move(r, new Vector3(7.8f, 1.75f, -6.2f), new Vector3(7.8f, 1.4f, -14f), new Vector3(7.0f, 1.7f, -10.2f), new Vector3(2.0f, 1.1f, -15.5f), 3.5f, 62);
        yield return Move(r, new Vector3(7.0f, 1.7f, -10.2f), new Vector3(2.0f, 1.1f, -15.5f), new Vector3(9.5f, 1.8f, -10.6f), new Vector3(15.0f, 1.1f, -16.5f), 3.5f, 62);
        r.rec.End();
    }

    static CasinoClerk ClerkFor(int sellsFloor) => Object.FindObjectsByType<CasinoClerk>().FirstOrDefault(c => c.SellsFloor == sellsFloor);

    static IEnumerator CasinoClerkTalk(Rig r)
    {
        SetTime(0);
        var clerk = ClerkFor(2);
        Vector3 feet = clerk.transform.position + clerk.transform.forward * 1.8f; feet.y = .05f;
        Teleport(r.cc, feet);
        yield return Settle(20);
        Aim(r, clerk.transform.position + Vector3.up * 1.3f);
        UseCine(r, true);
        Vector3 eye = clerk.transform.position + clerk.transform.forward * 3.0f + clerk.transform.right * 1.1f + Vector3.up * 1.75f;
        Vector3 look = clerk.transform.position + Vector3.up * 1.55f - clerk.transform.right * .55f;
        Teleport(r.cc, clerk.transform.position + clerk.transform.forward * 1.6f - clerk.transform.right * 1.5f - Vector3.up * clerk.transform.position.y + Vector3.up * .05f);   // keep the player behind the camera
        yield return Settle(10);
        Aim(r, clerk.transform.position + Vector3.up * 1.3f);   // the player looks at the clerk (not at a slot, whose prompt would float in the shot)
        r.rec.Begin("c2_clerk");
        yield return Move(r, eye, look, eye + clerk.transform.forward * -0.2f, look, .8f, 58);
        clerk.Interact();                 // お金があれば、2 階の鍵を買う（店の人が話す）
        yield return Move(r, eye + clerk.transform.forward * -0.2f, look, eye + clerk.transform.forward * -0.35f, look, 3.2f, 58);
        clerk.Interact();                 // 世間話
        yield return Move(r, eye + clerk.transform.forward * -0.35f, look, eye + clerk.transform.forward * -0.45f, look, 3.0f, 58);
        r.rec.End();
    }

    static IEnumerator CasinoElevatorRide(Rig r, System.Text.StringBuilder log)
    {
        SetTime(0);
        var ev = Object.FindAnyObjectByType<CasinoElevator>();
        var access = CasinoAccess.Instance;
        if (access.UnlockedFloor < 2 && !access.HasUnusedKey) { ClerkFor(2).Interact(); yield return Hold(.5f); }
        Transform cabin0 = ev.transform.Find("Floor_1F/Anchor_Cabin"), cabin1 = ev.transform.Find("Floor_2F/Anchor_Cabin");
        // 箱の前（ホール）から歩いて乗る
        Teleport(r.cc, new Vector3(cabin0.position.x, .05f, cabin0.position.z + 2.6f));
        yield return Settle(20);
        var key0 = ElevatorButton(ev, 0, CasinoElevatorButton.Kind.KeySlot, 0);
        var go2 = ElevatorButton(ev, 0, CasinoElevatorButton.Kind.Go, 1);
        Aim(r, cabin0.position + Vector3.up * 1.4f);
        UseCine(r, false);
        r.rec.Begin("c2_elevator");
        yield return WalkTo(r, new Vector3(cabin0.position.x, 0, cabin0.position.z), 2.2f, cabin0.position + Vector3.up * 1.3f - Vector3.forward * 2f);
        yield return AimOver(r, cabin0.position + Vector3.up * 1.3f - Vector3.forward * 2f, key0.transform.position, .6f);
        yield return Hold(.5f);
        if (key0.CanInteract()) { key0.Interact(); yield return Hold(.7f); }
        yield return AimOver(r, key0.transform.position, go2.transform.position, .4f);
        yield return Hold(.5f);
        go2.Interact();
        yield return AimOver(r, go2.transform.position, cabin0.position + Vector3.up * 1.4f + Vector3.forward * 3f, .8f);
        yield return WaitElevator(ev);
        log.AppendLine($"   elevator at floor {ev.CarFloor}");
        yield return Hold(.6f);
        yield return WalkTo(r, new Vector3(cabin1.position.x, 0, cabin1.position.z + 3.2f), 2.2f);
        yield return Hold(.6f);
        r.rec.End();
    }

    static IEnumerator CasinoUpstairs(Rig r, System.Text.StringBuilder log)
    {
        SetTime(0);
        var ev = Object.FindAnyObjectByType<CasinoElevator>();
        var access = CasinoAccess.Instance;
        Transform cabin1 = ev.transform.Find("Floor_2F/Anchor_Cabin"), cabin2 = ev.transform.Find("Floor_3F/Anchor_Cabin");
        // 2 階（エレベーターで来た所）
        Vector3 f2 = new Vector3(cabin1.position.x, cabin1.position.y, cabin1.position.z + 3.2f);
        Teleport(r.cc, new Vector3(cabin1.position.x, cabin1.position.y, cabin1.position.z + 1.0f));
        yield return Settle(30);
        UseCine(r, true);
        r.rec.Begin("c2_upstairs_2f");
        yield return Move(r, f2 + new Vector3(-.6f, 1.75f, 0), f2 + new Vector3(-4f, 1.0f, 4f), f2 + new Vector3(.8f, 1.8f, .8f), f2 + new Vector3(5f, 1.0f, 4.5f), 4.5f, 64);
        r.rec.End();
        // 3 階：鍵を買って上がる（撮らない）
        if (access.UnlockedFloor < 3)
        {
            if (!access.HasUnusedKey) { var c3 = ClerkFor(3); Teleport(r.cc, c3.transform.position + c3.transform.forward * 1.75f); yield return Settle(10); c3.Interact(); yield return Hold(.5f); }
            Teleport(r.cc, cabin1.position);
            yield return Settle(10);
            if (ev.CarFloor != 1) { ElevatorButton(ev, 1, CasinoElevatorButton.Kind.Call, 1)?.Interact(); yield return WaitElevator(ev); Teleport(r.cc, cabin1.position); yield return Settle(10); }
            var key1 = ElevatorButton(ev, 1, CasinoElevatorButton.Kind.KeySlot, 0);
            if (key1.CanInteract()) { key1.Interact(); yield return Hold(.5f); }
            ElevatorButton(ev, 1, CasinoElevatorButton.Kind.Go, 2).Interact();
            yield return WaitElevator(ev);
        }
        log.AppendLine($"   unlocked floor {access.UnlockedFloor}, car {ev.CarFloor}");
        Vector3 f3 = new Vector3(cabin2.position.x, cabin2.position.y, cabin2.position.z + 3.2f);
        Teleport(r.cc, new Vector3(cabin2.position.x, cabin2.position.y, cabin2.position.z + 1.0f));
        yield return Settle(30);
        r.rec.Begin("c2_upstairs_3f");
        yield return Move(r, f3 + new Vector3(.6f, 1.75f, 0), f3 + new Vector3(4f, 1.0f, 4.5f), f3 + new Vector3(-.8f, 1.8f, .8f), f3 + new Vector3(-4f, 1.0f, 4.5f), 4.5f, 64);
        r.rec.End();
        // 1 階へ戻しておく（撮らない）
        Teleport(r.cc, cabin2.position);
        yield return Settle(10);
        ElevatorButton(ev, 2, CasinoElevatorButton.Kind.Go, 0)?.Interact();
        yield return WaitElevator(ev);
    }

    static IEnumerator PoliceTalkShot(Rig r, System.Text.StringBuilder log)
    {
        SetTime(1);
        yield return Hold(1.5f);
        SmugglingPolicePatrol officer = null;
        for (float w = 0; officer == null && w < 8f; w += .2f)
        {
            officer = Object.FindObjectsByType<SmugglingPolicePatrol>().FirstOrDefault(o => o.IsOnDuty);
            yield return Hold(.2f);
        }
        if (officer == null) { log.AppendLine("   no officer on duty"); yield break; }
        var talk = officer.GetComponent<PoliceTalk>();
        Vector3 p0 = officer.transform.position;
        Vector3 feet = p0 + officer.transform.right * 2.2f; feet.y = .05f;
        Teleport(r.cc, feet);
        yield return Hold(1.2f);   // 立ち止まってこちらを向くまで
        Vector3 toMe = r.cc.transform.position - officer.transform.position; toMe.y = 0; toMe.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, toMe);
        Vector3 mid = (r.cc.transform.position + officer.transform.position) * .5f; mid.y = officer.transform.position.y;
        Vector3 eye = mid + side * 3.0f + Vector3.up * 1.8f;
        Vector3 look = officer.transform.position + Vector3.up * 1.75f + toMe * .5f;
        UseCine(r, true);
        r.rec.Begin("c2_police");
        yield return Move(r, eye, look, eye - side * .3f, look, 1.0f, 52);
        talk.Interact();
        yield return Move(r, eye - side * .3f, look, eye - side * .7f, look, 3.4f, 52);
        talk.Interact();
        yield return Move(r, eye - side * .7f, look, eye - side * 1.0f, look, 3.0f, 52);
        r.rec.End();
        SetTime(0);
    }

    // ================================================================ 3. スロット
    static IEnumerator SlotShot(Rig r)
    {
        SetTime(0);
        var slot = Object.FindObjectsByType<SlotMachine>().OrderBy(s => s.transform.position.y).ThenBy(s => s.name).First();
        Teleport(r.cc, slot.transform.position + slot.transform.forward * 2.2f + slot.transform.right * 1.4f);
        yield return Settle(30);
        Vector3 screen = slot.transform.TransformPoint(new Vector3(0, 1.3f, .37f));
        MethodInfo spin = typeof(SlotMachine).GetMethod("SpinRoutine", BindingFlags.Instance | BindingFlags.NonPublic);
        var screenComp = slot.GetComponent<SlotScreen>();
        var cases = new (int a, int b, int c, string message, int reward, int tier, float seconds, bool fever)[]
        {
            (0, 1, 2, "MISS", 0, 0, 2.3f, false),
            (3, 3, 1, "MISS", 0, 0, 4.0f, false),
            (3, 3, 3, "WIN +50", 50, 3, 5.6f, false),
            (4, 4, 4, "FEVER +100", 100, 4, 7.2f, false),
            (2, 2, 2, "FEVER WIN +60", 60, 2, 4.8f, true),
        };
        UseCine(r, true);
        Vector3 far = screen + slot.transform.forward * 2.3f + Vector3.up * .1f, near = screen + slot.transform.forward * 1.5f + Vector3.up * .04f;
        Place(r, far, screen - Vector3.up * .25f, 52);
        r.rec.Begin("c3_slot");
        float total = cases.Sum(k => k.seconds + .3f), elapsed = 0;
        foreach (var k in cases)
        {
            screenComp.SetFever(k.fever);
            slot.StartCoroutine((IEnumerator)spin.Invoke(slot, new object[] { k.a, k.b, k.c, k.message, k.reward, k.tier }));
            float dur = k.seconds + .3f;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed + t) / total));
                Place(r, Vector3.Lerp(far, near, u), Vector3.Lerp(screen - Vector3.up * .25f, screen, u), 52);
                yield return null;
            }
            elapsed += dur;
        }
        r.rec.End();
        screenComp.SetFever(false);
    }

    // ================================================================ 4. ブラックジャック・ハイ＆ロー
    static IEnumerator BlackjackShot(Rig r, System.Text.StringBuilder log)
    {
        SetTime(0);
        var bj = Object.FindObjectsByType<BlackjackTable>().OrderBy(b => b.transform.position.y).First();
        var view = bj.GetComponent<BlackjackTableView>();
        Teleport(r.cc, bj.transform.position + bj.transform.forward * 1.9f + bj.transform.right * 1.3f);
        yield return Settle(20);
        var earning = r.cc.GetComponentInParent<PlayerEarning>();
        ulong me = r.cc.GetComponentInParent<NetworkObject>().OwnerClientId;
        for (float w = 0; !bj.IsIdle && w < 12f; w += Time.deltaTime) yield return null;
        Vector3 eye = bj.transform.TransformPoint(new Vector3(.2f, 1.5f, 1.15f)), look = bj.transform.TransformPoint(new Vector3(0, .93f, .15f));
        UseCine(r, true);
        Place(r, eye, look, 56);
        r.rec.Begin("c4_blackjack");
        bool started = false, acted = false; int hits = 1; float nextAct = 2.4f;
        for (float t = 0; t < 13f; t += Time.deltaTime)
        {
            Place(r, Vector3.Lerp(eye, bj.transform.TransformPoint(new Vector3(-.15f, 1.42f, 1.0f)), Mathf.SmoothStep(0, 1, t / 13f)), look, 54);
            if (!started && t > .4f) { started = true; bj.Interact(earning); }
            if (started && !acted && t > nextAct && bj.CanLocalPlayerStand(me) && !view.IsBusy)
            {
                if (hits > 0 && view.LandedPlayer.Length < 3) { hits--; bj.Interact(earning); nextAct = t + 1.3f; }
                else { acted = true; bj.Stand(); }
            }
            if (started && acted && t > 4f && bj.IsIdle) { yield return Hold(.8f); break; }
            yield return null;
        }
        log.AppendLine($"   blackjack player '{view.LandedPlayer}' dealer '{view.LandedDealer}'");
        r.rec.End();
    }

    static IEnumerator HiLoShot(Rig r, System.Text.StringBuilder log)
    {
        SetTime(0);
        var hilo = Object.FindAnyObjectByType<HiLoTable>();
        var view = hilo.View;
        Teleport(r.cc, hilo.transform.TransformPoint(new Vector3(.9f, 0, 1.5f)));
        yield return Settle(20);
        var earning = r.cc.GetComponentInParent<PlayerEarning>();
        ulong me = r.cc.GetComponentInParent<NetworkObject>().OwnerClientId;
        for (float w = 0; (!hilo.IsIdle || view.IsBusy) && w < 12f; w += Time.deltaTime) yield return null;
        Vector3 eye = hilo.transform.TransformPoint(new Vector3(-.2f, 1.64f, 1.15f)), look = hilo.transform.TransformPoint(new Vector3(0, 1.1f, -.05f));
        UseCine(r, true);
        Place(r, eye, look, 56);
        r.rec.Begin("c4_hilo");
        bool started = false, collected = false; int guesses = 0; float nextAct = 1.6f;
        for (float t = 0; t < 14f; t += Time.deltaTime)
        {
            Place(r, Vector3.Lerp(eye, hilo.transform.TransformPoint(new Vector3(.25f, 1.6f, 1.05f)), Mathf.SmoothStep(0, 1, t / 14f)), look, 56);
            if (!started && t > .4f) { started = true; hilo.Interact(null, earning); }
            if (started && t > nextAct && hilo.IsGuessing(me) && !view.IsBusy)
            {
                int card = hilo.Card;
                if (guesses < 2)
                {
                    bool high = card <= 7;
                    if (HiLoRules.Multiplier(card, high, hilo.StepReturnRate) <= 0f) high = !high;
                    if (high) hilo.Interact(null, earning); else hilo.Secondary(null, earning);
                    guesses++; nextAct = t + 2.2f;
                }
                else if (!collected && hilo.Step > 0) { collected = true; hilo.Tertiary(null, earning); }
            }
            if (started && t > 2f && hilo.IsIdle && !view.IsBusy) { yield return Hold(1.0f); break; }
            yield return null;
        }
        log.AppendLine($"   hilo guesses {guesses}, collected {collected}, cards '{view.Landed}'");
        r.rec.End();
    }

    // ================================================================ 5. コンビニの外観
    static IEnumerator StoreOutside(Rig r, bool night)
    {
        SetTime(night ? 1 : 0);
        Teleport(r.cc, new Vector3(36f, .05f, 2f));
        yield return Settle(40);
        UseCine(r, true);
        r.rec.Begin(night ? "c5_store_night" : "c5_store_day");
        if (!night)
            yield return Move(r, new Vector3(20.5f, 1.7f, -1.2f), new Vector3(24.5f, 2.2f, -9f), new Vector3(31.5f, 1.8f, -1.6f), new Vector3(27.5f, 2.2f, -9f), 6f, 60);
        else
            yield return Move(r, new Vector3(26.1f, 1.5f, -1.0f), new Vector3(26.1f, 2.4f, -9f), new Vector3(26.4f, 1.6f, -5.2f), new Vector3(26.1f, 2.0f, -12f), 6f, 60);
        r.rec.End();
    }

    // ================================================================ 6. コンビニの買い物（夜。プレイヤーの目で）
    static IEnumerator StoreShopping(Rig r, System.Text.StringBuilder log)
    {
        SetTime(1);
        var shop = Object.FindAnyObjectByType<StoreShop>();
        var fridge = NightFridge.Instance;
        if (fridge.IsStocked) fridge.ServerConsumeStock();
        var broken = (NetworkVariable<bool>)typeof(NightFridge).GetField("broken", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fridge);
        broken.Value = false;
        var earning = r.cc.GetComponentInParent<PlayerEarning>();
        Vector3 W(float x, float y, float z) => shop.transform.TransformPoint(new Vector3(x, y, z));
        int Find(string tail) { for (int i = 0; i < shop.SectionCount; i++) if (shop.SectionCollider(i).name.EndsWith(tail)) return i; return -1; }

        // 入店：外から歩いて入る（自動ドアが開いて、いらっしゃいませ）
        Teleport(r.cc, new Vector3(26.1f, .05f, -4.6f));
        yield return Settle(40);
        Aim(r, new Vector3(26.1f, 1.5f, -12f));
        UseCine(r, false);
        r.rec.Begin("c6_store_enter");
        yield return Hold(.4f);
        yield return WalkTo(r, new Vector3(26.1f, 0, -11.2f), 2.4f, new Vector3(26.1f, 1.45f, -16f));
        yield return AimOver(r, new Vector3(26.1f, 1.45f, -16f), W(5.0f, 1.5f, 1.2f), 1.2f);
        yield return Hold(1.2f);
        r.rec.End();

        // 棚から 3 つ
        Teleport(r.cc, W(-5.7f, .1f, 2.4f));
        yield return Settle(20);
        int sOni = Find("Onigiri"), sDrink = Find("Drinks"), sSnack = Find("Snacks");
        Vector3 Sec(int s) => shop.SectionCollider(s).bounds.center;
        Aim(r, Sec(sOni));
        r.rec.Begin("c6_store_pick");
        yield return Hold(.8f);
        shop.Interact(shop.SectionCollider(sOni), earning);
        yield return Hold(.9f);
        yield return AimOver(r, Sec(sOni), Sec(sSnack), .7f);
        yield return Hold(.4f);
        shop.Interact(shop.SectionCollider(sSnack), earning);
        yield return Hold(.9f);
        yield return AimOver(r, Sec(sSnack), Sec(sDrink), .7f);
        yield return Hold(.4f);
        shop.Interact(shop.SectionCollider(sDrink), earning);
        yield return Hold(1.1f);
        r.rec.End();

        // レジ
        Teleport(r.cc, W(4.6f, .1f, 2.85f));
        yield return Settle(20);
        Vector3 reg = W(5.3f, 1.05f, 1.35f);
        Aim(r, reg);
        r.rec.Begin("c6_store_register");
        yield return Hold(1.0f);
        shop.Interact(shop.RegisterCollider, earning);
        for (float t = 0; t < 7f && shop.CurrentPhase != StoreShop.Phase.Pay; t += Time.deltaTime) yield return null;
        yield return Hold(1.0f);
        shop.Interact(shop.RegisterCollider, earning);
        yield return Hold(1.6f);
        shop.Interact(shop.RegisterCollider, earning);
        yield return Hold(1.6f);
        r.rec.End();
        log.AppendLine($"   shop phase {shop.CurrentPhase}, fridge stocked {fridge.IsStocked}");
    }

    // ================================================================ 7. レジ打ちのバイト（朝。プレイヤーの目で）
    static IEnumerator StoreJob(Rig r, System.Text.StringBuilder log, GameBalance balance)
    {
        SetTime(0);
        balance.storeJobCustomers = 2;   // 動画のあいだだけ 2 人（終わったら戻す）
        var shop = Object.FindAnyObjectByType<StoreShop>();
        var earning = r.cc.GetComponentInParent<PlayerEarning>();
        Vector3 W(float x, float y, float z) => shop.transform.TransformPoint(new Vector3(x, y, z));
        Teleport(r.cc, W(1.6f, .1f, 1.0f));
        yield return Settle(30);
        Vector3 card = shop.TimeCardCollider.bounds.center;
        Aim(r, card + new Vector3(0, .25f, 0));
        UseCine(r, false);
        r.rec.Begin("c7_store_job");
        yield return AimOver(r, card + new Vector3(0, .35f, 0), card, .6f);
        yield return Hold(1.0f);
        shop.Interact(shop.TimeCardCollider, earning);
        yield return Hold(.5f);
        Vector3 counter = W(5.62f, 1.15f, 2.3f);
        Aim(r, counter);
        int served = 0;
        for (float t = 0; t < 70f; t += Time.deltaTime)
        {
            Aim(r, counter + new Vector3(Mathf.Sin(t * .6f) * .08f, 0, 0));
            var ph = shop.CurrentJobPhase;
            if (ph == StoreShop.JobPhase.Scan)
            {
                yield return Hold(.5f);
                while (shop.CurrentJobPhase == StoreShop.JobPhase.Scan) { shop.Interact(shop.TillCollider, earning); yield return Hold(.5f); }
                continue;
            }
            if (ph == StoreShop.JobPhase.Change)
            {
                yield return Hold(1.0f);
                int correct = shop.JobPaid - shop.JobTotal;
                for (int k = 0; k < 4 && shop.SelectedChange != correct; k++) { shop.ChangeBet(1); yield return Hold(.45f); }
                yield return Hold(.4f);
                shop.Interact(shop.TillCollider, earning);
                yield return Hold(.9f);
                shop.Interact(shop.TillCollider, earning);
                served++;
                continue;
            }
            if (ph == StoreShop.JobPhase.Off && served > 0) { yield return Hold(2.2f); break; }
            yield return null;
        }
        r.rec.End();
        log.AppendLine($"   job served {served}, earned {shop.JobEarned}, mistakes {shop.JobMistakes}");
    }
}
#endif
