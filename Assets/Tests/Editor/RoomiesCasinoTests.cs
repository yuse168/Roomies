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
}
