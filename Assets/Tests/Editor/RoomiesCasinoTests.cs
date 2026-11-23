using System;
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
}
