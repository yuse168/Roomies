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
