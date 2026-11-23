#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// カジノの通し確認（Play Mode・ホストで GameRoom に入ってから）。結果は Temp/RoomiesCasino/check.txt。
/// 鍵を買う（お金が足りないとき・足りるとき・2 回目）→ 鍵を挿す → 上がる → 歩いて出る → 3 階も同じ → 降りる → 呼ぶ、を実際の操作と同じ呼び方で行う。
/// 確認のために共同口座へお金を足す（DebugGrant）。Play Mode の中だけのこと。
/// </summary>
internal static partial class RoomiesCasinoBuilder
{
    class CheckRunner : MonoBehaviour { }
    static readonly StringBuilder checkLog = new StringBuilder();
    static int checkOk, checkNg;

    [MenuItem("Roomies/Casino/Check (Play Mode)")]
    static void Check()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("[Casino] check works only in Play Mode"); return; }
        var cc = Object.FindObjectsByType<CharacterController>().FirstOrDefault(c => c.GetComponentInParent<PlayerMovement>() != null);
        if (cc == null) { Debug.LogWarning("[Casino] no player"); return; }
        checkLog.Clear(); checkOk = checkNg = 0;
        var runner = new GameObject("Casino Check Runner").AddComponent<CheckRunner>();
        runner.StartCoroutine(CheckRun(cc, runner));
    }

    static void Expect(bool ok, string what)
    {
        if (ok) checkOk++; else checkNg++;
        checkLog.AppendLine((ok ? "OK  " : "NG  ") + what);
    }

    static void Teleport(CharacterController cc, Vector3 feet)
    {
        cc.enabled = false; cc.transform.position = feet + Vector3.up * (cc.height / 2 + .08f); Physics.SyncTransforms(); cc.enabled = true;
    }

    /// <summary>PlayerInteract と同じ見方：目の高さから的へ 3m の線を引き、最初に当たった物が target か。</summary>
    static bool LooksAt(CharacterController cc, HomeAppliance target, Vector3 aim)
    {
        Vector3 eye = cc.transform.position + Vector3.up * .6f;
        Vector3 dir = aim - eye;
        if (dir.magnitude > 3f) return false;
        foreach (var hit in Physics.RaycastAll(eye, dir.normalized, 3f).OrderBy(h => h.distance))
        {
            if (hit.collider.transform.IsChildOf(cc.transform.root)) continue;
            return hit.collider.GetComponentInParent<HomeAppliance>() == target;
        }
        return false;
    }

    static bool Walk(CharacterController cc, Vector3 target)
    {
        for (int i = 0; i < 400; i++)
        {
            var d = target - cc.transform.position; d.y = 0;
            if (d.magnitude < .15f) return true;
            cc.Move(Vector3.ClampMagnitude(d, .06f) + Vector3.down * .05f);
            Physics.SyncTransforms();
        }
        return false;
    }

    static CasinoElevatorButton Button(CasinoElevator ev, int cabinFloor, CasinoElevatorButton.Kind kind, int index)
    {
        var floor = ev.transform.Find($"Floor_{cabinFloor + 1}F");
        foreach (var b in floor.GetComponentsInChildren<CasinoElevatorButton>(true))
        {
            var so = new SerializedObject(b);
            if (so.FindProperty("kind").enumValueIndex == (int)kind && (kind == CasinoElevatorButton.Kind.KeySlot || so.FindProperty("floor").intValue == index)) return b;
        }
        return null;
    }

    static IEnumerator CheckRun(CharacterController cc, MonoBehaviour runner)
    {
        Vector3 home = cc.transform.position;
        var money = SharedMoneyManager.Instance;
        var access = CasinoAccess.Instance;
        var ev = Object.FindAnyObjectByType<CasinoElevator>();
        var vis = Object.FindAnyObjectByType<CasinoFloorVisibility>();
        var clerks = Object.FindObjectsByType<CasinoClerk>().OrderBy(c => c.SellsFloor).ToArray();
        Expect(money != null && access != null && ev != null && vis != null && clerks.Length == 2,
            $"scene has money {money != null}, access {access != null}, elevator {ev != null}, visibility {vis != null}, clerks {clerks.Length}");
        if (money == null || access == null || ev == null || clerks.Length < 2) { Finish(cc, home, runner); yield break; }
        Expect(access.IsSpawned && ev.IsSpawned && clerks.All(c => c.IsSpawned), "network objects spawned");
        Expect(access.KeysBought == 0 && access.UnlockedFloor == 1, $"start: keys {access.KeysBought}, unlocked floor {access.UnlockedFloor}");

        // 「今日のおねがい」の達成ボーナスが途中で入ると、お金の計算が合わなくなる。確認のあいだは「達成済み」にしておく
        if (DailyRequestManager.Instance != null)
            ((Unity.Netcode.NetworkVariable<bool>)typeof(DailyRequestManager)
                .GetField("done", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(DailyRequestManager.Instance)).Value = true;

        Transform cabin0 = ev.transform.Find("Floor_1F/Anchor_Cabin"), cabin1 = ev.transform.Find("Floor_2F/Anchor_Cabin"), cabin2 = ev.transform.Find("Floor_3F/Anchor_Cabin");

        // ---- 1 階の店の人：お金が足りないと買えない
        var clerk1 = clerks[0];
        Vector3 front1 = clerk1.transform.position + clerk1.transform.forward * 1.75f;
        Teleport(cc, front1);
        yield return null;
        Expect(LooksAt(cc, clerk1, clerk1.transform.position + Vector3.up * 1.2f), "1F clerk can be looked at from the counter front");
        int price2 = CasinoAccess.KeyPrice(2), price3 = CasinoAccess.KeyPrice(3);
        if (money.CurrentMoney >= price2) money.TrySpend(money.CurrentMoney - (price2 - 1), SharedMoneyReason.DebugGrant, "casino check");
        int m0 = money.CurrentMoney;
        Expect(clerk1.Label.Contains(price2.ToString("N0")), $"1F clerk label '{clerk1.Label}'");
        clerk1.Interact();
        yield return new WaitForSeconds(.4f);
        Expect(money.CurrentMoney == m0 && access.KeysBought == 0, $"not enough money ({m0}): nothing charged, keys {access.KeysBought}");

        // ---- お金を足して買う。2 回押しても 1 回しか引かれない
        money.TryAdd(price2 + price3 + 2000, SharedMoneyReason.DebugGrant, "casino check");
        m0 = money.CurrentMoney;
        clerk1.Interact(); clerk1.Interact();
        yield return new WaitForSeconds(.4f);
        Expect(money.CurrentMoney == m0 - price2 && access.KeysBought == 1, $"bought 2F key once: money {m0} -> {money.CurrentMoney}, keys {access.KeysBought}");
        Expect(access.UnlockedFloor == 1 && access.HasUnusedKey, "key bought but not used yet: 2F still locked");
        Expect(clerk1.Label == "店の人と話す", $"1F clerk label after buying '{clerk1.Label}'");

        // ---- 箱に入り、鍵のないまま 2 階を押す → 動かない
        Expect(Walk(cc, new Vector3(cabin0.position.x, 0, cabin0.position.z + 2.2f)) && Walk(cc, cabin0.position), "walk from the hall into the 1F cabin (door is open)");
        yield return null;
        Expect(ev.IsInsideCabin(0, cc.transform.position), "standing inside the 1F cabin");
        var go2 = Button(ev, 0, CasinoElevatorButton.Kind.Go, 1);
        var key0 = Button(ev, 0, CasinoElevatorButton.Kind.KeySlot, 0);
        Expect(go2 != null && key0 != null, "cabin has the 2F button and the key slot");
        Expect(LooksAt(cc, go2, go2.transform.position), $"2F button can be looked at, label '{go2.Label}'");
        go2.Interact();
        yield return new WaitForSeconds(1.0f);
        Expect(ev.IsIdle && ev.CarFloor == 0, "2F button without the key: elevator stays");

        // ---- 鍵を挿して上がる
        Expect(key0.CanInteract() && LooksAt(cc, key0, key0.transform.position), $"key slot label '{key0.Label}'");
        key0.Interact();
        yield return new WaitForSeconds(.4f);
        Expect(access.UnlockedFloor == 2 && !access.HasUnusedKey, $"key used: unlocked floor {access.UnlockedFloor}");
        Expect(!key0.CanInteract(), "key slot has nothing more to do");
        Vector3 rel = cabin0.InverseTransformPoint(cc.transform.position);
        go2.Interact();
        yield return new WaitForSeconds(1.2f);
        Expect(!ev.IsIdle, "elevator is moving");
        float t = 0; while (!ev.IsIdle && t < 10f) { yield return new WaitForSeconds(.1f); t += .1f; }
        yield return new WaitForSeconds(.3f);
        Vector3 rel2 = cabin1.InverseTransformPoint(cc.transform.position);
        Expect(ev.CarFloor == 1 && ev.IsInsideCabin(1, cc.transform.position), $"arrived at 2F after {t + 1.2f:F1}s, player y {cc.transform.position.y:F2}");
        Expect((rel - rel2).magnitude < .25f, $"kept the place inside the cabin (moved {(rel - rel2).magnitude:F2}m)");
        Expect(vis.FloorAt(cc.transform.position) == 0, "floor visibility says 2F");
        yield return new WaitForSeconds(.3f);
        var env = GameObject.Find("Roomies_EnvironmentArt");
        var g2 = env != null ? env.transform.Find(RoomiesTownRebuild.CasinoGroup2F) : null;
        var g3 = env != null ? env.transform.Find(RoomiesTownRebuild.CasinoGroup3F) : null;
        Expect(g2 != null && g3 != null && g2.gameObject.activeSelf && !g3.gameObject.activeSelf, "on 2F: 2F interior shown, 3F interior hidden");

        // ---- 2 階：歩いて出る。店の人から 3 階の鍵を買う
        Expect(Walk(cc, new Vector3(cabin1.position.x, 0, cabin1.position.z + 3.0f)), $"walk out of the cabin onto 2F (at {cc.transform.position})");
        Expect(Mathf.Abs(cc.transform.position.y - (RoomiesTownRebuild.KF2 + cc.height / 2)) < .3f, $"standing on the 2F floor, y {cc.transform.position.y:F2}");
        var clerk2 = clerks[1];
        Teleport(cc, clerk2.transform.position + clerk2.transform.forward * 1.75f);
        yield return null;
        Expect(LooksAt(cc, clerk2, clerk2.transform.position + Vector3.up * 1.2f), $"2F clerk can be looked at, label '{clerk2.Label}'");
        m0 = money.CurrentMoney;
        clerk2.Interact();
        yield return new WaitForSeconds(.4f);
        Expect(money.CurrentMoney == m0 - price3 && access.KeysBought == 2, $"bought 3F key: money {m0} -> {money.CurrentMoney}, keys {access.KeysBought}");

        // ---- 3 階へ
        Teleport(cc, cabin1.position);
        yield return null;
        var key1 = Button(ev, 1, CasinoElevatorButton.Kind.KeySlot, 0);
        key1.Interact();
        yield return new WaitForSeconds(.4f);
        Expect(access.UnlockedFloor == 3, $"3F key used: unlocked floor {access.UnlockedFloor}");
        Button(ev, 1, CasinoElevatorButton.Kind.Go, 2).Interact();
        t = 0; yield return new WaitForSeconds(1f); while (!ev.IsIdle && t < 10f) { yield return new WaitForSeconds(.1f); t += .1f; }
        yield return new WaitForSeconds(.4f);
        Expect(ev.CarFloor == 2 && ev.IsInsideCabin(2, cc.transform.position), $"arrived at 3F, player y {cc.transform.position.y:F2}");
        Expect(g2 != null && !g2.gameObject.activeSelf && g3.gameObject.activeSelf, "on 3F: 3F interior shown, 2F interior hidden");
        Expect(Walk(cc, new Vector3(cabin2.position.x, 0, cabin2.position.z + 3.0f)), "walk out of the cabin onto 3F");
        Expect(Walk(cc, new Vector3(7.8f, 0, -9.0f)), $"walk across 3F to the front windows (at {cc.transform.position})");
        Expect(Mathf.Abs(cc.transform.position.y - (RoomiesTownRebuild.KF3 + cc.height / 2)) < .3f, $"standing on the 3F floor, y {cc.transform.position.y:F2}");
        bool stopped = !Walk(cc, new Vector3(7.8f, 0, -5f));
        Expect(stopped && cc.transform.position.z < -8.2f, $"3F front window wall stops the player (z {cc.transform.position.z:F2})");

        // ---- 1 階へ降りる（鍵はいらない）
        Teleport(cc, cabin2.position);
        yield return null;
        Button(ev, 2, CasinoElevatorButton.Kind.Go, 0).Interact();
        t = 0; yield return new WaitForSeconds(1f); while (!ev.IsIdle && t < 10f) { yield return new WaitForSeconds(.1f); t += .1f; }
        yield return new WaitForSeconds(.4f);
        Expect(ev.CarFloor == 0 && ev.IsInsideCabin(0, cc.transform.position), $"back at 1F, player y {cc.transform.position.y:F2}");
        Expect(g2 != null && !g2.gameObject.activeSelf && !g3.gameObject.activeSelf, "on 1F: 2F and 3F interiors hidden");

        // ---- 外の呼びボタン：2 階の扉の前から呼ぶ（箱は 1 階にある）。扉が閉まっている間は箱に入れない
        Teleport(cc, cabin1.position + new Vector3(.6f, 0, 2.2f));
        yield return null;
        bool blocked = !Walk(cc, cabin1.position);
        Expect(blocked, "2F hall door is closed while the car is on 1F");
        Teleport(cc, cabin1.position + new Vector3(.6f, 0, 2.2f));
        yield return null;
        var call = Button(ev, 1, CasinoElevatorButton.Kind.Call, 1);
        Expect(LooksAt(cc, call, call.transform.position), $"2F call button can be looked at, label '{call.Label}'");
        call.Interact();
        t = 0; yield return new WaitForSeconds(1f); while (!ev.IsIdle && t < 10f) { yield return new WaitForSeconds(.1f); t += .1f; }
        yield return new WaitForSeconds(.8f);
        Expect(ev.CarFloor == 1, $"car came to 2F; the player was not moved (y {cc.transform.position.y:F2})");
        Expect(Walk(cc, cabin1.position), "walk into the 2F cabin after calling");

        // ---- セーブに入るか（RunSaveManager が書くのを待つ）
        yield return new WaitForSeconds(3f);
        var saved = SaveSystem.Load();
        Expect(saved != null && saved.casinoKeysBought == 2 && saved.casinoKeysUsed == 2,
            $"save file has keys bought {(saved != null ? saved.casinoKeysBought : -1)}, used {(saved != null ? saved.casinoKeysUsed : -1)}");

        yield return CheckRates(cc, money, vis);
        yield return CheckRoulette(cc, money);
        CheckDailyRequestCountsOnlyWinnings(money);

        Finish(cc, home, runner);
    }

    // ================================================================ 階ごとのレート（段階 4）
    static Vector3 SlotFront(SlotMachine slot, float floorY, out Vector3 screen)
    {
        var canvas = slot.GetComponentInChildren<Canvas>(true);
        var corners = new Vector3[4]; ((RectTransform)canvas.transform).GetWorldCorners(corners);
        screen = (corners[0] + corners[2]) / 2;
        var faceDir = Vector3.ProjectOnPlane(-canvas.transform.forward, Vector3.up).normalized;
        Vector3 stand = screen + faceDir * 1.1f; stand.y = floorY;
        return stand;
    }

    static IEnumerator WaitIdle(BlackjackTable bj)
    {
        float t = 0; while (!bj.IsIdle && t < 10f) { yield return new WaitForSeconds(.1f); t += .1f; }
    }

    static IEnumerator CheckRates(CharacterController cc, SharedMoneyManager money, CasinoFloorVisibility vis)
    {
        var earning = cc.GetComponentInParent<PlayerEarning>();
        ulong me = cc.GetComponentInParent<Unity.Netcode.NetworkObject>().OwnerClientId;
        money.TryAdd(30000, SharedMoneyReason.DebugGrant, "casino check");
        float f1 = RoomiesTownRebuild.KFloorY(1), f2 = RoomiesTownRebuild.KFloorY(2), f3 = RoomiesTownRebuild.KFloorY(3);
        var slots = Object.FindObjectsByType<SlotMachine>().OrderBy(s => s.GetComponentInChildren<Canvas>(true).transform.position.y).ToArray();
        var slot1 = slots.First();
        var slot2 = slots.Last();
        Expect(slots.Length == 5 && slot2.GetComponentInChildren<Canvas>(true).transform.position.y > f2, $"slots in scene {slots.Length} (3 on 1F, 2 on 2F)");

        // ---- 1 階のスロット：フィーバーの無料の回は、始まった回の賭け金（10）で配当が決まる。途中で 100 に上げても増えない
        {
            Teleport(cc, SlotFront(slot1, f1, out _));
            yield return null;
            var so = new SerializedObject(slot1);
            int oldMiss = so.FindProperty("missPercent").intValue;
            var symbols = so.FindProperty("slotSymbols");
            var oldWeights = Enumerable.Range(0, symbols.arraySize).Select(i => symbols.GetArrayElementAtIndex(i).FindPropertyRelative("weight").intValue).ToArray();
            so.FindProperty("missPercent").intValue = 0;                       // 必ず当たる
            for (int i = 0; i < symbols.arraySize; i++) symbols.GetArrayElementAtIndex(i).FindPropertyRelative("weight").intValue = i == 0 ? 100 : 0;   // いつもチェリー（10）
            so.ApplyModifiedPropertiesWithoutUndo();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(SlotMachine).GetField("feverBetCost", flags).SetValue(slot1, 10);
            ((Unity.Netcode.NetworkVariable<int>)typeof(SlotMachine).GetField("feverSpinCount", flags).GetValue(slot1)).Value = 1;
            while (slot1.CurrentBetLabel != "100 R") slot1.ChangeBet(1);
            int before = money.CurrentMoney;
            slot1.Interact(earning);
            yield return new WaitForSeconds(3.0f);
            Expect(money.CurrentMoney == before + 20, $"fever free spin after switching the bet to 100: money {before} -> {money.CurrentMoney} (+20 expected: cherry 10 x fever 2 at the 10 bet; +200 would mean the bet switch still pays)");
            so.FindProperty("missPercent").intValue = oldMiss;
            for (int i = 0; i < symbols.arraySize; i++) symbols.GetArrayElementAtIndex(i).FindPropertyRelative("weight").intValue = oldWeights[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            while (slot1.CurrentBetLabel != "10 R") slot1.ChangeBet(1);
        }

        // ---- 2 階のスロット：賭け金 100 から
        {
            Vector3 stand = SlotFront(slot2, f2, out Vector3 screen);
            Teleport(cc, stand);
            yield return new WaitForSeconds(.4f);
            Vector3 eye = cc.transform.position + Vector3.up * .6f;
            bool hit = Physics.Raycast(eye, (screen - eye).normalized, out var h, 3f) && h.collider.GetComponentInParent<SlotMachine>() == slot2;
            Expect(hit, "2F slot can be looked at from its front");
            Expect(slot2.CurrentBetLabel == "100 R", $"2F slot lowest bet {slot2.CurrentBetLabel}");
            Expect(Mathf.Abs(slot2.ExpectedReturn() - .978f) < .01f, $"2F slot expected return {slot2.ExpectedReturn() * 100f:F1}% (1F {slot1.ExpectedReturn() * 100f:F1}%)");
            var cabinet = slot2.transform.Find("Visual");
            Expect(cabinet != null && cabinet.gameObject.activeInHierarchy, "on 2F: the 2F slot cabinet is shown");
            int before = money.CurrentMoney;
            slot2.Interact(earning);
            yield return new WaitForSeconds(.3f);
            Expect(money.CurrentMoney == before - 100, $"2F slot spin costs 100: money {before} -> {money.CurrentMoney}");
            yield return new WaitForSeconds(3.0f);
        }

        // ---- ブラックジャック：2 階はダブルダウンあり、3 階はさらにブラックジャックが 3 倍戻し
        var tables = Object.FindObjectsByType<BlackjackTable>().OrderBy(b => b.transform.position.y).ToArray();
        Expect(tables.Length == 3, $"blackjack tables in scene {tables.Length}");
        if (tables.Length == 3)
        {
            var bj1 = tables[0]; var bj2 = tables[1]; var bj3 = tables[2];
            Expect(!bj1.AllowsDoubleDown && bj1.BetAmounts[0] == 10, "1F table: bets from 10, no double down");
            Expect(bj2.AllowsDoubleDown && bj2.BetAmounts[0] == 100 && Mathf.Approximately(bj2.BlackjackReturnMultiplier, 2.5f), "2F table: bets from 100, double down, blackjack returns 2.5x");
            Expect(bj3.AllowsDoubleDown && bj3.BetAmounts[0] == 500 && Mathf.Approximately(bj3.BlackjackReturnMultiplier, 3f), "3F table: bets from 500, double down, blackjack returns 3x");

            // 2 階：ダブルダウンできる回が来るまで（配られてすぐ終わる回もある）
            Vector3 stand2 = bj2.transform.position + bj2.transform.forward * 1.5f; stand2.y = f2;
            Teleport(cc, stand2);
            yield return null;
            Vector3 eye = cc.transform.position + Vector3.up * .6f;
            Vector3 aim = bj2.transform.position + Vector3.up * .95f;
            Expect(Physics.Raycast(eye, (aim - eye).normalized, out var h2, 3f) && h2.collider.GetComponentInParent<BlackjackTable>() == bj2, "2F blackjack table can be looked at");
            bool doubled = false;
            for (int round = 0; round < 10 && !doubled; round++)
            {
                yield return WaitIdle(bj2);
                int start = money.CurrentMoney;
                bj2.Interact(earning);
                yield return new WaitForSeconds(.8f);
                if (!bj2.CanLocalPlayerDouble(me)) { if (bj2.CanLocalPlayerStand(me)) bj2.Stand(); continue; }
                Expect(bj2.GetInteractionLabel(me).Contains("DOUBLE"), "2F table offers double down in its label");
                Expect(money.CurrentMoney == start - 100, $"2F table first bet 100: money {start} -> {money.CurrentMoney}");
                bj2.DoubleDown();
                yield return new WaitForSeconds(.4f);
                Expect(!bj2.CanLocalPlayerDouble(me) && !bj2.CanLocalPlayerStand(me), "after double down the hand is over (one card only)");
                yield return WaitIdle(bj2);
                int diff = money.CurrentMoney - start;
                Expect(diff == -200 || diff == 0 || diff == 200, $"double down result is -200 / 0 / +200: {diff:+0;-0;0}");
                doubled = true;
            }
            Expect(doubled, "played a double down on the 2F table");

            // 3 階：500 から。1 回遊ぶ
            yield return WaitIdle(bj2);
            Vector3 stand3 = bj3.transform.position + bj3.transform.forward * 1.5f; stand3.y = f3;
            Teleport(cc, stand3);
            yield return new WaitForSeconds(.4f);
            var art3 = bj3.transform.Find("Visual");
            Expect(art3 != null && art3.gameObject.activeInHierarchy, "on 3F: the 3F table is shown");
            var cab2 = slot2.transform.Find("Visual");
            Expect(cab2 != null && !cab2.gameObject.activeInHierarchy, "on 3F: the 2F slot cabinet is hidden");
            int m3 = money.CurrentMoney;
            bj3.Interact(earning);
            yield return new WaitForSeconds(.8f);
            if (bj3.CanLocalPlayerStand(me))
            {
                Expect(money.CurrentMoney == m3 - 500, $"3F table bet 500: money {m3} -> {money.CurrentMoney}");
                bj3.Stand();
            }
            yield return WaitIdle(bj3);
            int d3 = money.CurrentMoney - m3;
            Expect(d3 == -500 || d3 == 0 || d3 == 500 || d3 == 1000, $"3F table result is -500 / 0 / +500 / +1000 (blackjack): {d3:+0;-0;0}");
        }
    }

    // ================================================================ ルーレット（段階 5）
    static int SpotIndexOf(RouletteTable table, RouletteBetKind kind, int number)
    {
        for (int i = 0; i < table.SpotCount; i++)
        {
            var s = table.SpotAt(i);
            if (s.kind == kind && (kind != RouletteBetKind.Number || s.number == number)) return i;
        }
        return -1;
    }

    /// <summary>PlayerInteract と同じ見方で、その場所を見ているか（最初に当たる当たり判定がその場所か）。</summary>
    static bool LooksAtSpot(CharacterController cc, RouletteTable table, int spot, out Collider collider)
    {
        collider = null;
        Vector3 eye = cc.transform.position + Vector3.up * .6f;
        Vector3 aim = table.SpotAt(spot).transform.position + Vector3.up * .02f;
        if (!Physics.Raycast(eye, (aim - eye).normalized, out var hit, 3f)) return false;
        collider = hit.collider;
        return hit.collider.GetComponentInParent<RouletteSpot>() == table.SpotAt(spot) && hit.collider.GetComponentInParent<CasinoGame>() == table;
    }

    static IEnumerator CheckRoulette(CharacterController cc, SharedMoneyManager money)
    {
        var earning = cc.GetComponentInParent<PlayerEarning>();
        ulong me = cc.GetComponentInParent<Unity.Netcode.NetworkObject>().OwnerClientId;
        var tables = Object.FindObjectsByType<RouletteTable>().OrderBy(t => t.transform.position.y).ToArray();
        Expect(tables.Length == 2, $"roulette tables in scene {tables.Length} (2F and 3F)");
        if (tables.Length != 2) yield break;
        var t2 = tables[0]; var t3 = tables[1];
        Expect(t2.IsSpawned && t3.IsSpawned, "roulette tables spawned");
        Expect(!t2.ZeroRefundsEvenMoney && t2.BetAmounts[0] == 100, "2F roulette: chips from 100, zero takes all outside bets");
        Expect(t3.ZeroRefundsEvenMoney && t3.BetAmounts[0] == 500, "3F roulette: chips from 500, zero refunds even-money bets");

        // 2 階の台：遊ぶ人の側（台の +Z）の真ん中に立つ
        Teleport(cc, t2.transform.TransformPoint(new Vector3(-.45f, 0, 1.3f)));
        yield return new WaitForSeconds(.4f);
        int red = SpotIndexOf(t2, RouletteBetKind.Red, 0), n17 = SpotIndexOf(t2, RouletteBetKind.Number, 17), n36 = SpotIndexOf(t2, RouletteBetKind.Number, 36), n0 = SpotIndexOf(t2, RouletteBetKind.Number, 0);
        Expect(LooksAtSpot(cc, t2, red, out var redCol), "red can be looked at from the player side");
        Expect(LooksAtSpot(cc, t2, n17, out var n17Col), "number 17 can be looked at");
        Expect(LooksAtSpot(cc, t2, n36, out _), "number 36 (far right, far row) can be looked at");
        Expect(LooksAtSpot(cc, t2, n0, out _), "number 0 can be looked at");
        if (redCol == null || n17Col == null) yield break;
        Expect(t2.GetLabel(redCol, me).Contains("赤") && t2.GetLabel(redCol, me).Contains("100"), "label names the spot and the chip value");
        Expect(t2.IsIdle, "table waits for the first bet");

        int start = money.CurrentMoney;
        t2.Interact(redCol, earning);
        yield return new WaitForSeconds(.4f);
        Expect(money.CurrentMoney == start - 100 && t2.TotalOn(red) == 100 && !t2.IsIdle, $"bet 100 on red: money {start} -> {money.CurrentMoney}, on red {t2.TotalOn(red)}, countdown started");
        Expect(t2.SpotAt(red).chips.gameObject.activeSelf, "chips are shown on red");

        // チップを 500 に替えて 17 に置き、R で下げる
        t2.ChangeBet(1);
        t2.Interact(n17Col, earning);
        yield return new WaitForSeconds(.4f);
        Expect(money.CurrentMoney == start - 600 && t2.TotalOn(n17) == 500, $"bet 500 on 17: money {money.CurrentMoney}, on 17 {t2.TotalOn(n17)}");
        Expect(t2.GetLabel(n17Col, me).Contains("500"), "label shows the own chips on 17");
        t2.Secondary(n17Col, earning);
        yield return new WaitForSeconds(.4f);
        Expect(money.CurrentMoney == start - 100 && t2.TotalOn(n17) == 0, $"took the 17 bet back: money {money.CurrentMoney}, on 17 {t2.TotalOn(n17)}");
        Expect(!t2.SpotAt(n17).chips.gameObject.activeSelf, "chips on 17 are hidden again");
        t2.ChangeBet(-1);

        // 1 人の上限（5000）を超えては置けない
        t2.ChangeBet(-1);   // 1000
        for (int i = 0; i < 6; i++) t2.Interact(n17Col, earning);
        yield return new WaitForSeconds(.5f);
        Expect(t2.TotalOn(n17) == 4000 && money.CurrentMoney == start - 4100, $"per-player limit 5000: on 17 {t2.TotalOn(n17)}, money {money.CurrentMoney} (100 on red + 4000, the rest refused)");
        t2.ChangeBet(1);

        // 締め切り → 回る → 結果。払い戻しが決まりどおりか
        float t = 0; while (t2.IsBettingOpen && t < 20f) { yield return new WaitForSeconds(.2f); t += .2f; }
        Expect(!t2.IsBettingOpen, $"betting closed after {t:F0}s");
        int mid = money.CurrentMoney;
        t2.Interact(redCol, earning);
        yield return new WaitForSeconds(.3f);
        Expect(money.CurrentMoney == mid, "no bets while the wheel spins");
        t = 0; while (!t2.IsIdle && t < 20f) { yield return new WaitForSeconds(.2f); t += .2f; }
        int result = t2.LastResult;
        int back = 100 * RouletteRules.ReturnMultiplier(RouletteBetKind.Red, 0, result, false) + 4000 * RouletteRules.ReturnMultiplier(RouletteBetKind.Number, 17, result, false);
        Expect(result >= 0 && result <= 36, $"result {RouletteRules.ResultName(result)}".Replace("緑", "green").Replace("赤", "red").Replace("黒", "black"));
        Expect(money.CurrentMoney == start - 4100 + back, $"payout by the rules: money {start} - 4100 + {back} = {money.CurrentMoney}");
        Expect(t2.TotalOn(red) == 0 && t2.TotalOn(n17) == 0 && !t2.SpotAt(red).chips.gameObject.activeSelf, "table cleared for the next round");

        // 3 階の台：置けるか（1 回だけ。結果は待つ）
        Teleport(cc, t3.transform.TransformPoint(new Vector3(-.45f, 0, 1.3f)));
        yield return new WaitForSeconds(.4f);
        int black3 = SpotIndexOf(t3, RouletteBetKind.Black, 0);
        Expect(LooksAtSpot(cc, t3, black3, out var blackCol), "3F roulette: black can be looked at");
        int s3 = money.CurrentMoney;
        if (blackCol != null) t3.Interact(blackCol, earning);
        yield return new WaitForSeconds(.4f);
        Expect(money.CurrentMoney == s3 - 500, $"3F roulette: chip 500, money {s3} -> {money.CurrentMoney}");
        t = 0; while (!t3.IsIdle && t < 40f) { yield return new WaitForSeconds(.2f); t += .2f; }
        int r3 = t3.LastResult;
        int back3 = 500 * RouletteRules.ReturnMultiplier(RouletteBetKind.Black, 0, r3, true);
        Expect(money.CurrentMoney == s3 - 500 + back3, $"3F payout: result {r3}, back {back3}, money {money.CurrentMoney}");
    }

    // ================================================================ 今日のおねがい：賭け金を引いた差だけを数える
    static void CheckDailyRequestCountsOnlyWinnings(SharedMoneyManager money)
    {
        var req = DailyRequestManager.Instance;
        Expect(req != null && req.IsSpawned, "daily request manager present");
        if (req == null) return;
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        void Set<T>(string field, T value) => ((Unity.Netcode.NetworkVariable<T>)typeof(DailyRequestManager).GetField(field, flags).GetValue(req)).Value = value;

        // 「◯円稼ぐ」
        Set("kind", (byte)DailyRequestManager.RequestKind.EarnMoney); Set("target", 1000000); Set("progress", 0); Set("done", false);
        money.TryAdd(500, SharedMoneyReason.BlackjackPayout, "check: push", 500);
        Expect(req.Progress == 0, $"earn request: a push (500 back on a 500 bet) counts 0 (progress {req.Progress})");
        money.TryAdd(1000, SharedMoneyReason.BlackjackPayout, "check: win", 500);
        Expect(req.Progress == 500, $"earn request: a win (1000 back on 500) counts 500 (progress {req.Progress})");
        money.TryAdd(300, SharedMoneyReason.CasinoPayout, "check: partial", 1000);
        Expect(req.Progress == 500, $"earn request: a net loss counts 0 (progress {req.Progress})");
        money.TryAdd(200, SharedMoneyReason.DeliveryReward, "check: delivery");
        Expect(req.Progress == 700, $"earn request: other income still counts in full (progress {req.Progress})");

        // 「ギャンブルで◯回勝つ」
        Set("kind", (byte)DailyRequestManager.RequestKind.GambleWins); Set("target", 100); Set("progress", 0); Set("done", false);
        money.TryAdd(500, SharedMoneyReason.BlackjackPayout, "check: push", 500);
        money.TryAdd(10, SharedMoneyReason.SlotReward, "check: slot even", 10);
        Expect(req.Progress == 0, $"gamble-wins request: pushes are not wins (progress {req.Progress})");
        money.TryAdd(1000, SharedMoneyReason.CasinoPayout, "check: roulette win", 500);
        money.TryAdd(20, SharedMoneyReason.SlotReward, "check: fever free spin");
        Expect(req.Progress == 2, $"gamble-wins request: real wins count (progress {req.Progress})");
    }

    /// <summary>
    /// 「続きから」の確認（Play Mode。GameRoom に入る前に実行する）：鍵 2 本を買って 1 本だけ挿したセーブを読み込んだことにして、ホストで入る。
    /// 入ったあと Check - Continue Result を実行すると、鍵が戻っているかを Temp/RoomiesCasino/continue.txt に書く。
    /// </summary>
    [MenuItem("Roomies/Casino/Check - Continue With Keys (Play Mode)")]
    static void CheckContinueEnter()
    {
        if (!EditorApplication.isPlaying) return;
        SaveSystem.SetPendingLoad(new RoomiesSaveData { day = 2, time = 0, sharedMoney = 4321, casinoKeysBought = 2, casinoKeysUsed = 1 });
        RoomiesTownRebuild.EnterGameRoomAsHost();
    }

    [MenuItem("Roomies/Casino/Check - Continue Result (Play Mode)")]
    static void CheckContinueResult()
    {
        if (!EditorApplication.isPlaying) return;
        var access = CasinoAccess.Instance; var money = SharedMoneyManager.Instance;
        bool ok = access != null && money != null && access.KeysBought == 2 && access.KeysUsed == 1 && access.UnlockedFloor == 2 && access.HasUnusedKey && money.CurrentMoney == 4321;
        string line = $"{(ok ? "OK " : "NG ")} continue: keys bought {(access != null ? access.KeysBought : -1)}, used {(access != null ? access.KeysUsed : -1)}, unlocked floor {(access != null ? access.UnlockedFloor : -1)}, money {(money != null ? money.CurrentMoney : -1)}";
        System.IO.Directory.CreateDirectory("Temp/RoomiesCasino");
        System.IO.File.WriteAllText("Temp/RoomiesCasino/continue.txt", line);
        Debug.Log("[Casino] " + line);
    }

    static void Finish(CharacterController cc, Vector3 home, MonoBehaviour runner)
    {
        cc.enabled = false; cc.transform.position = home; Physics.SyncTransforms(); cc.enabled = true;
        checkLog.Insert(0, $"casino check: ok {checkOk}, ng {checkNg}\n");
        System.IO.Directory.CreateDirectory("Temp/RoomiesCasino");
        System.IO.File.WriteAllText("Temp/RoomiesCasino/check.txt", checkLog.ToString());
        Debug.Log("[Casino] check: " + checkLog.ToString().Split('\n')[0]);
        Object.Destroy(runner.gameObject);
    }
}
#endif
