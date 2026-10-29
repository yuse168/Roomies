using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using static RoomiesPlayHarness;
using Object = UnityEngine.Object;

/// <summary>
/// ホスト1人で 1日目→夜→ベッドで朝→3日目の家賃→4日目、セーブ／続きから、ゲームオーバー までを通す。
/// 順番に依存する（Order）。1つのゲームを最初のテストで立ち上げ、後のテストはその続きを見る。
/// 手で見る部分（見た目・音・操作感）は docs/playmode-check-20261029.md の手順書で確認する。
/// </summary>
[TestFixture]
public class RoomiesPlayLoopTests : IPrebuildSetup, IPostBuildCleanup
{
#if UNITY_EDITOR
    // BootSceneBuilder がエディターの Play 開始シーンを BootScene に固定している。
    // そのままだとテストのシーンでなくタイトルが起動してテストが始まらないので、テストのあいだだけ外す。
    const string StartScenePrefKey = "Roomies.PlayModeTests.StartScene";
    public void Setup()
    {
        var start = UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene;
        UnityEditor.SessionState.SetString(StartScenePrefKey, start != null ? UnityEditor.AssetDatabase.GetAssetPath(start) : "");
        UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = null;
    }

    public void Cleanup()
    {
        string path = UnityEditor.SessionState.GetString(StartScenePrefKey, "");
        if (!string.IsNullOrEmpty(path))
            UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(path);
    }
#else
    public void Setup() { }
    public void Cleanup() { }
#endif

    static bool booted;
    static readonly List<string> errors = new();
    static float savedTimeScale = 1f;

    static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add($"[{type}] {message}\n{stack?.Split('\n').FirstOrDefault()}");
    }

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // エラーログで個々のテストを落とさず、最後のテストでまとめて出す（どこで出たかはログの順番で分かる）
        LogAssert.ignoreFailingMessages = true;
        Application.logMessageReceived += OnLog;
        BackupRealSave();
        savedTimeScale = Time.timeScale;
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Application.logMessageReceived -= OnLog;
        Time.timeScale = savedTimeScale;
        if (Network != null) { Network.Shutdown(); Object.Destroy(Network.gameObject); }
        RestoreRealSave();
        Call(GameType("SaveSystem"), "ClearPendingLoad");
    }

    IEnumerator EnsureBooted()
    {
        if (booted) yield break;
        booted = true;
        yield return BootHost();
    }

    [UnityTest, Order(1)]
    public IEnumerator Boot_GameRoomAndUiAreLoadedOnce()
    {
        yield return EnsureBooted();
        Assert.That(LoadedSceneCount("GameRoom"), Is.EqualTo(1));
        Assert.That(LoadedSceneCount("GameRoomUI"), Is.EqualTo(1), "UIシーンは1つだけ");
        Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude).Length, Is.EqualTo(1), "EventSystem は1つ");
        Assert.That(CurrentDay, Is.EqualTo(1));
        Assert.That(IsNight, Is.False);
        Assert.That(IsGameOver, Is.False);
        var ui = Instance("GameUIRefs");
        StringAssert.Contains("DAY 1", Text((Component)Get(ui, "DayText")));
        Capture("pm_01_day1_morning_hud");
    }

    [UnityTest, Order(2)]
    public IEnumerator Money_SharedAccountAndHudFollow()
    {
        yield return EnsureBooted();
        var money = Instance("SharedMoneyManager");
        int before = Money;
        var reason = Enum.Parse(GameType("SharedMoneyReason"), "DebugGrant");
        Assert.That(Call(money, "TryAdd", 250, reason, "playmode-test"), Is.True);
        Assert.That(Money, Is.EqualTo(before + 250));
        Assert.That(Call(money, "TrySpend", 100, reason, "playmode-test"), Is.True);
        Assert.That(Money, Is.EqualTo(before + 150));
        yield return null; yield return null;
        string expected = (before + 150).ToString("N0");
        var ui = Instance("GameUIRefs");
        StringAssert.Contains(expected, Text((Component)Get(ui, "SharedMoneyText")), "共同口座の文字");
        var hud = Object.FindAnyObjectByType(GameType("HudThemer"));
        Assert.That(hud, Is.Not.Null, "HudThemer がある");
        StringAssert.Contains(expected, Text((Component)Get(hud, "moneyValueLabel")), "HUD の所持金");
        // 足りない額は引けない（残高はそのまま）
        Assert.That(Call(money, "TrySpend", Money + 1, reason, "playmode-test"), Is.False);
        Assert.That(Money, Is.EqualTo(before + 150));
    }

    [UnityTest, Order(3)]
    public IEnumerator DailyRequest_IsPickedAndShownOnHud()
    {
        yield return EnsureBooted();
        var req = Instance("DailyRequestManager");
        Assert.That(req, Is.Not.Null, "DailyRequestManager がある");
        string kind = Get(req, "Kind").ToString();
        Assert.That(kind, Is.EqualTo("EarnMoney").Or.EqualTo("DeliverCount"), "1日目は稼ぐ／配達のどちらか");
        yield return null;
        var hud = Object.FindAnyObjectByType(GameType("HudThemer"));
        string objective = Text((Component)Get(hud, "objectiveLabel"));
        string status = (string)Get(req, "StatusText");
        TestContext.WriteLine($"おねがい: {kind} / {status} / HUD: {objective}");
        if (!(bool)Get(req, "IsDone"))
            StringAssert.Contains(status, objective, "HUD の目標に今日のおねがいが出る");
    }

    [UnityTest, Order(4)]
    public IEnumerator Night_ArrivesAndEventBannerPlays()
    {
        yield return EnsureBooted();
        Time.timeScale = 1f;
        yield return GoToNight();
        Assert.That(IsNight, Is.True);
        StringAssert.Contains("夜", Text((Component)Get(Instance("GameUIRefs"), "DayText")));
        var bannerUI = Get(Instance("GameUIRefs"), "NightEventUI");
        Assert.That(bannerUI, Is.Not.Null);
        // 夜のイベントは夜になった直後にサーバーが抽選して全員にバナーを出す
        yield return WaitUntil(() => (bool)Get(bannerUI, "playing"), 6f, "夜のバナーが出る");
        yield return new WaitForSecondsRealtime(0.8f);
        string title = Text((Component)Get(bannerUI, "titleLabel"));
        TestContext.WriteLine("夜のバナー: " + title + " / " + Text((Component)Get(bannerUI, "bodyLabel")));
        Assert.That(title, Is.Not.Null.And.Not.Empty, "見出しが出る");
        Capture("pm_02_day1_night_banner");
    }

    [UnityTest, Order(5)]
    public IEnumerator Bed_SleepingAloneEndsTheNight()
    {
        yield return EnsureBooted();
        if (!IsNight) yield return GoToNight();
        var beds = Object.FindObjectsByType(GameType("Bed"), FindObjectsInactive.Exclude);
        Assert.That(beds.Length, Is.EqualTo(5), "ベッドは5台");
        var bed = (Component)beds[0];
        var player = Network.LocalClient.PlayerObject;
        var movement = player.GetComponent(GameType("PlayerMovement"));
        Call(movement, "TeleportTo", bed.transform.position + bed.transform.forward * 0.8f + Vector3.up * 0.2f, Quaternion.identity);
        yield return null; yield return null;
        Assert.That(Call(bed, "CanInteract"), Is.True, "夜・ベッドの近くなら寝られる");
        Call(bed, "Interact");
        yield return WaitUntil(() => (bool)Get(bed, "IsOccupied"), 3f, "ベッドに寝る");
        Time.timeScale = 4f; // 朝の演出を早送り
        yield return WaitUntil(() => CurrentDay == 2 && !IsNight && !IsTransitioning, 60f, "全員（1人）が寝て2日目の朝");
        Time.timeScale = 1f;
        Assert.That((bool)Get(bed, "IsOccupied"), Is.False, "朝になったら起きる");
        Assert.That(IsGameOver, Is.False);
        Capture("pm_03_day2_morning");
    }

    [UnityTest, Order(6)]
    public IEnumerator Rent_Day3IsPaidOnceAndDay4Starts()
    {
        yield return EnsureBooted();
        Call(Instance("SharedMoneyManager"), "ServerRestoreBalance", 5000);
        Time.timeScale = 4f;
        // 2日目の夜 → 3日目の朝（家賃なし）
        yield return GoToNight();
        yield return SkipNightAndWaitMorning(3);
        Assert.That(IsGameOver, Is.False);
        // 3日目の夜 → 家賃 → 4日目
        Call(Instance("SharedMoneyManager"), "ServerRestoreBalance", 5000);
        yield return GoToNight();
        yield return new WaitForSecondsRealtime(0.5f); // 夜のイベントの入出金が先に済むのを待つ
        int rent = Get<int>(Day, "CurrentRentTotal");
        int before = Money;
        TestContext.WriteLine($"3日目の夜: 家賃 {rent} / 残高 {before}");
        Assert.That(rent, Is.GreaterThan(0));
        yield return SkipNightAndWaitMorning(4);
        Time.timeScale = 1f;
        Assert.That(IsGameOver, Is.False, "払えたのでゲームオーバーにならない");
        Assert.That(CurrentDay, Is.EqualTo(4));
        Assert.That(Money, Is.EqualTo(before - rent), "家賃は1回だけ引かれる");
        Capture("pm_04_day4_after_rent");
    }

    [UnityTest, Order(7)]
    public IEnumerator Save_AutosaveHasTheCurrentState()
    {
        yield return EnsureBooted();
        var save = GameType("SaveSystem");
        yield return WaitUntil(() =>
        {
            var d = Call(save, "Load");
            return d != null && Get<int>(d, "day") == CurrentDay && Get<int>(d, "sharedMoney") == Money;
        }, 8f, "オートセーブが今の日付・残高になる");
        var data = Call(save, "Load");
        TestContext.WriteLine($"セーブ: day={Get(data, "day")} time={Get(data, "time")} money={Get(data, "sharedMoney")} host={Get(data, "hostName")}");
    }

    [UnityTest, Order(8)]
    public IEnumerator Load_ContinueRestoresDayMoneyAndFurniture()
    {
        yield return EnsureBooted();
        var save = GameType("SaveSystem");
        var data = Call(save, "Load");
        Assert.That(data, Is.Not.Null);
        // 読み込みで戻ることが分かるよう、今と違う値にしてから「続きから」と同じ手順で始め直す
        data.GetType().GetField("day").SetValue(data, 2);
        data.GetType().GetField("time").SetValue(data, 0);
        data.GetType().GetField("sharedMoney").SetValue(data, 4321);
        var furnitureList = (IList)data.GetType().GetField("furniture").GetValue(data);
        var saved = Activator.CreateInstance(furnitureList.GetType().GetGenericArguments()[0]);
        saved.GetType().GetField("catalogIndex").SetValue(saved, 0);
        saved.GetType().GetField("position").SetValue(saved, new Vector3(17f, 3.4f, 18f));
        saved.GetType().GetField("yaw").SetValue(saved, 90f);
        furnitureList.Clear();
        furnitureList.Add(saved);

        yield return ShutdownToEmptyScene();
        yield return BootHost(data);
        yield return WaitUntil(() => CurrentDay == 2 && Money == 4321, 15f, "セーブの日付・残高に戻る");
        Assert.That(IsNight, Is.False);
        yield return WaitUntil(() => Object.FindObjectsByType(GameType("NetworkFurniture"), FindObjectsInactive.Exclude).Length == 1, 10f, "家具1つが戻る");
        Assert.That(Get(save, "PendingLoad"), Is.Null, "読み込み後は待ちのデータが消える");
        Capture("pm_05_loaded_day2");
    }

    [UnityTest, Order(9)]
    public IEnumerator GameOver_RentUnpaidShowsScreenAndDeletesSave()
    {
        yield return EnsureBooted();
        var save = GameType("SaveSystem");
        Time.timeScale = 4f;
        if (CurrentDay < 3)
        {
            yield return GoToNight();
            yield return SkipNightAndWaitMorning(3);
        }
        yield return GoToNight();
        yield return new WaitForSecondsRealtime(0.5f);
        Call(Instance("SharedMoneyManager"), "ServerRestoreBalance", 0);
        Call(Day, "ServerSkipToMorning");
        yield return WaitUntil(() => IsGameOver, 60f, "家賃が払えずゲームオーバー");
        Time.timeScale = 1f;
        Assert.That(Money, Is.EqualTo(0), "払えないときは残高を減らさない");
        var screen = Instance("GameOverScreen");
        Assert.That(screen, Is.Not.Null);
        yield return WaitUntil(() => ((GameObject)Get(screen, "root")).activeInHierarchy, 10f, "ゲームオーバー画面が出る");
        StringAssert.Contains("GAME OVER", Text((Component)Get(Instance("GameUIRefs"), "DayText")));
        yield return WaitUntil(() => !(bool)Call(save, "Exists"), 6f, "負けたらセーブが消える");
        Capture("pm_06_game_over");
    }

    [Test, Order(100)]
    public void NoErrorsWereLogged()
    {
        if (errors.Count > 0)
            Assert.Fail($"エラーログ {errors.Count} 件:\n" + string.Join("\n", errors.Distinct().Take(30)));
    }
}
