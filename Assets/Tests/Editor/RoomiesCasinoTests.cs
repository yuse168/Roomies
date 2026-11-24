using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// カジノの数値の回帰テスト（Play Mode不要）。
/// 「遊ぶほど増える台」に戻っていないかを、プレハブに保存された値から計算して確かめる。
///
/// ゲーム本体は Assembly-CSharp にあるので、ほかのテストと同じくリフレクションで触る。
/// </summary>
public class RoomiesCasinoTests
{
    private static Type GameType(string name) => Type.GetType(name + ", Assembly-CSharp", true);

    private static Component LoadComponent(string prefabPath, string typeName)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.That(prefab, Is.Not.Null, prefabPath + " が無い");
        Component c = prefab.GetComponentInChildren(GameType(typeName), true);
        Assert.That(c, Is.Not.Null, prefabPath + " に " + typeName + " が無い");
        return c;
    }

    private static T Field<T>(object target, string name) =>
        (T)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .GetValue(target);

    private static float SlotReturn(string prefabPath)
    {
        Component slot = LoadComponent(prefabPath, "SlotMachine");
        return (float)slot.GetType().GetMethod("ExpectedReturn").Invoke(slot, null);
    }

    [Test]
    public void FirstFloorSlotReturnsLessThanTheBet()
    {
        float rtp = SlotReturn("Assets/_Prefabs/Slot.prefab");
        Assert.That(rtp, Is.InRange(0.93f, 0.98f),
            $"1 階のスロットの戻る割合は 96% 前後のはず（いま {rtp * 100f:F1}%）");
    }

    [Test]
    public void SecondFloorSlotReturnsMoreThanFirstFloorButLessThanTheBet()
    {
        float first = SlotReturn("Assets/_Prefabs/Slot.prefab");
        float second = SlotReturn("Assets/_Prefabs/Casino/Slot_2F.prefab");
        Assert.That(second, Is.GreaterThan(first), "上の階ほど戻る割合が高い");
        Assert.That(second, Is.InRange(0.97f, 0.995f),
            $"2 階のスロットの戻る割合は 98% 前後のはず（いま {second * 100f:F1}%）");
    }

    [Test]
    public void UpperFloorBlackjackTablesHaveHigherStakesAndBetterRules()
    {
        Component first = LoadComponent("Assets/_Prefabs/BlackjackTable.prefab", "BlackjackTable");
        Component second = LoadComponent("Assets/_Prefabs/Casino/BlackjackTable_2F.prefab", "BlackjackTable");
        Component third = LoadComponent("Assets/_Prefabs/Casino/BlackjackTable_3F.prefab", "BlackjackTable");

        Assert.That(Field<bool>(first, "allowDoubleDown"), Is.False, "1 階はダブルダウンなし");
        Assert.That(Field<bool>(second, "allowDoubleDown"), Is.True);
        Assert.That(Field<bool>(third, "allowDoubleDown"), Is.True);

        Assert.That(Field<float>(first, "blackjackReturnMultiplier"), Is.EqualTo(2.5f).Within(0.001f));
        Assert.That(Field<float>(second, "blackjackReturnMultiplier"), Is.EqualTo(2.5f).Within(0.001f));
        Assert.That(Field<float>(third, "blackjackReturnMultiplier"), Is.EqualTo(3f).Within(0.001f),
            "3 階はブラックジャックで賭け金の 3 倍が戻る");

        Assert.That(Field<int[]>(first, "betAmounts"), Is.EqualTo(new[] { 10, 50, 100 }));
        Assert.That(Field<int[]>(second, "betAmounts"), Is.EqualTo(new[] { 100, 500, 1000 }));
        Assert.That(Field<int[]>(third, "betAmounts"), Is.EqualTo(new[] { 500, 2000, 5000 }));
    }

    // ---- ルーレット ----

    private static float RouletteReturn(string kindName, bool zeroRefund)
    {
        Type rules = GameType("RouletteRules");
        object kind = Enum.Parse(GameType("RouletteBetKind"), kindName);
        return (float)rules.GetMethod("ExpectedReturn").Invoke(null, new[] { kind, (object)zeroRefund });
    }

    [Test]
    public void RouletteEveryBetReturnsThirtySixOverThirtySeven()
    {
        foreach (string kind in new[] { "Number", "Red", "Black", "Odd", "Even", "Low", "High", "Dozen1", "Dozen2", "Dozen3", "Column1", "Column2", "Column3" })
            Assert.That(RouletteReturn(kind, false), Is.EqualTo(36f / 37f).Within(0.0001f), kind + " は 97.3% のはず");
    }

    [Test]
    public void RouletteThirdFloorRefundsEvenMoneyBetsOnZero()
    {
        foreach (string kind in new[] { "Red", "Black", "Odd", "Even", "Low", "High" })
            Assert.That(RouletteReturn(kind, true), Is.EqualTo(1f).Within(0.0001f), kind + " は 0 の返金で 100% になる");
        foreach (string kind in new[] { "Number", "Dozen1", "Dozen2", "Dozen3", "Column1", "Column2", "Column3" })
            Assert.That(RouletteReturn(kind, true), Is.EqualTo(36f / 37f).Within(0.0001f), kind + " は返金なしのまま");
    }

    [Test]
    public void RouletteWheelHasEveryNumberOnceAndRedsAreEighteen()
    {
        Type rules = GameType("RouletteRules");
        int[] order = (int[])rules.GetField("WheelOrder").GetValue(null);
        Assert.That(order.Length, Is.EqualTo(37));
        Assert.That(new System.Collections.Generic.HashSet<int>(order).Count, Is.EqualTo(37), "盤の数字に重なりがない");
        int reds = 0;
        for (int n = 1; n <= 36; n++)
            if ((bool)rules.GetMethod("IsRed").Invoke(null, new object[] { n })) reds++;
        Assert.That(reds, Is.EqualTo(18));
    }

    [Test]
    public void RoulettePrefabHasAllSpotsAndThirdFloorRules()
    {
        Component second = LoadComponent("Assets/_Prefabs/Casino/RouletteTable.prefab", "RouletteTable");
        Component third = LoadComponent("Assets/_Prefabs/Casino/RouletteTable_3F.prefab", "RouletteTable");
        var so = new SerializedObject(second);
        SerializedProperty spots = so.FindProperty("spots");
        Assert.That(spots.arraySize, Is.EqualTo(49), "数字 37 ＋ 外側 12（2 倍の賭け 6・3 つ分け 3・列 3）");
        for (int i = 0; i < spots.arraySize; i++)
            Assert.That(spots.GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null, "賭ける場所 " + i);
        Assert.That(Field<bool>(second, "zeroRefundsEvenMoney"), Is.False);
        Assert.That(Field<bool>(third, "zeroRefundsEvenMoney"), Is.True, "3 階は 0 で 2 倍の賭けを返金");
        Assert.That(Field<int[]>(second, "betAmounts"), Is.EqualTo(new[] { 100, 500, 1000 }));
        Assert.That(Field<int[]>(third, "betAmounts"), Is.EqualTo(new[] { 500, 2000, 5000 }));
    }

    // ---- ハイ＆ロー・チンチロ ----

    [Test]
    public void HiLoEveryGuessReturnsTheSameRate()
    {
        Type rules = GameType("HiLoRules");
        MethodInfo winning = rules.GetMethod("WinningRanks"), multiplier = rules.GetMethod("Multiplier");
        for (int card = 1; card <= 13; card++)
            foreach (bool high in new[] { true, false })
            {
                int k = (int)winning.Invoke(null, new object[] { card, high });
                float m = (float)multiplier.Invoke(null, new object[] { card, high, 0.995f });
                if (k == 0) { Assert.That(m, Is.EqualTo(0f), "出ない向きには賭けられない"); continue; }
                Assert.That(k / 13f * m, Is.EqualTo(0.995f).Within(0.0001f), $"カード {card} の{(high ? "上" : "下")}");
            }
    }

    [Test]
    public void HiLoPrefabStepRateIsJustUnderOne()
    {
        Component table = LoadComponent("Assets/_Prefabs/Casino/HiLoTable.prefab", "HiLoTable");
        float rate = Field<float>(table, "stepReturnRate");
        int steps = Field<int>(table, "maxSteps");
        Assert.That(rate, Is.InRange(0.98f, 1.0f), "1 回ごとの戻る割合");
        Assert.That(Mathf.Pow(rate, steps), Is.GreaterThan(0.96f), "最後まで続けても 96% を下回らない");
        Assert.That(Field<int[]>(table, "betAmounts"), Is.EqualTo(new[] { 100, 500, 1000 }));
    }

    [Test]
    public void HiLoPrefabHasCardsAndChipsOnTheTable()
    {
        Component table = LoadComponent("Assets/_Prefabs/Casino/HiLoTable.prefab", "HiLoTable");
        var view = Field<Component>(table, "view");
        Assert.That(view, Is.Not.Null, "HiLoTable.view");
        Assert.That(view.gameObject, Is.SameAs(table.gameObject), "台の上の実物を動かす部品は、ルートに置く");
        int steps = Field<int>(table, "maxSteps");
        var cards = Field<Array>(view, "cards");
        Assert.That(cards.Length, Is.GreaterThanOrEqualTo(steps + 1), "1 枚目 ＋ 続けられる回数ぶんのカードが置いてある");
        foreach (object slot in cards)
        {
            var pivot = (Transform)slot.GetType().GetField("pivot").GetValue(slot);
            var face = (Renderer)slot.GetType().GetField("face").GetValue(slot);
            Assert.That(pivot, Is.Not.Null); Assert.That(face, Is.Not.Null);
            Assert.That(pivot.name, Does.StartWith("Pivot_Card_"));
            Assert.That(pivot.parent, Is.SameAs(table.transform), "カードの軸は、ルートの直下（Visual の外）");
            Assert.That(pivot.gameObject.activeSelf, Is.False, "ふだんは隠してある");
            Assert.That(face.transform.parent.name, Is.EqualTo("Visual"), "見た目は、軸の中の Visual");
        }
        foreach (string name in new[] { "anchorDeck", "anchorDiscard", "anchorRow", "anchorTray", "anchorBet", "anchorPayout", "pivotBet", "pivotPayout" })
            Assert.That(Field<Transform>(view, name), Is.Not.Null, name);
        Assert.That(Field<GameObject[]>(view, "betChips").Length, Is.GreaterThanOrEqualTo(3));
        Assert.That(Field<GameObject[]>(view, "payoutChips").Length, Is.GreaterThanOrEqualTo(3));
        // ブラックジャックと同じトランプのマテリアル
        Component bj = LoadComponent("Assets/_Prefabs/BlackjackTable.prefab", "BlackjackTableView");
        var bjCards = Field<Array>(bj, "playerCards");
        object bjSlot = bjCards.GetValue(0), hlSlot = cards.GetValue(0);
        var bjFace = (Renderer)bjSlot.GetType().GetField("face").GetValue(bjSlot);
        var hlFace = (Renderer)hlSlot.GetType().GetField("face").GetValue(hlSlot);
        Assert.That(hlFace.sharedMaterial, Is.SameAs(bjFace.sharedMaterial), "トランプの絵は、ブラックジャックと同じマテリアル");
        Assert.That(Field<Component>(table, "oddsText"), Is.Not.Null, "立て札の倍率の文字");
    }

    [Test]
    public void EveryCasinoSoundUsedInCodeHasAFileAndALevel()
    {
        // コードに書いてある "casino/〇〇" の音は、どれも Resources/Audio/casino/ にファイルがあり、音量の表（SfxLevels）にも行がある
        var keys = new System.Collections.Generic.SortedSet<string>();
        foreach (string file in System.IO.Directory.GetFiles("Assets/_Scripts", "*.cs", System.IO.SearchOption.AllDirectories))
        {
            if (file.Replace(System.IO.Path.DirectorySeparatorChar, '/').Contains("/Editor/")) continue;
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(System.IO.File.ReadAllText(file), "\"(casino/[a-z_0-9]+)\""))
                keys.Add(m.Groups[1].Value);
        }
        Assert.That(keys.Count, Is.GreaterThan(20));
        string levels = System.IO.File.ReadAllText("Assets/Resources/Audio/SfxLevels.asset");
        foreach (string key in keys)
        {
            Assert.That(Resources.Load<AudioClip>("Audio/" + key), Is.Not.Null, "音のファイルが無い: " + key);
            Assert.That(levels, Does.Contain(key), "音量の表（SfxLevels）に行が無い: " + key);
        }
        // 仮に使っていた音（決定音・エモートの音）を、カジノと NPC のコードが使っていない
        foreach (string file in new[] { "Assets/_Scripts/Casino/CasinoClerk.cs", "Assets/_Scripts/Casino/CasinoElevatorButton.cs", "Assets/_Scripts/Npc/NpcSpeechBubble.cs" })
            Assert.That(System.IO.File.ReadAllText(file), Does.Not.Contain("\"ui/"), file);
    }

    [Test]
    public void StorePrefabsAreWired()
    {
        // 売り場：売り場 8 つ（種類 0〜7 が 1 つずつ）。見て E を押す所は Visual の外。動かす物も Visual の外
        Component shop = LoadComponent("Assets/_Prefabs/Store/StoreShop.prefab", "StoreShop");
        var sections = Field<Array>(shop, "sections");
        Assert.That(sections.Length, Is.EqualTo(8));
        var kinds = new System.Collections.Generic.HashSet<int>();
        foreach (object sec in sections)
        {
            var c = (Collider)sec.GetType().GetField("collider").GetValue(sec);
            var anchor = (Transform)sec.GetType().GetField("anchor").GetValue(sec);
            Assert.That(c, Is.Not.Null); Assert.That(anchor, Is.Not.Null);
            Assert.That(c.transform.parent, Is.SameAs(shop.transform), "売り場の当たり判定はルートの直下（Visual の外）");
            Assert.That(c.name, Does.StartWith("Pick_"));
            kinds.Add((int)sec.GetType().GetField("kind").GetValue(sec));
        }
        Assert.That(kinds.Count, Is.EqualTo(8), "売っている物の種類が 1 つずつ");
        Assert.That(Field<Collider>(shop, "registerCollider"), Is.Not.Null);
        foreach (string slots in new[] { "flySlots", "counterItems" })
        {
            var arr = Field<Array>(shop, slots);
            Assert.That(arr.Length, Is.EqualTo(Field<int>(shop, "itemsPerSet")), slots);
            foreach (object slot in arr)
            {
                var pivot = (Transform)slot.GetType().GetField("pivot").GetValue(slot);
                var k = (GameObject[])slot.GetType().GetField("kinds").GetValue(slot);
                Assert.That(pivot.parent, Is.SameAs(shop.transform), "動かす軸はルートの直下");
                Assert.That(pivot.name, Does.StartWith("Pivot_"));
                Assert.That(k.Length, Is.EqualTo(8));
                Assert.That(pivot.gameObject.activeSelf, Is.False, "ふだんは隠してある");
            }
        }
        foreach (string pivot in new[] { "bagPivot", "drawerPivot", "receiptPivot" })
        {
            var t = Field<Transform>(shop, pivot);
            Assert.That(t, Is.Not.Null, pivot);
            Assert.That(t.parent, Is.SameAs(shop.transform), pivot);
        }
        Assert.That(shop.transform.Find("Visual"), Is.Not.Null);
        Assert.That(shop.transform.Find("Visual").GetComponentsInChildren<MonoBehaviour>(true).Length, Is.Zero, "Visual の中にスクリプトがない");
        // 値段は冷蔵庫の補充と同じ（店の側には値段を持たない）。合計を分けても合計は変わらない
        var split = (int[])GameType("StoreShop").GetMethod("SplitPrice").Invoke(null, new object[] { 300, 3 });
        Assert.That(split, Is.EqualTo(new[] { 100, 100, 100 }));
        var odd = (int[])GameType("StoreShop").GetMethod("SplitPrice").Invoke(null, new object[] { 301, 3 });
        Assert.That(odd.Sum(), Is.EqualTo(301));
        // 自動ドア：左右のとびらの軸と、感知の位置
        Component door = LoadComponent("Assets/_Prefabs/Store/StoreDoor.prefab", "StoreAutoDoor");
        foreach (string f in new[] { "leftDoor", "rightDoor", "sensor" }) Assert.That(Field<Transform>(door, f), Is.Not.Null, f);
        Assert.That(door.GetComponentsInChildren<Collider>(true).Length, Is.Zero, "とびらに当たり判定はない（通れなくならないように）");
        // 店の人：鍵は売らず、話すだけ。吹き出しがある
        Component clerk = LoadComponent("Assets/_Prefabs/Store/StoreClerk.prefab", "CasinoClerk");
        Assert.That(Field<int>(clerk, "sellsFloor"), Is.Zero);
        Assert.That(Field<Component>(clerk, "bubble"), Is.Not.Null);
        Assert.That(Field<string[]>(clerk, "smallTalk").Length, Is.GreaterThanOrEqualTo(6));
    }

    [Test]
    public void EveryStoreSoundUsedInCodeHasAFileAndALevel()
    {
        var keys = new System.Collections.Generic.SortedSet<string>();
        foreach (string file in System.IO.Directory.GetFiles("Assets/_Scripts/Store", "*.cs", System.IO.SearchOption.AllDirectories))
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(System.IO.File.ReadAllText(file), "\"(store/[a-z_0-9]+)\""))
                keys.Add(m.Groups[1].Value);
        Assert.That(keys.Count, Is.GreaterThanOrEqualTo(10));
        string levels = System.IO.File.ReadAllText("Assets/Resources/Audio/SfxLevels.asset");
        foreach (string key in keys)
        {
            Assert.That(Resources.Load<AudioClip>("Audio/" + key), Is.Not.Null, "音のファイルが無い: " + key);
            Assert.That(levels, Does.Contain(key), "音量の表（SfxLevels）に行が無い: " + key);
        }
    }

    [Test]
    public void RoomComputerPrefabAndDesktopAreWired()
    {
        // 部屋の PC：ルートに仕組みと当たり判定、Anchor_Screen、Visual。シーンに置いてある
        Component pc = LoadComponent("Assets/_Prefabs/Room/RoomComputer.prefab", "RoomComputer");
        Assert.That(pc.GetComponent<BoxCollider>(), Is.Not.Null, "見て E を押す当たり判定");
        Assert.That(pc.GetComponent("NetworkObject"), Is.Not.Null);
        var anchor = Field<Transform>(pc, "anchorScreen");
        Assert.That(anchor, Is.Not.Null);
        Assert.That(anchor.name, Is.EqualTo("Anchor_Screen"));
        Assert.That(pc.transform.Find("Visual"), Is.Not.Null);
        string room = System.IO.File.ReadAllText("Assets/_Scenes/GameRoom.unity");
        string ui = System.IO.File.ReadAllText("Assets/_Scenes/GameRoomUI.unity");
        string Guid(string script) => AssetDatabase.AssetPathToGUID(script);
        Assert.That(room, Does.Contain(Guid("Assets/_Prefabs/Room/RoomComputer.prefab")), "GameRoom に PC が置いてある");
        Assert.That(room, Does.Contain(Guid("Assets/_Scripts/Stocks/StockMarket.cs")), "GameRoom に株の市場が置いてある");
        Assert.That(ui, Does.Contain(Guid("Assets/_Scripts/UI/RoomComputerUI.cs")), "GameRoomUI に PC の画面が置いてある（実行時に作らない）");
        // 開いている間は、移動・操作・Esc メニュー・家具ショップのキーが止まる
        foreach (string file in new[] { "Assets/_Scripts/PlayerMovement.cs", "Assets/_Scripts/PlayerInteract.cs", "Assets/_Scripts/UI/EscMenuUI.cs", "Assets/_Scripts/Furniture/FurnitureEditController.cs" })
            Assert.That(System.IO.File.ReadAllText(file), Does.Contain("RoomComputerUI.IsOpen"), file);
        // 家具の注文は前からの仕組み（DayManager.BuyFurnitureServerRpc）を使う。二重に作らない
        string computerUi = System.IO.File.ReadAllText("Assets/_Scripts/UI/RoomComputerUI.cs");
        Assert.That(computerUi, Does.Contain("BuyFurnitureServerRpc"));
        Assert.That(computerUi, Does.Not.Contain("TrySpend"), "PC の画面はお金を直接動かさない（サーバーが決める）");
        Assert.That(computerUi, Does.Not.Contain("new GameObject("), "PC の画面は実行時に物を作らない");
        Assert.That(computerUi, Does.Not.Contain("Instantiate("), "PC の画面は実行時に物を作らない");
        // 音は PC の音（仮の ui/ の音を使わない）
        Assert.That(computerUi, Does.Not.Contain("\"ui/"));
        string levels = System.IO.File.ReadAllText("Assets/Resources/Audio/SfxLevels.asset");
        foreach (string key in new[] { "pc/open", "pc/close", "pc/click" })
        {
            Assert.That(Resources.Load<AudioClip>("Audio/" + key), Is.Not.Null, key);
            Assert.That(levels, Does.Contain(key), key);
        }
    }

    [Test]
    public void StockMarketRulesHold()
    {
        Type t = GameType("StockMarket");
        int Next(int price, float drift, float vol, float z, float nudge, int min, int max) =>
            (int)t.GetMethod("NextPrice").Invoke(null, new object[] { price, drift, vol, z, nudge, min, max });
        Assert.That(Next(100, 0f, 2f, 0f, 0f, 5, 5000), Is.EqualTo(100), "乱数 0・ドリフト 0 なら変わらない");
        Assert.That(Next(100, 0f, 2f, 1f, 0f, 5, 5000), Is.EqualTo(102), "1σ で約 2% 上がる");
        Assert.That(Next(100, 0f, 2f, -1f, 0f, 5, 5000), Is.EqualTo(98));
        Assert.That(Next(100, 0f, 2f, 0f, 10f, 5, 5000), Is.EqualTo(111), "ニュースの +10% が足される（e^0.1）");
        Assert.That(Next(6, 0f, 20f, -3f, -50f, 5, 5000), Is.EqualTo(5), "下限で止まる");
        Assert.That(Next(4900, 0f, 20f, 3f, 50f, 5, 5000), Is.EqualTo(5000), "上限で止まる");
        Assert.That(Next(100, 0f, 2f, 100f, 0f, 5, 5000), Is.EqualTo(Next(100, 0f, 2f, 3f, 0f, 5, 5000)), "乱数は ±3σ で切る");

        int Fee(int amount, float percent) => (int)t.GetMethod("Fee").Invoke(null, new object[] { amount, percent });
        Assert.That(Fee(1000, 1f), Is.EqualTo(10));
        Assert.That(Fee(150, 1f), Is.EqualTo(2), "1 円未満は切り上げ");
        Assert.That(Fee(0, 1f), Is.Zero);
        Assert.That(Fee(1000, 0f), Is.Zero);

        int Basis(int basis, int holding, int qty) => (int)t.GetMethod("BasisAfterSell").Invoke(null, new object[] { basis, holding, qty });
        Assert.That(Basis(1000, 10, 5), Is.EqualTo(500), "半分売れば、払った額も半分");
        Assert.That(Basis(1000, 10, 10), Is.Zero, "全部売れば 0");
        Assert.That(Basis(1000, 3, 1), Is.EqualTo(667));

        // 銘柄：4〜6 個、略号は重ならない、名前と値段がある
        var balance = ScriptableObject.CreateInstance(GameType("GameBalance"));
        var stocks = (Array)balance.GetType().GetField("stocks").GetValue(balance);
        Assert.That(stocks.Length, Is.InRange(4, 6));
        var codes = new System.Collections.Generic.HashSet<string>();
        foreach (object s in stocks)
        {
            string code = (string)s.GetType().GetField("code").GetValue(s);
            string name = (string)s.GetType().GetField("name").GetValue(s);
            int start = (int)s.GetType().GetField("startPrice").GetValue(s);
            Assert.That(codes.Add(code), Is.True, "略号が重なっている: " + code);
            Assert.That(name, Is.Not.Empty);
            Assert.That(start, Is.GreaterThan(0));
        }
        UnityEngine.Object.DestroyImmediate(balance);

        // お金の理由と、セーブの項目
        Assert.That(Enum.IsDefined(GameType("SharedMoneyReason"), "StockBuy"), Is.True);
        Assert.That(Enum.IsDefined(GameType("SharedMoneyReason"), "StockSell"), Is.True);
        var save = Activator.CreateInstance(GameType("RoomiesSaveData"));
        foreach (string f in new[] { "stockPrices", "stockHoldings", "stockCostBasis" })
            Assert.That(save.GetType().GetField(f).GetValue(save), Is.Not.Null, f);
        string json = JsonUtility.ToJson(save);
        Assert.That(json, Does.Contain("stockHoldings"));
        // 前のセーブ（株の項目なし）を読んでも壊れない
        var old = JsonUtility.FromJson("{\"version\":1,\"day\":2,\"sharedMoney\":500}", GameType("RoomiesSaveData"));
        Assert.That(((int[])old.GetType().GetField("stockHoldings").GetValue(old)).Length, Is.Zero);
    }

    [Test]
    public void StoreJobIsWiredAndChangeRulesHold()
    {
        // レジ打ちのバイト：見て押す所はトリガー（歩くのをじゃましない）。位置はアンカー、お客さんは Pivot、見た目は Visual の中
        Component shop = LoadComponent("Assets/_Prefabs/Store/StoreShop.prefab", "StoreShop");
        foreach (string f in new[] { "timeCardCollider", "tillCollider" })
        {
            var c = Field<Collider>(shop, f);
            Assert.That(c, Is.Not.Null, f);
            Assert.That(c.isTrigger, Is.True, f);
            Assert.That(c.transform.parent, Is.SameAs(shop.transform), f);
        }
        foreach (string f in new[] { "anchorCashier", "anchorJobExit", "anchorCustomerDoor", "anchorCustomerCounter" })
            Assert.That(Field<Transform>(shop, f).name, Does.StartWith("Anchor_"), f);
        var customer = Field<Transform>(shop, "customerPivot");
        Assert.That(customer.name, Is.EqualTo("Pivot_Customer"));
        Assert.That(customer.gameObject.activeSelf, Is.False, "お客さんは、ふだんは隠してある");
        var looks = Field<GameObject[]>(shop, "customerLooks");
        Assert.That(looks.Length, Is.GreaterThanOrEqualTo(3));
        foreach (var look in looks) Assert.That(look.transform.parent.name, Is.EqualTo("Visual"));
        Assert.That(Field<Component>(shop, "customerBubble"), Is.Not.Null);
        Assert.That(Field<int[]>(shop, "kindPrices").Length, Is.EqualTo(8), "お客さん用の値段が、売っている物の数だけある");
        Assert.That(Field<Component>(shop, "jobScreenText"), Is.Not.Null);

        // お客さんが出す額：合計以上のいちばん小さいお札。小銭を足すとお釣りがきりのいい額になる
        Type t = GameType("StoreShop");
        int Pay(int total, bool coins) => (int)t.GetMethod("PickPayment").Invoke(null, new object[] { total, new[] { 500, 1000, 2000, 5000 }, coins });
        Assert.That(Pay(210, false), Is.EqualTo(500));
        Assert.That(Pay(740, false), Is.EqualTo(1000));
        Assert.That(Pay(740, true), Is.EqualTo(1040));
        Assert.That(Pay(1000, true), Is.EqualTo(1000), "ちょうどのときは、そのまま");
        Assert.That(Pay(5600, false), Is.EqualTo(6000), "お札の表より大きいときは、千円単位");
        // お釣りの候補：3 つ、正しい額が 1 つだけ、どれも 0 以上
        MethodInfo make = t.GetMethod("MakeChangeOptions");
        for (int seed = 0; seed < 50; seed++)
            foreach (int change in new[] { 0, 10, 60, 300, 1290 })
            {
                var o = (int[])make.Invoke(null, new object[] { change, new System.Random(seed) });
                Assert.That(o.Length, Is.EqualTo(3));
                Assert.That(o.Count(v => v == change), Is.EqualTo(1));
                Assert.That(o.Distinct().Count(), Is.EqualTo(3));
                Assert.That(o.All(v => v >= 0), Is.True);
            }
        // バイト代の理由は「今日のおねがい」の稼ぎに数える
        Assert.That(Enum.IsDefined(GameType("SharedMoneyReason"), "StoreWage"), Is.True);
        Assert.That(System.IO.File.ReadAllText("Assets/_Scripts/Night/DailyRequestManager.cs"), Does.Contain("SharedMoneyReason.StoreWage"));
    }

    [Test]
    public void ChinchiroHandsAreRecognised()
    {
        Type rules = GameType("ChinchiroRules");
        MethodInfo evaluate = rules.GetMethod("Evaluate");
        string Hand(int a, int b, int c, out int point)
        {
            object[] args = { a, b, c, 0 };
            string name = evaluate.Invoke(null, args).ToString();
            point = (int)args[3];
            return name;
        }
        Assert.That(Hand(1, 1, 1, out _), Is.EqualTo("Pinzoro"));
        Assert.That(Hand(4, 4, 4, out _), Is.EqualTo("Triple"));
        Assert.That(Hand(6, 4, 5, out _), Is.EqualTo("Shigoro"));
        Assert.That(Hand(2, 3, 1, out _), Is.EqualTo("Hifumi"));
        Assert.That(Hand(3, 6, 3, out int p), Is.EqualTo("Point")); Assert.That(p, Is.EqualTo(6));
        Assert.That(Hand(2, 2, 5, out p), Is.EqualTo("Point")); Assert.That(p, Is.EqualTo(5));
        Assert.That(Hand(1, 3, 5, out _), Is.EqualTo("None"));
    }

    [Test]
    public void ChinchiroReturnsSlightlyMoreThanTheBet()
    {
        Component table = LoadComponent("Assets/_Prefabs/Casino/ChinchiroTable.prefab", "ChinchiroTable");
        float rtp = (float)table.GetType().GetMethod("ExpectedReturn").Invoke(table, null);
        Assert.That(rtp, Is.InRange(1.0f, 1.012f), $"3 階のチンチロの戻る割合は 100〜101% のはず（いま {rtp * 100f:F1}%）");
        Assert.That(Field<int[]>(table, "betAmounts"), Is.EqualTo(new[] { 500, 2000, 5000 }));
    }

    [Test]
    public void CasinoKeyPricesComeFromGameBalance()
    {
        ScriptableObject balance = ScriptableObject.CreateInstance(GameType("GameBalance"));
        try
        {
            balance.GetType().GetField("casinoKeyPrice2F").SetValue(balance, 1234);
            balance.GetType().GetField("casinoKeyPrice3F").SetValue(balance, 5678);
            MethodInfo price = balance.GetType().GetMethod("CasinoKeyPrice");
            Assert.That(price.Invoke(balance, new object[] { 2 }), Is.EqualTo(1234));
            Assert.That(price.Invoke(balance, new object[] { 3 }), Is.EqualTo(5678));
        }
        finally { UnityEngine.Object.DestroyImmediate(balance); }
    }

    [Test]
    public void OldSaveWithoutCasinoFieldsLoadsAsNoKeys()
    {
        // 鍵の項目を足す前のセーブ（項目が無い JSON）でも読めて、鍵は 0 本になる
        Type saveType = GameType("RoomiesSaveData");
        object data = JsonUtility.FromJson("{\"version\":1,\"day\":4,\"sharedMoney\":777}", saveType);
        Assert.That(saveType.GetField("day").GetValue(data), Is.EqualTo(4));
        Assert.That(saveType.GetField("casinoKeysBought").GetValue(data), Is.EqualTo(0));
        Assert.That(saveType.GetField("casinoKeysUsed").GetValue(data), Is.EqualTo(0));
        Assert.That(saveType.GetField("version").GetValue(data), Is.EqualTo(saveType.GetField("CurrentVersion").GetValue(null)),
            "鍵の項目を足しても版は上げない（上げると前のセーブが捨てられる）");
    }

    [Test]
    public void ElevatorPrefabHasThreeWiredFloors()
    {
        Component elevator = LoadComponent("Assets/_Prefabs/Casino/CasinoElevator.prefab", "CasinoElevator");
        var so = new SerializedObject(elevator);
        SerializedProperty floors = so.FindProperty("floors");
        Assert.That(floors.arraySize, Is.EqualTo(3));
        for (int i = 0; i < floors.arraySize; i++)
        {
            SerializedProperty f = floors.GetArrayElementAtIndex(i);
            Assert.That(f.FindPropertyRelative("cabinAnchor").objectReferenceValue, Is.Not.Null, $"{i + 1}階の Anchor_Cabin");
            Assert.That(f.FindPropertyRelative("doorLeft").objectReferenceValue, Is.Not.Null, $"{i + 1}階の Pivot_DoorL");
            Assert.That(f.FindPropertyRelative("doorRight").objectReferenceValue, Is.Not.Null, $"{i + 1}階の Pivot_DoorR");
        }
        // ボタンは階ごとに、呼ぶ 1・行き先 3・鍵穴 1
        Assert.That(elevator.GetComponentsInChildren(GameType("CasinoElevatorButton"), true).Length, Is.EqualTo(15));
    }

    [Test]
    public void ArcadeMachineReturnsLessThanItsPrice()
    {
        Component gimmick = LoadComponent("Assets/_Prefabs/NetworkFurniture.prefab", "FurnitureGimmick");
        int cost = Field<int>(gimmick, "arcadeCost");
        int max = Mathf.Max(cost, Field<int>(gimmick, "arcadeMaxPrize"));
        float miss = Field<float>(gimmick, "arcadeMissChance");

        // 景品は cost〜max の一様な整数を 10 円きざみに切り捨てたもの
        float sum = 0f;
        for (int v = cost; v <= max; v++) sum += v / 10 * 10;
        float rtp = (1f - miss) * (sum / (max - cost + 1)) / cost;

        Assert.That(rtp, Is.LessThan(1f),
            $"アーケードは遊ぶほど増えてはいけない（いま {rtp * 100f:F1}%）");
        Assert.That(rtp, Is.GreaterThan(0.7f), "減りすぎも遊ばれなくなる");
    }

    [Test]
    public void CasinoSoundsExist()
    {
        // スクリプトが鳴らすキーは、すべて Resources/Audio/casino/ に音がある（無いキーは黙って無音になるので、ここで確かめる）
        foreach (string key in new[]
                 {
                     "elevator_open", "elevator_close", "elevator_move", "elevator_ding",
                     "roulette_spin", "roulette_stop", "chip", "card", "dice", "win", "lose", "cash",
                     "slot_start", "slot_reel", "slot_stopa", "slot_stopb", "slot_stopc", "slot_reach", "slot_reach_miss", "slot_lose",
                     "slot_win_small", "slot_win_mid", "slot_win_big", "slot_jackpot", "slot_coin", "slot_tick", "chips_slide",
                 })
            Assert.That(Resources.Load<AudioClip>("Audio/casino/" + key), Is.Not.Null, "casino/" + key);
    }

    [Test]
    public void SlotScreenIsWiredInThePrefab()
    {
        // 画面の部品はプレハブに置いてあり、実行時には作らない。割り当てが切れていないかを確かめる
        Component screen = LoadComponent("Assets/_Prefabs/Slot.prefab", "SlotScreen");
        var so = new SerializedObject(screen);
        Assert.That(so.FindProperty("symbolSprites").arraySize, Is.EqualTo(5), "シンボルの絵は 5 つ（SlotMachine の slotSymbols と同じ数）");
        Assert.That(so.FindProperty("blurSprites").arraySize, Is.EqualTo(5));
        Assert.That(so.FindProperty("reelCells").arraySize, Is.EqualTo(15), "リール 3 本 × マス 5 つ");
        for (int i = 0; i < 15; i++)
            Assert.That(so.FindProperty("reelCells").GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null, "reelCells[" + i + "]");
        foreach (string name in new[] { "winLine", "flash", "sunburst", "logo", "reachFrame", "sweep", "winCounter", "feverBackground", "lampLight" })
            Assert.That(so.FindProperty(name).objectReferenceValue, Is.Not.Null, name);
        int strip = so.FindProperty("reelStrips").arraySize;
        Assert.That(strip % 3 == 0 && strip >= 15, "リールの並びは 3 本ぶん");
        // どのリールにも、5 つの絵がすべて入っている（入っていない絵は、そのリールで止められない）
        for (int reel = 0; reel < 3; reel++)
            for (int symbol = 0; symbol < 5; symbol++)
            {
                bool found = false;
                for (int k = 0; k < strip / 3; k++)
                    if (so.FindProperty("reelStrips").GetArrayElementAtIndex(reel * (strip / 3) + k).intValue == symbol) found = true;
                Assert.That(found, $"リール {reel + 1} に絵 {symbol} が無い");
            }

        Component machine = LoadComponent("Assets/_Prefabs/Slot.prefab", "SlotMachine");
        var ms = new SerializedObject(machine);
        foreach (string name in new[] { "screen", "resultText", "feverText", "betText" })
            Assert.That(ms.FindProperty(name).objectReferenceValue, Is.Not.Null, name);
    }

    [Test]
    public void BlackjackCardsAreWiredInThePrefab()
    {
        Component table = LoadComponent("Assets/_Prefabs/BlackjackTable.prefab", "BlackjackTable");
        Assert.That(new SerializedObject(table).FindProperty("view").objectReferenceValue, Is.Not.Null, "BlackjackTable.view");

        Component view = LoadComponent("Assets/_Prefabs/BlackjackTable.prefab", "BlackjackTableView");
        var so = new SerializedObject(view);
        foreach (string hand in new[] { "playerCards", "dealerCards" })
        {
            var cards = so.FindProperty(hand);
            Assert.That(cards.arraySize, Is.GreaterThanOrEqualTo(6), hand + "：6 枚以上（21 を超えない手の多くが収まる数）");
            for (int i = 0; i < cards.arraySize; i++)
            {
                Assert.That(cards.GetArrayElementAtIndex(i).FindPropertyRelative("pivot").objectReferenceValue, Is.Not.Null, $"{hand}[{i}].pivot");
                Assert.That(cards.GetArrayElementAtIndex(i).FindPropertyRelative("face").objectReferenceValue, Is.Not.Null, $"{hand}[{i}].face");
            }
        }
        foreach (string name in new[] { "anchorShoe", "anchorDiscard", "anchorPlayerHand", "anchorDealerHand", "pivotBet", "pivotDouble", "pivotPayout",
                     "anchorTray", "anchorBet", "anchorPayout", "playerScoreText", "dealerScoreText" })
            Assert.That(so.FindProperty(name).objectReferenceValue, Is.Not.Null, name);
    }

    [Test]
    public void BlackjackViewCountsVisibleCardsOnly()
    {
        Type view = GameType("BlackjackTableView");
        MethodInfo encode = view.GetMethod("Encode"), value = view.GetMethod("HandValue");
        char ace = (char)encode.Invoke(null, new object[] { 1, 0 }), king = (char)encode.Invoke(null, new object[] { 13, 2 }), nine = (char)encode.Invoke(null, new object[] { 9, 1 });

        object[] args = { "" + ace + king, false };
        Assert.That((int)value.Invoke(null, args), Is.EqualTo(21), "A + K = 21");
        Assert.That((bool)args[1], Is.False);

        args = new object[] { "" + ace + nine + king, false };
        Assert.That((int)value.Invoke(null, args), Is.EqualTo(20), "A + 9 + K = 20（A は 1 に数え直す）");

        args = new object[] { "" + nine + '#', false };
        Assert.That((int)value.Invoke(null, args), Is.EqualTo(9), "伏せ札は数えない");
        Assert.That((bool)args[1], Is.True, "伏せ札があることが分かる");
    }

    [Test]
    public void CasinoTextsHaveTheirGlyphs()
    {
        // カジノの画面に出す文字が、フォント（予備のフォントも含む）に入っているか。入っていない字は □ になる。
        // TextMeshPro は、このテストの集まりからは直接参照していないので、リフレクションで呼ぶ
        Type fontType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("TMPro.TMP_FontAsset")).First(t => t != null);
        Type settings = fontType.Assembly.GetType("TMPro.TMP_Settings");
        MethodInfo has = fontType.GetMethod("HasCharacter", new[] { typeof(char), typeof(bool), typeof(bool) });
        UnityEngine.Object black = AssetDatabase.LoadAssetAtPath("Assets/TextMesh Pro/Resources/Fonts & Materials/MPLUSU-Black SDF.asset", fontType);
        object fallback = settings.GetProperty("defaultFontAsset", BindingFlags.Static | BindingFlags.Public).GetValue(null);
        Assert.That(black, Is.Not.Null);
        Assert.That(fallback, Is.Not.Null);
        bool Has(object font, char c) => (bool)has.Invoke(font, new object[] { c, true, false });

        const string sample = "鍵階賭金額戻上下同数字外勝負引分枚開始残回目締切置赤黒奇偶ハイローチンチロルーレットエレベーター足りないでがご";
        foreach (char c in sample)
        {
            Assert.That(Has(black, c), "MPLUSU-Black（台の札・看板）に無い字: " + c);
            Assert.That(Has(fallback, c), "モニター・スロットの画面のフォントに無い字: " + c);
        }
        // トランプの印（ハイ＆ローのカードの文字）は、予備のフォントから出る
        foreach (char c in "♠♥♦♣")
            Assert.That(Has(black, c), "トランプの印が出ない: " + c);
    }

    [Test]
    public void NpcTalkIsWiredInThePrefabs()
    {
        // 店の人：吹き出しが割り当ててあり、せりふが数種類ある
        Component clerk = LoadComponent("Assets/_Prefabs/Casino/CasinoClerk.prefab", "CasinoClerk");
        var cs = new SerializedObject(clerk);
        Assert.That(cs.FindProperty("bubble").objectReferenceValue, Is.Not.Null, "CasinoClerk.bubble");
        Assert.That(cs.FindProperty("smallTalk").arraySize, Is.GreaterThanOrEqualTo(5), "店の人のせりふは 5 つ以上");

        // 警察：話しかける仕組みと吹き出しがルートにあり、巡回の部品とつながっている
        Component talk = LoadComponent("Assets/_Prefabs/Smuggling/Police_Placeholder.prefab", "PoliceTalk");
        var ts = new SerializedObject(talk);
        Assert.That(ts.FindProperty("patrol").objectReferenceValue, Is.Not.Null, "PoliceTalk.patrol");
        Assert.That(ts.FindProperty("bubble").objectReferenceValue, Is.Not.Null, "PoliceTalk.bubble");
        Assert.That(ts.FindProperty("lines").arraySize, Is.GreaterThanOrEqualTo(5), "警察のせりふは 5 つ以上");
        Assert.That(talk.transform.parent, Is.Null, "PoliceTalk はプレハブのルートに置く");

        foreach (string path in new[] { "Assets/_Prefabs/Casino/CasinoClerk.prefab", "Assets/_Prefabs/Smuggling/Police_Placeholder.prefab" })
        {
            Component bubble = LoadComponent(path, "NpcSpeechBubble");
            var bs = new SerializedObject(bubble);
            var anchor = bs.FindProperty("bubble").objectReferenceValue as Transform;
            Assert.That(anchor, Is.Not.Null, path + " の吹き出し");
            Assert.That(anchor.gameObject.activeSelf, Is.False, "吹き出しは、ふだんは隠してある");
            Assert.That(bs.FindProperty("text").objectReferenceValue, Is.Not.Null, path + " の吹き出しの文字");
        }
    }
}
