#if UNITY_EDITOR
using System.Collections;
using System.IO;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// コンビニのレジ打ちのバイトの通し確認（Play Mode）：Roomies/Store/Job Check (Play Mode)。
/// 先に Roomies/Art/Town/Walk - Enter GameRoom As Host (Play Mode) でホストとして入っておく。
/// 本当にバイトをする（共同口座にバイト代が入る）。遊んでいるセーブでは使わない（セーブの控えを取ってから Play Mode にし、あとで戻すこと）。
/// 1 回目：お客さん全員。2 人目はわざとお釣りをまちがえ、4 人目は待たせて帰らせる（待てる秒数を確認の間だけ短くする）。
/// 2 回目：始めてすぐ R で終える。3 回目：途中で夜にして終わらせる。
/// 結果は Temp/RoomiesStore/job_check.txt、コマ送りの写真は Temp/RoomiesCasino/frames/storejob/。
/// </summary>
internal static partial class RoomiesCasinoDemo
{
    const string StoreJobCheckPath = "Temp/RoomiesStore/job_check.txt";

    [MenuItem("Roomies/Store/Job Check (Play Mode)")]
    static void StoreJobCheck()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Store] Play Mode で実行してください"); return; }
        var runner = NewRunner();
        runner.StartCoroutine(StoreJobCheckRoutine(runner));
    }

    static IEnumerator StoreJobCheckRoutine(Runner runner)
    {
        Directory.CreateDirectory("Temp/RoomiesStore");
        File.Delete(StoreJobCheckPath);
        if (Directory.Exists(OutDir + "/storejob"))
            foreach (string file in Directory.GetFiles(OutDir + "/storejob", "*", SearchOption.AllDirectories))
                try { File.Delete(file); } catch (IOException) { }
        var log = new System.Text.StringBuilder();
        int ok = 0, ng = 0;
        void Expect(bool good, string what) { if (good) ok++; else ng++; log.AppendLine((good ? "OK  " : "NG  ") + what); }
        bool asyncBefore = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        var balance = GameBalance.Current;
        float patienceBefore = balance.storeJobPatienceSeconds;

        var shop = Object.FindAnyObjectByType<StoreShop>();
        var money = SharedMoneyManager.Instance;
        var dm = DayManager.Instance;
        var cc = MyController();
        Expect(shop != null && shop.IsSpawned && money != null && dm != null && cc != null, "job: shop, money, day manager and player found");
        if (shop == null || money == null || dm == null || cc == null) { FinishJob(log, ok, ng, asyncBefore); yield break; }
        var earning = cc.GetComponentInParent<PlayerEarning>();
        ulong me = cc.GetComponentInParent<NetworkObject>().OwnerClientId;
        Vector3 W(float x, float y, float z) => shop.transform.TransformPoint(new Vector3(x, y, z));
        bool InArea() { Vector3 l = shop.transform.InverseTransformPoint(cc.transform.position); return l.x > 2.8f && l.x < 7.4f && l.z > -.4f && l.z < 1.0f; }
        Expect(shop.TimeCardCollider != null && shop.TillCollider != null, "job: time card and till are wired");

        // ---- 夜はバイトできない
        dm.ServerApplySavedState(dm.CurrentDay, 1, 0, 0);
        yield return new WaitForSeconds(.6f);
        Teleport(cc, W(1.9f, .1f, .55f));
        yield return new WaitForSeconds(.4f);
        string nightLabel = shop.GetLabel(shop.TimeCardCollider, me);
        shop.Interact(shop.TimeCardCollider, earning);
        yield return new WaitForSeconds(.6f);
        Expect(nightLabel != null && nightLabel.Contains("朝だけ") && shop.Cashier == ulong.MaxValue, $"night: refuses the job ('{Strip(nightLabel)}')");

        // ---- 朝：タイムカードでバイトに入る
        dm.ServerApplySavedState(dm.CurrentDay, 0, 0, 0);
        yield return new WaitForSeconds(.6f);
        string cardLabel = shop.GetLabel(shop.TimeCardCollider, me);
        Expect(cardLabel != null && cardLabel.Contains("バイトに入る"), $"morning: time card offers the job ('{Strip(cardLabel)}')");
        Teleport(cc, W(-1.0f, .1f, 2.6f));   // 写真の手前をふさがないよう、少し離れる
        for (float wb = 0; wb < 8f && shop.ClerkBubble != null && shop.ClerkBubble.IsShowing; wb += Time.deltaTime) yield return null;   // 夜の断りの吹き出しが消えてから撮る
        yield return new WaitForSeconds(.4f);
        Camera cardCam = NewCamera(runner.transform, W(1.15f, 1.65f, 1.3f), W(2.75f, 1.2f, .3f), 55);
        yield return new WaitForEndOfFrame();
        Shoot(cardCam, "storejob", "a_timecard", 2);
        Object.Destroy(cardCam.gameObject);
        Teleport(cc, W(1.9f, .1f, .55f));
        yield return new WaitForSeconds(.4f);

        int moneyStart = money.CurrentMoney, earnStart = earning != null ? earning.GetEarning() : 0;
        shop.Interact(shop.TimeCardCollider, earning);
        yield return new WaitForSeconds(.8f);
        Expect(shop.Cashier == me && InArea(), $"start: the player is the cashier and stands behind the register (at {shop.transform.InverseTransformPoint(cc.transform.position):F2})");
        Expect(shop.ClerkBubble != null && shop.ClerkBubble.IsShowing, $"start: the clerk says something ('{(shop.ClerkBubble != null ? shop.ClerkBubble.CurrentLine : "")}')");

        Camera sideCam = NewCamera(runner.transform, W(3.3f, 1.75f, 3.9f), W(5.5f, 1.05f, 1.4f), 58);
        Camera eyeCam = NewCamera(runner.transform, W(5.5f, 1.95f, .45f), W(5.45f, 1.15f, 2.2f), 66);
        // 吹き出しと浮く文字は Camera.main（ふだんはプレイヤーのカメラ）のほうを向く。写真のカメラから見て表になるよう、撮る間だけ写真のカメラを Main にする
        var mainCams = new System.Collections.Generic.List<Camera>();
        foreach (var mc in Camera.allCameras) if (mc.CompareTag("MainCamera")) mainCams.Add(mc);
        void AsMain(Camera cam) { foreach (var mc in mainCams) if (mc != null) mc.tag = "Untagged"; sideCam.tag = "Untagged"; eyeCam.tag = "Untagged"; sideCam.enabled = false; eyeCam.enabled = false; cam.tag = "MainCamera"; cam.enabled = true; }   // Camera.main は、有効なカメラしか返さない
        void RestoreMain() { sideCam.tag = "Untagged"; eyeCam.tag = "Untagged"; sideCam.enabled = false; eyeCam.enabled = false; foreach (var mc in mainCams) if (mc != null) mc.tag = "MainCamera"; }
        int customers = StoreShop.JobCustomers;
        int expectedEarn = 0, expectedMistakes = 0;
        for (int c = 0; c < customers; c++)
        {
            bool wrong = c == 1, giveUp = c == 3;
            if (giveUp) balance.storeJobPatienceSeconds = 5f;
            AsMain(sideCam);
            float t = 0; int f = 0;
            while (t < 8f && shop.CurrentJobPhase != StoreShop.JobPhase.Scan)
            {
                yield return new WaitForEndOfFrame();
                if (c == 0 && t >= f * .1f) { Shoot(sideCam, "storejob/enter", $"f_{f:D3}"); f++; }
                t += Time.deltaTime;
            }
            Expect(shop.CurrentJobPhase == StoreShop.JobPhase.Scan && shop.CustomerShown && shop.JobCustomerIndex == c,
                $"customer {c + 1}: walks in and puts {shop.JobItemCount} item(s) on the counter ({t:F1}s)");
            if (giveUp)
            {
                int before = money.CurrentMoney;
                t = 0;
                while (t < 9f && shop.CurrentJobPhase == StoreShop.JobPhase.Scan) { yield return null; t += Time.deltaTime; }
                Expect(shop.CurrentJobPhase == StoreShop.JobPhase.Leave && money.CurrentMoney == before, $"customer {c + 1}: leaves after waiting too long, no pay ({t:F1}s)");
                expectedMistakes++;
                balance.storeJobPatienceSeconds = patienceBefore;
                yield return WaitPhaseNot(shop, StoreShop.JobPhase.Leave, 4f);
                continue;
            }
            string scanLabel = shop.GetLabel(shop.TillCollider, me);
            Expect(scanLabel != null && scanLabel.Contains("読み取る"), $"customer {c + 1}: label says scan ('{Strip(scanLabel)}')");
            int items = shop.JobItemCount;
            AsMain(eyeCam);
            t = 0; f = 0;
            for (int i = 0; i < items; i++)
            {
                shop.Interact(shop.TillCollider, earning);
                float w = 0;
                while (w < .45f) { yield return new WaitForEndOfFrame(); if (c == 0 && t >= f * .07f) { Shoot(eyeCam, "storejob/scan", $"f_{f:D3}"); f++; } w += Time.deltaTime; t += Time.deltaTime; }
            }
            // 1 人目：お札を出して「お預かり」の文字が出るところまで撮る（出た瞬間の向きも確かめる）
            if (c == 0) { float w2 = 0; while (w2 < .9f) { yield return new WaitForEndOfFrame(); if (t >= f * .07f) { Shoot(eyeCam, "storejob/scan", $"f_{f:D3}"); f++; } w2 += Time.deltaTime; t += Time.deltaTime; } }
            Expect(shop.JobScanned == items && shop.CurrentJobPhase == StoreShop.JobPhase.Change, $"customer {c + 1}: all {items} scanned, waiting for change (total ¥{shop.JobTotal:N0}, paid ¥{shop.JobPaid:N0})");
            int correct = shop.JobPaid - shop.JobTotal;
            int[] options = shop.ChangeOptions();
            Expect(options.Length == 3 && System.Array.IndexOf(options, correct) >= 0, $"customer {c + 1}: 3 change options include the right one ({string.Join(",", options)} / ¥{correct})");
            for (int k = 0; k < 4 && (shop.SelectedChange == correct) == wrong; k++) { shop.ChangeBet(1); yield return null; }
            yield return new WaitForSeconds(.35f);
            if (c == 0) Shoot(eyeCam, "storejob", "b_change", 2);
            string changeLabel = shop.GetLabel(shop.TillCollider, me);
            Expect(changeLabel.Contains("お釣り") && changeLabel.Contains(shop.SelectedChange.ToString("N0")) && shop.JobScreenLine.Contains(shop.SelectedChange.ToString("N0")),
                $"customer {c + 1}: label and screen show the chosen change ¥{shop.SelectedChange:N0} ('{shop.JobScreenLine.Replace("\n", " / ")}')");
            int moneyBefore = money.CurrentMoney;
            shop.Interact(shop.TillCollider, earning);
            yield return new WaitForSeconds(.4f);
            Expect(shop.CurrentJobPhase == StoreShop.JobPhase.Bag && shop.JobMistakes == expectedMistakes + (wrong ? 1 : 0),
                $"customer {c + 1}: change {(wrong ? "is wrong (counted as a mistake)" : "is right")} (mistakes {shop.JobMistakes})");
            if (wrong) { expectedMistakes++; Expect(shop.CustomerBubble != null && shop.CustomerBubble.IsShowing && shop.CustomerBubble.CurrentLine.Contains(correct.ToString("N0")), $"customer {c + 1}: the customer points out the right change ('{shop.CustomerBubble?.CurrentLine}')"); }
            AsMain(sideCam);
            shop.Interact(shop.TillCollider, earning);
            t = 0; f = 0;
            while (t < 1.6f) { yield return new WaitForEndOfFrame(); if (c == 0 && t >= f * .08f) { Shoot(sideCam, "storejob/bag", $"f_{f:D3}"); f++; } t += Time.deltaTime; }
            Expect(shop.CustomerBubble != null && shop.CustomerBubble.IsShowing && shop.CustomerBubble.CurrentLine.Length > 0, $"customer {c + 1}: says a word when leaving with the bag ('{shop.CustomerBubble?.CurrentLine}')");
            int pay = wrong ? 0 : balance.storeJobPayPerCustomer + balance.storeJobFastBonus;
            expectedEarn += pay;
            Expect(money.CurrentMoney == moneyBefore + pay, $"customer {c + 1}: wage ¥{pay} (shared money {moneyBefore} -> {money.CurrentMoney})");
            yield return WaitPhaseNot(shop, StoreShop.JobPhase.Leave, 4f);
        }
        // 終わり
        float te = 0; int fe = 0;
        while (te < 2.0f) { yield return new WaitForEndOfFrame(); if (te >= fe * .1f) { Shoot(sideCam, "storejob/end", $"f_{fe:D3}"); fe++; } te += Time.deltaTime; }
        yield return new WaitForSeconds(.6f);
        Expect(shop.CurrentJobPhase == StoreShop.JobPhase.Off && shop.Cashier == ulong.MaxValue && !InArea(), $"end: the shift is over and the player is back in the shop ({shop.transform.InverseTransformPoint(cc.transform.position):F2})");
        Expect(money.CurrentMoney - moneyStart == expectedEarn && expectedMistakes == 2, $"end: total wage ¥{money.CurrentMoney - moneyStart} = expected ¥{expectedEarn}, no perfect bonus with 2 mistakes");
        Expect(earning == null || earning.GetEarning() - earnStart == expectedEarn, $"end: the player's own earning counts the wage (+¥{(earning != null ? earning.GetEarning() - earnStart : 0)})");
        RestoreMain();
        Object.Destroy(sideCam.gameObject);
        Object.Destroy(eyeCam.gameObject);

        // ---- 2 回目：始めてすぐ R で終える
        shop.Interact(shop.TimeCardCollider, earning);
        yield return new WaitForSeconds(.8f);
        Expect(shop.Cashier == me && InArea(), "quit: second shift started");
        int beforeQuit = money.CurrentMoney;
        shop.Secondary(shop.TillCollider, earning);
        yield return new WaitForSeconds(.8f);
        Expect(shop.Cashier == ulong.MaxValue && !InArea() && money.CurrentMoney == beforeQuit, "quit: R ends the shift, the player is moved out, no money moved");

        // ---- 3 回目：途中で夜になると終わる
        shop.Interact(shop.TimeCardCollider, earning);
        yield return new WaitForSeconds(3.5f);
        Expect(shop.Cashier == me && shop.CustomerShown, "night: third shift started and a customer came");
        dm.ServerApplySavedState(dm.CurrentDay, 1, 0, 0);
        yield return new WaitForSeconds(1.0f);
        Expect(shop.Cashier == ulong.MaxValue && !InArea() && !shop.CustomerShown, "night: the shift ends when night comes, the player is moved out");

        // ---- 買い物のレジは、バイトのあとも前のまま
        string regLabel = shop.GetLabel(shop.RegisterCollider, me);
        Expect(shop.CurrentPhase == StoreShop.Phase.Idle && regLabel != null && !regLabel.Contains("バイト"), $"shop: the shopping register is unchanged ('{Strip(regLabel)}')");
        dm.ServerApplySavedState(dm.CurrentDay, 0, 0, 0);

        balance.storeJobPatienceSeconds = patienceBefore;
        FinishJob(log, ok, ng, asyncBefore);
        Object.Destroy(runner.gameObject);
    }

    static IEnumerator WaitPhaseNot(StoreShop shop, StoreShop.JobPhase phase, float seconds)
    {
        float t = 0;
        while (t < seconds && shop.CurrentJobPhase == phase) { yield return null; t += Time.deltaTime; }
    }

    static void FinishJob(System.Text.StringBuilder log, int ok, int ng, bool asyncBefore)
    {
        ShaderUtil.allowAsyncCompilation = asyncBefore;
        File.WriteAllText(StoreJobCheckPath, $"store job check: ok {ok}, ng {ng}\n" + log);
        Debug.Log($"[Store] job check: ok {ok}, ng {ng}");
    }
}
#endif
