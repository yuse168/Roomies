#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// コンビニの通し確認（Play Mode）：Roomies/Store/Check (Play Mode)。
/// 先に Roomies/Art/Town/Walk - Enter GameRoom As Host (Play Mode) でホストとして入っておく。
/// 本当に買い物をする（共同口座から食材 1 回分を払い、冷蔵庫に食材が入る。足りなければ確認のために足す）。遊んでいるセーブでは使わない
/// （確認のときは、セーブの控えを取ってから Play Mode にし、あとで戻すこと）。
/// 結果は Temp/RoomiesStore/check.txt、コマ送りの写真は Temp/RoomiesCasino/frames/store/。
/// </summary>
internal static partial class RoomiesCasinoDemo
{
    const string StoreCheckPath = "Temp/RoomiesStore/check.txt";

    [MenuItem("Roomies/Store/Check (Play Mode)")]
    static void StoreCheck()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Store] Play Mode で実行してください"); return; }
        var runner = NewRunner();
        runner.StartCoroutine(StoreCheckRoutine(runner));
    }

    static IEnumerator StoreCheckRoutine(Runner runner)
    {
        Directory.CreateDirectory("Temp/RoomiesStore");
        File.Delete(StoreCheckPath);
        // 前の回の写真を消す（ほかのプログラムがフォルダを開いていて消せないときは、消せる物だけ消して続ける）
        if (Directory.Exists(OutDir + "/store"))
            foreach (string file in Directory.GetFiles(OutDir + "/store", "*", SearchOption.AllDirectories))
                try { File.Delete(file); } catch (IOException) { }
        var log = new System.Text.StringBuilder();
        int ok = 0, ng = 0;
        void Expect(bool good, string what) { if (good) ok++; else ng++; log.AppendLine((good ? "OK  " : "NG  ") + what); }
        bool asyncBefore = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;

        var shop = Object.FindAnyObjectByType<StoreShop>();
        var door = Object.FindAnyObjectByType<StoreAutoDoor>();
        var clerk = Object.FindObjectsByType<CasinoClerk>().FirstOrDefault(c => c.name.StartsWith("StoreClerk"));
        var fridge = NightFridge.Instance;
        var money = SharedMoneyManager.Instance;
        var dm = DayManager.Instance;
        var cc = MyController();
        Expect(shop != null && shop.IsSpawned, "store: shop present and spawned");
        Expect(door != null && door.IsSpawned, "store: automatic door present and spawned");
        Expect(clerk != null && clerk.IsSpawned, "store: clerk present and spawned");
        Expect(fridge != null && money != null && dm != null && cc != null, "store: fridge, money, day manager and player found");
        if (shop == null || door == null || fridge == null || money == null || dm == null || cc == null) { Finish(log, ok, ng, asyncBefore); yield break; }
        var earning = cc.GetComponentInParent<PlayerEarning>();
        ulong me = cc.GetComponentInParent<NetworkObject>().OwnerClientId;

        // 夜にして、冷蔵庫を空・故障なしにする
        if (!dm.IsNight) dm.ServerApplySavedState(dm.CurrentDay, 1, 0, 0);
        if (fridge.IsStocked) fridge.ServerConsumeStock();
        if (fridge.IsBroken)
        {
            var nv = (NetworkVariable<bool>)typeof(NightFridge).GetField("broken", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fridge);
            nv.Value = false;
        }
        yield return new WaitForSeconds(.6f);
        Expect(dm.IsNight && fridge.CurrentStockBlock == NightFridge.StockBlock.None, $"store: night, fridge empty and working (block {fridge.CurrentStockBlock})");

        // ---- 自動ドア：外から近づくと開き、いらっしゃいませ
        Teleport(cc, new Vector3(26.1f, .05f, -3.2f));
        yield return new WaitForSeconds(1.2f);
        Expect(!door.IsOpen, "door: closed while nobody is near");
        Camera outCam = NewCamera(runner.transform, new Vector3(27.6f, 1.6f, -4.6f), new Vector3(26.1f, 1.3f, -8.7f), 60);
        Teleport(cc, new Vector3(26.1f, .05f, -6.6f));
        float tw = 0; int f = 0;
        while (tw < 1.2f) { yield return new WaitForEndOfFrame(); if (tw >= f * .08f) { Shoot(outCam, "store/door", $"f_{f:D2}"); f++; } tw += Time.deltaTime; }
        Expect(door.IsOpen && door.OpenAmount > .9f, $"door: opens when a player comes near (open amount {door.OpenAmount:F2})");
        var bubble = shop.ClerkBubble;
        Expect(bubble != null && bubble.IsShowing && bubble.CurrentLine.Contains("いらっしゃいませ"), $"door: the clerk greets the customer ('{(bubble != null ? bubble.CurrentLine : "")}')");
        Object.Destroy(outCam.gameObject);

        // ---- 中へ（歩いて通れる）：とびらの所を通り抜ける
        Teleport(cc, new Vector3(26.1f, .2f, -10.4f));
        yield return new WaitForSeconds(.5f);
        Expect(cc.transform.position.z < -9.8f && cc.transform.position.y > .05f, $"store: the player stands inside the store (at {cc.transform.position:F1})");
        // 入ってすぐ（とびらから 2.3m 以内）は開いたまま。奥へ進むと閉まる
        Teleport(cc, new Vector3(26.1f, .2f, -12.4f));
        yield return new WaitForSeconds(2.0f);
        Expect(!door.IsOpen, "door: closes again after the customer walks further in");

        // ---- 棚から 3 つ（おにぎり・飲み物・お菓子）。1 つ目の動きを撮る
        Teleport(cc, new Vector3(20.9f, .2f, -14.2f));
        yield return new WaitForSeconds(.4f);
        int Find(int kind) { for (int i = 0; i < shop.SectionCount; i++) if (shop.SectionCollider(i).name.EndsWith(new[] { "Onigiri", "Bento", "Bread", "Snacks", "Cups", "Drinks", "Milk", "Ice" }[kind])) return i; return -1; }
        int sOni = Find(0), sDrink = Find(5), sSnack = Find(3);
        Expect(sOni >= 0 && sDrink >= 0 && sSnack >= 0, "shelves: onigiri, drink and snack sections found");
        string label0 = shop.GetLabel(shop.SectionCollider(sOni), me);
        Expect(label0 != null && label0.Contains("おにぎり") && label0.Contains("0/3"), $"shelves: label offers to take one ('{Strip(label0)}')");
        // プレイヤーの体が手前をふさがないよう、横の少しうしろから撮る
        Camera shelfCam = NewCamera(runner.transform, new Vector3(21.9f, 1.75f, -16.3f), new Vector3(20.2f, 1.45f, -14.3f), 60);
        shop.Interact(shop.SectionCollider(sOni), earning);
        tw = 0; f = 0;
        while (tw < .8f) { yield return new WaitForEndOfFrame(); if (tw >= f * .06f) { Shoot(shelfCam, "store/pick", $"f_{f:D2}"); f++; } tw += Time.deltaTime; }
        Object.Destroy(shelfCam.gameObject);
        Expect(shop.CartOf(me).Count == 1, $"shelves: one item in the basket ({shop.CartOf(me).Count})");
        shop.Interact(shop.SectionCollider(sDrink), earning);
        yield return new WaitForSeconds(.4f);
        shop.Interact(shop.SectionCollider(sSnack), earning);
        yield return new WaitForSeconds(.4f);
        shop.Interact(shop.SectionCollider(sSnack), earning);     // 4 つ目は入らない
        yield return new WaitForSeconds(.4f);
        var cart = shop.CartOf(me);
        Expect(cart.Count == 3 && cart[0] == 0 && cart[1] == 5 && cart[2] == 3, $"shelves: basket holds 3 (onigiri, drink, snack) and a 4th is refused ({string.Join(",", cart)})");
        Expect(shop.GetLabel(shop.SectionCollider(sOni), me).Contains("カゴがいっぱい"), "shelves: label says the basket is full");
        shop.Secondary(shop.SectionCollider(sSnack), earning);
        yield return new WaitForSeconds(.4f);
        Expect(shop.CartOf(me).Count == 2, "shelves: R puts the last item back (2 left)");
        shop.Interact(shop.SectionCollider(sSnack), earning);
        yield return new WaitForSeconds(.4f);
        Expect(shop.CartOf(me).Count == 3, "shelves: picked the snack again (3)");

        // ---- 昼はお会計できない（冷蔵庫の補充と同じ決まり）
        dm.ServerApplySavedState(dm.CurrentDay, 0, 0, 0);
        yield return new WaitForSeconds(.5f);
        Teleport(cc, new Vector3(29.4f, .2f, -11.1f));
        yield return new WaitForSeconds(.4f);
        string dayLabel = shop.GetLabel(shop.RegisterCollider, me);
        shop.Interact(shop.RegisterCollider, earning);
        yield return new WaitForSeconds(.5f);
        Expect(dayLabel.Contains("夜だけ") && shop.CurrentPhase == StoreShop.Phase.Idle && shop.CartOf(me).Count == 3, $"register: refuses in the daytime and keeps the basket ('{Strip(dayLabel)}')");
        dm.ServerApplySavedState(dm.CurrentDay, 1, 0, 0);
        yield return new WaitForSeconds(.6f);

        // ---- お会計：読み取り → 支払い → 袋
        int price = shop.SetPrice;
        if (!money.CanPay(price)) money.TryAdd(price * 2, SharedMoneyReason.DebugGrant, "store check");
        int before = money.CurrentMoney;
        string regLabel = shop.GetLabel(shop.RegisterCollider, me);
        Expect(regLabel.Contains("お会計") && regLabel.Contains(price.ToString("N0")), $"register: label offers to check out for the fridge price ¥{price:N0} ('{Strip(regLabel)}')");
        Camera regCam = NewCamera(runner.transform, new Vector3(31.9f, 1.5f, -11.0f), new Vector3(31.6f, 1.0f, -12.45f), 58);
        shop.Interact(shop.RegisterCollider, earning);
        tw = 0; f = 0;
        while (tw < 6f && shop.CurrentPhase != StoreShop.Phase.Pay) { yield return new WaitForEndOfFrame(); if (tw >= f * .08f) { Shoot(regCam, "store/scan", $"f_{f:D3}"); f++; } tw += Time.deltaTime; }
        yield return new WaitForSeconds(.3f);
        Expect(shop.CurrentPhase == StoreShop.Phase.Pay && shop.Buyer == me && shop.CartOf(me).Count == 0, $"register: items are scanned and it waits for payment (phase {shop.CurrentPhase}, after {tw:F1}s)");
        Expect(shop.DisplayLine.Contains(price.ToString("N0")), $"register: the display shows the total ('{shop.DisplayLine.Replace("\n", " ")}')");
        Expect(money.CurrentMoney == before, "register: no money moves before paying");
        shop.Interact(shop.RegisterCollider, earning);
        tw = 0; f = 0;
        while (tw < 1.4f) { yield return new WaitForEndOfFrame(); if (tw >= f * .08f) { Shoot(regCam, "store/pay", $"f_{f:D2}"); f++; } tw += Time.deltaTime; }
        Expect(money.CurrentMoney == before - price, $"pay: shared money {before} -> {money.CurrentMoney} (exactly ¥{price})");
        Expect(fridge.IsStocked, "pay: the fridge at home is stocked");
        Expect(shop.CurrentPhase == StoreShop.Phase.Bag && shop.BagShown, "pay: the bag is handed over on the counter");
        Expect(bubble.IsShowing && (bubble.CurrentLine.Contains("ありがとう")), $"pay: the clerk thanks ('{bubble.CurrentLine}')");
        shop.Interact(shop.RegisterCollider, earning);
        tw = 0; f = 0;
        while (tw < .8f) { yield return new WaitForEndOfFrame(); if (tw >= f * .08f) { Shoot(regCam, "store/bag", $"f_{f:D2}"); f++; } tw += Time.deltaTime; }
        Expect(shop.CurrentPhase == StoreShop.Phase.Idle && !shop.BagShown, "bag: taken, the register is idle again");
        Expect(money.CurrentMoney == before - price, "bag: no extra money moved");
        Object.Destroy(regCam.gameObject);

        // ---- 冷蔵庫がいっぱいなら、お会計しない
        foreach (int s in new[] { sOni, sOni, sDrink }) { shop.Interact(shop.SectionCollider(s), earning); yield return new WaitForSeconds(.3f); }
        int before2 = money.CurrentMoney;
        string fullLabel = shop.GetLabel(shop.RegisterCollider, me);
        shop.Interact(shop.RegisterCollider, earning);
        yield return new WaitForSeconds(.6f);
        Expect(fullLabel.Contains("いっぱい") && shop.CurrentPhase == StoreShop.Phase.Idle && money.CurrentMoney == before2, $"register: refuses when the fridge is already stocked ('{Strip(fullLabel)}')");
        shop.Secondary(shop.RegisterCollider, earning);
        yield return new WaitForSeconds(.4f);
        Expect(shop.CartOf(me).Count == 0, "register: R empties the basket");

        // ---- 店の人の世間話
        yield return new WaitForSeconds(2.5f);
        var lines = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < 4; i++)
        {
            clerk.Interact();
            yield return new WaitForSeconds(.5f);
            if (bubble.IsShowing) lines.Add(bubble.CurrentLine);
        }
        Expect(lines.Count >= 2, $"clerk: small talk varies ({lines.Count} lines in 4 talks)");
        log.AppendLine("      " + string.Join(" / ", lines));
        Camera talkCam = NewCamera(runner.transform, new Vector3(31.2f, 1.62f, -10.3f), new Vector3(30.0f, 1.6f, -13.3f), 62);
        yield return new WaitForEndOfFrame();
        Shoot(talkCam, "store", "clerk_talk", 2);
        Object.Destroy(talkCam.gameObject);

        Finish(log, ok, ng, asyncBefore);
        Object.Destroy(runner.gameObject);
    }

    static string Strip(string s) => s == null ? "" : System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", "");

    static void Finish(System.Text.StringBuilder log, int ok, int ng, bool asyncBefore)
    {
        ShaderUtil.allowAsyncCompilation = asyncBefore;
        File.WriteAllText(StoreCheckPath, $"store check: ok {ok}, ng {ng}\n" + log);
        Debug.Log($"[Store] check: ok {ok}, ng {ng}");
    }
}
#endif
