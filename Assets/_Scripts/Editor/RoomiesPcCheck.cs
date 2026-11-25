#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 部屋の PC の通し確認（Play Mode）：Roomies/PC/Check (Play Mode)。先に Town/Walk - Enter GameRoom As Host で入っておく。
/// PC を開く → 家具ショップで 1 つ注文 → 株を買って売る → 閉じる。お金・家具・株が本当に動く（セーブの控えを取ってから Play Mode にし、あとで戻すこと）。
/// Game ビューを 1920×1080 にして、画面全体（HUD・PC の画面も含む）を Temp/RoomiesPc/shots/ に撮る。結果は Temp/RoomiesPc/check.txt。
/// </summary>
internal static partial class RoomiesCasinoDemo
{
    const string PcDir = "Temp/RoomiesPc";

    [MenuItem("Roomies/PC/Check (Play Mode)")]
    static void PcCheck()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[PC] Play Mode で実行してください"); return; }
        var runner = NewRunner();
        runner.StartCoroutine(PcCheckRoutine(runner));
    }

    static IEnumerator Snap(string name)
    {
        Directory.CreateDirectory(PcDir + "/shots");
        string path = Path.GetFullPath($"{PcDir}/shots/{name}.png");
        File.Delete(path);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(path);
        for (int i = 0; i < 3; i++) yield return null;
    }

    static IEnumerator WaitUntil(System.Func<bool> done, float seconds)
    {
        for (float t = 0; t < seconds && !done(); t += Time.unscaledDeltaTime) yield return null;
    }

    static IEnumerator PcCheckRoutine(Runner runner)
    {
        Directory.CreateDirectory(PcDir);
        File.Delete(PcDir + "/check.txt");
        if (Directory.Exists(PcDir + "/shots")) foreach (var f in Directory.GetFiles(PcDir + "/shots")) try { File.Delete(f); } catch (IOException) { }
        var log = new System.Text.StringBuilder();
        int ok = 0, ng = 0;
        void Expect(bool good, string what) { if (good) ok++; else ng++; log.AppendLine((good ? "OK  " : "NG  ") + what); }
        int prevSize = SetGameViewSize(VW, VH);
        bool asyncBefore = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;

        var pc = Object.FindAnyObjectByType<RoomComputer>();
        var ui = GameUIRefs.Instance != null ? GameUIRefs.Instance.ComputerUI : null;
        var market = StockMarket.Instance;
        var money = SharedMoneyManager.Instance;
        var dm = DayManager.Instance;
        var r = MakeRig(runner);
        Expect(pc != null && pc.IsSpawned, "pc: RoomComputer in the room and spawned");
        Expect(ui != null, "pc: desktop UI found through GameUIRefs");
        Expect(market != null && market.IsSpawned && market.Count >= 4 && market.Count <= 6, $"stocks: market spawned with {(market != null ? market.Count : 0)} stocks");
        if (pc == null || ui == null || market == null) { Done(); yield break; }
        if (DailyRequestManager.Instance != null)
            ((NetworkVariable<bool>)typeof(DailyRequestManager).GetField("done", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(DailyRequestManager.Instance)).Value = true;
        if (money.CurrentMoney < 5000) money.TryAdd(5000, SharedMoneyReason.DebugGrant, "pc check");

        // ---- 机の前に立って、モニターを見る（夜：家具は夜だけ注文できる）
        SetTime(1);
        Vector3 front = pc.transform.TransformPoint(new Vector3(-.1f, 0, 1.05f));
        Teleport(r.cc, front);
        yield return WaitUntil(() => false, .5f);
        // 体を回すと目の位置も動くので、2 回向け直す。そのあと、プレイヤーの画面の案内（PlayerInteract が自分で当てたレイの結果）を読む
        for (int k = 0; k < 3; k++) { Aim(r, pc.AnchorScreen.position); yield return null; yield return null; }
        yield return WaitUntil(() => false, .3f);
        var interact = r.cc.GetComponentInParent<PlayerInteract>();
        string prompt = interact != null && interact.interactText != null && interact.interactText.gameObject.activeInHierarchy ? interact.interactText.text : "";
        Expect(prompt.Contains("パソコンを使う"), $"pc: looking at the monitor shows the prompt '{System.Text.RegularExpressions.Regex.Replace(prompt, "<[^>]+>", "")}'");
        Expect(pc.CanInteract() && pc.Label == "パソコンを使う", $"pc: label '{pc.Label}'");
        yield return Snap("0_room_pc");
        // 机の少しうしろから、モニターの待ち受け（Codex の絵と時計）を撮る
        Teleport(r.cc, pc.transform.TransformPoint(new Vector3(1.6f, 0, 2.4f)));   // 写真の手前をふさがないよう、少しよける
        Vector3 back = pc.transform.TransformPoint(new Vector3(.35f, 1.45f, 1.55f));
        Place(r, back, pc.AnchorScreen.position + Vector3.down * .05f, 46);
        UseCine(r, true);
        yield return WaitUntil(() => false, .6f);
        yield return Snap("0b_desk_monitor");
        UseCine(r, false);
        Teleport(r.cc, front);
        yield return WaitUntil(() => false, .3f);
        for (int k = 0; k < 3; k++) { Aim(r, pc.AnchorScreen.position); yield return null; yield return null; }
        var screenClock = pc.GetComponentsInChildren<TMPro.TMP_Text>(true).FirstOrDefault(t => t.name == "Clock");
        Expect(screenClock != null && screenClock.text.Contains(":") && pc.transform.Find("Visual/Screen") != null, $"desk: the monitor shows the standby picture and the clock ('{screenClock?.text}')");
        var restyle = UnityEngine.SceneManagement.SceneManager.GetSceneByName("GameRoom").GetRootGameObjects().FirstOrDefault(g => g.name == "Roomies_RoomRestyle");
        bool oldText = restyle != null && restyle.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t => t.text.Contains("バイト中") || t.text.Contains("稼ぎ"));
        Expect(restyle != null && !oldText, "desk: the old 'part-time job' text is gone from the monitor");

        // ---- 開く
        Vector3 before = r.cc.transform.position;
        int wallpaperBefore = ui.WallpaperIndex;
        pc.Interact();
        yield return WaitUntil(() => false, .25f);
        yield return Snap("1a_boot");
        yield return WaitUntil(() => false, .8f);
        Expect(RoomComputerUI.IsOpen && Cursor.lockState == CursorLockMode.None, "open: the desktop is shown and the cursor is free");
        Expect(!pc.CanInteract(), "open: the PC cannot be opened twice");
        yield return Snap("1_desktop");
        ui.NextWallpaper();
        yield return WaitUntil(() => false, .3f);
        Expect(ui.WallpaperIndex != wallpaperBefore, $"desktop: the wallpaper button switches the wallpaper ({wallpaperBefore} -> {ui.WallpaperIndex})");
        yield return Snap("1b_desktop_town");
        while (ui.WallpaperIndex != wallpaperBefore) ui.NextWallpaper();   // 元の壁紙に戻す（その PC に覚える値も）
        Expect((r.cc.transform.position - before).magnitude < .05f && !EscMenuUI.IsOpen, "open: the player does not move, the pause menu stays closed");

        // ---- 家具ショップで 1 つ注文
        ui.ShowApp(0);
        yield return WaitUntil(() => false, .4f);
        Expect(ui.OpenApp == 0, "shop: furniture shop app is open");
        yield return Snap("2_shop");
        int furnitureBefore = Object.FindObjectsByType<NetworkFurniture>().Length;
        int m0 = money.CurrentMoney;
        FurnitureItem item = FurnitureCatalog.Get(0);
        ui.BuyFurniture(0);
        yield return WaitUntil(() => ui.FurnitureStatusLine.Contains("届きました") || ui.FurnitureStatusLine.Contains("できません"), 4f);
        yield return WaitUntil(() => false, .3f);
        int furnitureAfter = Object.FindObjectsByType<NetworkFurniture>().Length;
        Expect(money.CurrentMoney == m0 - item.cost && furnitureAfter == furnitureBefore + 1,
            $"shop: bought '{item.displayName}' with the existing order flow (money {m0} -> {money.CurrentMoney}, furniture {furnitureBefore} -> {furnitureAfter}, '{ui.FurnitureStatusLine}')");
        yield return Snap("3_shop_bought");

        // ---- 家具の効果（前の O キーの一覧）：買った家具が並ぶ
        ui.ShowApp(2);
        yield return WaitUntil(() => false, .4f);
        Expect(ui.OpenApp == 2 && ui.EffectRowsShown >= 1, $"effects: the furniture effects app lists the placed furniture ({ui.EffectRowsShown} rows)");
        yield return Snap("3b_effects");

        // ---- 株：値動きを何回か進めて、買って、売る
        ui.ShowApp(1);
        ui.SelectStock(2);
        for (int i = 0; i < 14; i++) { market.ServerTick(); yield return null; }
        yield return WaitUntil(() => false, .4f);
        int[] hist = market.History(2);
        Expect(hist.Length == StockMarket.HistoryLength && hist.Distinct().Count() > 3, $"stocks: prices move on the host ({string.Join(",", hist.Skip(StockMarket.HistoryLength - 8))})");
        yield return Snap("4_stocks");
        int price = market.Price(2);
        int m1 = money.CurrentMoney;
        ui.Trade(true, 5);
        yield return WaitUntil(() => market.Holding(2) == 5, 3f);
        yield return WaitUntil(() => false, .3f);
        int expectedCost = price * 5 + StockMarket.Fee(price * 5, GameBalance.Current.stockFeePercent);
        Expect(market.Holding(2) == 5 && money.CurrentMoney == m1 - expectedCost && market.CostBasis(2) == expectedCost,
            $"stocks: bought 5 shares at ¥{price} (money {m1} -> {money.CurrentMoney}, expected -¥{expectedCost}; '{ui.StockStatusLine}')");
        for (int i = 0; i < 4; i++) { market.ServerTick(); yield return null; }
        yield return WaitUntil(() => false, .3f);
        yield return Snap("5_stocks_bought");
        int sellPrice = market.Price(2);
        int m2 = money.CurrentMoney;
        ui.Trade(false, 0);
        yield return WaitUntil(() => market.Holding(2) == 0, 3f);
        yield return WaitUntil(() => false, .3f);
        int expectedBack = sellPrice * 5 - StockMarket.Fee(sellPrice * 5, GameBalance.Current.stockFeePercent);
        Expect(market.Holding(2) == 0 && money.CurrentMoney == m2 + expectedBack && market.CostBasis(2) == 0,
            $"stocks: sold all 5 at ¥{sellPrice} (money {m2} -> {money.CurrentMoney}, expected +¥{expectedBack}; '{ui.StockStatusLine}')");
        yield return Snap("6_stocks_sold");
        ui.Trade(false, 1);
        yield return WaitUntil(() => ui.StockStatusLine.Length > 0 && !ui.StockStatusLine.Contains("注文を出して"), 3f);
        Expect(market.Holding(2) == 0, $"stocks: selling without shares is refused ('{ui.StockStatusLine}')");

        // ---- セーブ：持ち株と値段が書けて、戻せる
        market.ServerTick();
        ui.Trade(true, 1);
        yield return WaitUntil(() => market.Holding(2) == 1, 3f);
        var data = new RoomiesSaveData();
        market.ServerCapture(data);
        Expect(data.stockPrices.Length == market.Count && data.stockHoldings[2] == 1 && data.stockCostBasis[2] > 0, $"save: prices, holdings and cost are captured ({string.Join(",", data.stockHoldings)})");
        int[] savedPrices = (int[])data.stockPrices.Clone();
        for (int i = 0; i < 3; i++) market.ServerTick();
        market.ServerRestore(data.stockPrices, data.stockHoldings, data.stockCostBasis);
        yield return null;
        Expect(Enumerable.Range(0, market.Count).All(s => market.Price(s) == savedPrices[s]) && market.Holding(2) == 1, "save: restoring puts the prices and holdings back");
        ui.Trade(false, 0);
        yield return WaitUntil(() => market.Holding(2) == 0, 3f);

        // ---- 朝：家具は注文できない（前と同じ決まり）
        SetTime(0);
        ui.ShowApp(0);
        yield return WaitUntil(() => false, .5f);
        ui.BuyFurniture(1);
        yield return WaitUntil(() => false, .3f);
        Expect(ui.FurnitureStatusLine.Contains("夜"), $"shop: in the morning the order is refused ('{ui.FurnitureStatusLine}')");
        yield return Snap("7_shop_morning");

        // ---- 日本語の字が欠けていない（画面の文字が、フォントにすべてある）
        var missing = new System.Collections.Generic.HashSet<char>();
        foreach (var t in ui.GetComponentsInChildren<TMPro.TMP_Text>(true))
        {
            string plain = System.Text.RegularExpressions.Regex.Replace(t.text, "<[^>]+>", "");
            if (t.font != null && !t.font.HasCharacters(plain, out var miss, true, true) && miss != null) foreach (char c in miss) missing.Add(c);
        }
        Expect(missing.Count == 0, $"text: every character is in the font (missing '{new string(missing.ToArray())}')");

        // ---- 閉じる
        ui.Close();
        yield return WaitUntil(() => !RoomComputerUI.IsOpen, 1f);
        yield return WaitUntil(() => false, .2f);
        Expect(!RoomComputerUI.IsOpen && Cursor.lockState == CursorLockMode.Locked && !EscMenuUI.IsOpen, "close: back in the room, cursor locked, pause menu not opened");
        Expect(pc.CanInteract(), "close: the PC can be opened again");
        yield return Snap("8_closed");
        Done();

        void Done()
        {
            ShaderUtil.allowAsyncCompilation = asyncBefore;
            RestoreGameViewSize(prevSize);
            File.WriteAllText(PcDir + "/check.txt", $"pc check: ok {ok}, ng {ng}\n" + log);
            Object.Destroy(runner.gameObject);
        }
    }
}
#endif
