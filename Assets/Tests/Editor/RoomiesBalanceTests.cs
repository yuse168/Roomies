using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 数値まわりの回帰テスト（Play Mode不要）。
/// 夜イベントの難易度スケール・家賃の値上がり・効果音の音量表を見る。
///
/// ゲーム本体は Assembly-CSharp にあり、このテスト用アセンブリからは直接参照できないので、
/// 既存の `RoomiesRegressionTests` と同じくリフレクションで触る。
/// </summary>
public class RoomiesBalanceTests
{
    private readonly List<Object> created = new();

    private static Type GameType(string name) => Type.GetType(name + ", Assembly-CSharp", true);

    private ScriptableObject NewAsset(string typeName)
    {
        var asset = ScriptableObject.CreateInstance(GameType(typeName));
        created.Add(asset);
        return asset;
    }

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public)
            .SetValue(target, value);

    private static T Call<T>(object target, string method, params object[] args) =>
        (T)target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.Public)
            .Invoke(target, args);

    [TearDown]
    public void TearDown()
    {
        foreach (Object asset in created)
            if (asset != null) Object.DestroyImmediate(asset);
        created.Clear();
    }

    // ---- 家賃の値上がり ----

    [Test]
    public void RentGrowsEachCycleAndStopsAtMax()
    {
        ScriptableObject balance = NewAsset("GameBalance");
        Set(balance, "baseRent", 500);
        Set(balance, "rentIncreasePerCycle", 150);
        Set(balance, "maxRent", 800);

        Assert.That(Call<int>(balance, "RentForCycle", 1), Is.EqualTo(500), "1周目は基本額のまま");
        Assert.That(Call<int>(balance, "RentForCycle", 2), Is.EqualTo(650));
        Assert.That(Call<int>(balance, "RentForCycle", 3), Is.EqualTo(800));
        Assert.That(Call<int>(balance, "RentForCycle", 10), Is.EqualTo(800), "上限を超えない");
    }

    [Test]
    public void RentForCycleHandlesZeroAndNegativeCycles()
    {
        ScriptableObject balance = NewAsset("GameBalance");
        Set(balance, "baseRent", 500);
        Set(balance, "rentIncreasePerCycle", 150);
        Set(balance, "maxRent", 2000);

        Assert.That(Call<int>(balance, "RentForCycle", 0), Is.EqualTo(500));
        Assert.That(Call<int>(balance, "RentForCycle", -5), Is.EqualTo(500));
    }

    // ---- 夜イベントの難易度 ----

    [Test]
    public void NightAmountScaleRisesWithDayAndIsCapped()
    {
        ScriptableObject balance = NewAsset("GameBalance");
        Set(balance, "nightAmountScalePerDay", 0.3f);
        Set(balance, "nightAmountScaleMax", 2.5f);

        Assert.That(Call<float>(balance, "NightScale", 1), Is.EqualTo(1f).Within(0.001f), "Day1は等倍");
        Assert.That(Call<float>(balance, "NightScale", 3), Is.EqualTo(1.6f).Within(0.001f));
        Assert.That(Call<float>(balance, "NightScale", 99), Is.EqualTo(2.5f).Within(0.001f), "上限で止まる");
    }

    [Test]
    public void PeaceWeightFallsButNeverReachesZero()
    {
        ScriptableObject balance = NewAsset("GameBalance");
        Set(balance, "peaceWeightDay1", 25);
        Set(balance, "peaceWeightDropPerDay", 7);

        Assert.That(Call<int>(balance, "PeaceWeight", 1), Is.EqualTo(25));
        Assert.That(Call<int>(balance, "PeaceWeight", 2), Is.EqualTo(18));
        Assert.That(Call<int>(balance, "PeaceWeight", 3), Is.EqualTo(11));
        Assert.That(Call<int>(balance, "PeaceWeight", 50), Is.EqualTo(6), "平和な夜が完全には消えない");
    }

    // ---- 効果音の音量表 ----

    [Test]
    public void SfxLevelsReturnsOneForUnknownKeysAndClampsNegatives()
    {
        ScriptableObject levels = NewAsset("SfxLevels");
        Type entryType = GameType("SfxLevels+Entry");

        Array entries = Array.CreateInstance(entryType, 2);
        entries.SetValue(MakeEntry(entryType, "ui/confirm", 0.5f), 0);
        entries.SetValue(MakeEntry(entryType, "broken", -3f), 1);
        Set(levels, "entries", entries);

        Assert.That(Call<float>(levels, "Get", "ui/confirm"), Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(Call<float>(levels, "Get", "ui/unknown"), Is.EqualTo(1f).Within(0.001f),
            "表に無い音も鳴る");
        Assert.That(Call<float>(levels, "Get", (string)null), Is.EqualTo(1f).Within(0.001f));
        Assert.That(Call<float>(levels, "Get", "broken"), Is.EqualTo(0f).Within(0.001f),
            "マイナスの音量は0に丸める");
    }

    private static object MakeEntry(Type entryType, string key, float volume)
    {
        object entry = Activator.CreateInstance(entryType);
        entryType.GetField("key").SetValue(entry, key);
        entryType.GetField("volume").SetValue(entry, volume);
        return entry;
    }
}
