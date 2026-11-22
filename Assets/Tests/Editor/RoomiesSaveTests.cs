using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// セーブ（続きから）のテスト。
// 置き場所は必ず一時フォルダへ向ける（本物のセーブ roomies_save.json には触らない）。
// ゲーム本体は Assembly-CSharp にあるので、ほかのテストと同じく Reflection で呼ぶ。
public class RoomiesSaveTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Public = BindingFlags.Instance | BindingFlags.Public;

    private readonly List<GameObject> objects = new();
    private NetworkManager network;
    private string tempDir;
    private string savePath;

    private static Type GameType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static Type SaveSystem => GameType("SaveSystem");

    private static object Static(string method, params object[] args) =>
        SaveSystem.GetMethod(method, BindingFlags.Static | BindingFlags.Public).Invoke(null, args);

    private static object Get(object target, string field) => target.GetType().GetField(field, Public).GetValue(target);
    private static T Get<T>(object target, string field) => (T)Get(target, field);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Public).SetValue(target, value);

    private static object PrivateField(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    private static T Value<T>(object target, string name) => ((NetworkVariable<T>)PrivateField(target, name)).Value;
    private static void SetValue<T>(object target, string name, T value) => ((NetworkVariable<T>)PrivateField(target, name)).Value = value;
    private static object Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, Private | Public).Invoke(target, args);

    private static object NewData() => Activator.CreateInstance(GameType("RoomiesSaveData"));

    private static object NewFurniture(int index, Vector3 position, float yaw, bool effectActive)
    {
        object f = Activator.CreateInstance(GameType("SavedFurniture"));
        Set(f, "catalogIndex", index);
        Set(f, "position", position);
        Set(f, "yaw", yaw);
        Set(f, "effectActive", effectActive);
        return f;
    }

    private static object NewPlayer(string name, int earning)
    {
        object p = Activator.CreateInstance(GameType("SavedPlayerMoney"));
        Set(p, "name", name);
        Set(p, "earning", earning);
        return p;
    }

    private static int CurrentVersion => (int)GameType("RoomiesSaveData").GetField("CurrentVersion").GetRawConstantValue();

    [SetUp]
    public void SetUp()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "RoomiesSaveTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        savePath = Path.Combine(tempDir, "roomies_save.json");
        Static("SetFilePathOverrideForTests", savePath);
        Static("ClearPendingLoad");
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (network != null)
        {
            network.Shutdown();
            double deadline = EditorApplication.timeSinceStartup + 5;
            while (network != null && network.ShutdownInProgress && EditorApplication.timeSinceStartup < deadline)
                yield return null;
        }

        foreach (var go in objects)
            if (go != null) Object.DestroyImmediate(go);
        objects.Clear();
        network = null;
        RoomiesTestNetcode.ClearSingleton();

        var effects = GameType("FurnitureEffectManager").GetProperty("InstanceOrNull").GetValue(null) as Component;
        if (effects != null) Object.DestroyImmediate(effects.gameObject);

        Static("ClearPendingLoad");
        Static("SetFilePathOverrideForTests", new object[] { null });
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
    }

    // ---------- ファイルの読み書き ----------

    [Test]
    public void SavePathIsRedirectedAwayFromTheRealSave()
    {
        string path = (string)SaveSystem.GetProperty("FilePath").GetValue(null);
        Assert.That(path, Is.EqualTo(savePath));
        Assert.That(path, Does.Not.StartWith(Application.persistentDataPath));
    }

    [Test]
    public void WriteThenLoadRoundTripsEveryField()
    {
        object data = NewData();
        Set(data, "day", 7);
        Set(data, "time", 1);
        Set(data, "remainingSeconds", 42.5f);
        Set(data, "sharedMoney", 1234);
        Set(data, "rentSurcharge", 150);
        Set(data, "fridgeStocked", true);
        Set(data, "fridgeBroken", false);
        Set(data, "hostName", "ゆせ");
        var furniture = (IList)Get(data, "furniture");
        furniture.Add(NewFurniture(3, new Vector3(1.5f, 3.3f, -2f), 90f, true));
        furniture.Add(NewFurniture(8, new Vector3(-4f, 0f, 6.25f), 215f, false));
        ((IList)Get(data, "players")).Add(NewPlayer("Bob", 320));

        Assert.That(Static("Write", data), Is.True);
        Assert.That(File.Exists(savePath), Is.True);
        Assert.That(File.Exists(savePath + ".tmp"), Is.False, "一時ファイルが残っている");

        object loaded = Static("Load");
        Assert.That(loaded, Is.Not.Null);
        Assert.That(Get<int>(loaded, "version"), Is.EqualTo(CurrentVersion));
        Assert.That(Get<int>(loaded, "day"), Is.EqualTo(7));
        Assert.That(Get<int>(loaded, "time"), Is.EqualTo(1));
        Assert.That(Get<float>(loaded, "remainingSeconds"), Is.EqualTo(42.5f));
        Assert.That(Get<int>(loaded, "sharedMoney"), Is.EqualTo(1234));
        Assert.That(Get<int>(loaded, "rentSurcharge"), Is.EqualTo(150));
        Assert.That(Get<bool>(loaded, "fridgeStocked"), Is.True);
        Assert.That(Get<bool>(loaded, "fridgeBroken"), Is.False);
        Assert.That(Get<string>(loaded, "hostName"), Is.EqualTo("ゆせ"));
        Assert.That(Get<string>(loaded, "savedAtUtc"), Is.Not.Empty);

        var loadedFurniture = (IList)Get(loaded, "furniture");
        Assert.That(loadedFurniture.Count, Is.EqualTo(2));
        Assert.That(Get<int>(loadedFurniture[0], "catalogIndex"), Is.EqualTo(3));
        Assert.That(Get<Vector3>(loadedFurniture[0], "position"), Is.EqualTo(new Vector3(1.5f, 3.3f, -2f)));
        Assert.That(Get<float>(loadedFurniture[0], "yaw"), Is.EqualTo(90f));
        Assert.That(Get<bool>(loadedFurniture[0], "effectActive"), Is.True);
        Assert.That(Get<int>(loadedFurniture[1], "catalogIndex"), Is.EqualTo(8));
        Assert.That(Get<bool>(loadedFurniture[1], "effectActive"), Is.False);

        var loadedPlayers = (IList)Get(loaded, "players");
        Assert.That(loadedPlayers.Count, Is.EqualTo(1));
        Assert.That(Get<string>(loadedPlayers[0], "name"), Is.EqualTo("Bob"));
        Assert.That(Get<int>(loadedPlayers[0], "earning"), Is.EqualTo(320));
    }

    [Test]
    public void MissingFileLoadsAsNoSave()
    {
        Assert.That(Static("Exists"), Is.False);
        Assert.That(Static("Load"), Is.Null);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("{ this is not json")]
    [TestCase("[1,2,3]")]
    [TestCase("null")]
    public void BrokenFileLoadsAsNoSaveWithoutThrowing(string content)
    {
        File.WriteAllText(savePath, content);
        Assert.DoesNotThrow(() => Static("Load"));
        Assert.That(Static("Load"), Is.Null);
    }

    [Test]
    public void UnsupportedVersionsAreNotLoaded()
    {
        File.WriteAllText(savePath, "{\"version\":" + (CurrentVersion + 1) + ",\"day\":9,\"sharedMoney\":500}");
        Assert.That(Static("Load"), Is.Null, "新しすぎる版を読んでしまった");

        File.WriteAllText(savePath, "{\"version\":0,\"day\":9,\"sharedMoney\":500}");
        Assert.That(Static("Load"), Is.Null, "版0を読んでしまった");
    }

    [Test]
    public void VersionOneSaveStillLoadsWithDefaultsForNewFields()
    {
        // 10/23 時点（版1）の形。冷蔵庫と effectActive はまだ無い
        File.WriteAllText(savePath,
            "{\"version\":1,\"savedAtUtc\":\"2026-10-23T04:00:00Z\",\"hostName\":\"host\",\"day\":4,\"time\":0," +
            "\"remainingSeconds\":120.0,\"sharedMoney\":800,\"rentSurcharge\":50," +
            "\"furniture\":[{\"catalogIndex\":2,\"position\":{\"x\":1.0,\"y\":3.3,\"z\":2.0},\"yaw\":180.0}]," +
            "\"players\":[{\"name\":\"A\",\"earning\":10}]}");

        object loaded = Static("Load");
        Assert.That(loaded, Is.Not.Null);
        Assert.That(Get<int>(loaded, "day"), Is.EqualTo(4));
        Assert.That(Get<int>(loaded, "sharedMoney"), Is.EqualTo(800));
        Assert.That(Get<bool>(loaded, "fridgeStocked"), Is.False);
        Assert.That(Get<bool>(loaded, "fridgeBroken"), Is.False);
        var furniture = (IList)Get(loaded, "furniture");
        Assert.That(furniture.Count, Is.EqualTo(1));
        Assert.That(Get<bool>(furniture[0], "effectActive"), Is.True, "版1の家具は効果ありとして読む");
    }

    [Test]
    public void HandEditedValuesAreMadeSafe()
    {
        File.WriteAllText(savePath,
            "{\"version\":" + CurrentVersion + ",\"day\":-3,\"time\":7,\"sharedMoney\":100," +
            "\"furniture\":[{\"catalogIndex\":-1,\"position\":{\"x\":0,\"y\":0,\"z\":0},\"yaw\":0}," +
            "{\"catalogIndex\":1,\"position\":{\"x\":1,\"y\":2,\"z\":3},\"yaw\":45}]," +
            "\"players\":[{\"name\":\"\",\"earning\":5}]}");

        object loaded = Static("Load");
        Assert.That(loaded, Is.Not.Null);
        Assert.That(Get<int>(loaded, "day"), Is.EqualTo(1));
        Assert.That(Get<int>(loaded, "time"), Is.EqualTo(0));
        Assert.That(((IList)Get(loaded, "furniture")).Count, Is.EqualTo(1), "番号が負の家具は捨てる");
        Assert.That(((IList)Get(loaded, "players")).Count, Is.EqualTo(0), "名前の無い所持金は捨てる");
    }

    [Test]
    public void OverwriteKeepsPreviousSaveAsBackupAndFallsBackToIt()
    {
        object first = NewData();
        Set(first, "day", 2);
        Set(first, "sharedMoney", 200);
        Assert.That(Static("Write", first), Is.True);

        object second = NewData();
        Set(second, "day", 3);
        Set(second, "sharedMoney", 300);
        Assert.That(Static("Write", second), Is.True);

        Assert.That(File.Exists(savePath + ".bak"), Is.True);
        Assert.That(Get<int>(Static("Load"), "day"), Is.EqualTo(3));

        // 本体が壊れたら、ひとつ前から読む
        File.WriteAllText(savePath, "{ broken");
        object recovered = Static("Load");
        Assert.That(recovered, Is.Not.Null);
        Assert.That(Get<int>(recovered, "day"), Is.EqualTo(2));
        Assert.That(Get<int>(recovered, "sharedMoney"), Is.EqualTo(200));
    }

    [Test]
    public void DeleteRemovesSaveBackupAndPendingLoad()
    {
        Static("Write", NewData());
        Static("Write", NewData());
        Static("SetPendingLoad", Static("Load"));
        Assert.That(Static("Exists"), Is.True);

        Assert.That(Static("Delete"), Is.True);
        Assert.That(File.Exists(savePath), Is.False);
        Assert.That(File.Exists(savePath + ".bak"), Is.False);
        Assert.That(Static("Exists"), Is.False);
        Assert.That(Static("Load"), Is.Null);
        Assert.That(SaveSystem.GetProperty("PendingLoad").GetValue(null), Is.Null);
    }

    // ---------- 部屋の状態 → セーブ → 部屋の状態 ----------

    private GameObject NewObject(string name)
    {
        var go = new GameObject("RoomiesSaveTest_" + name);
        objects.Add(go);
        return go;
    }

    private void StartHost()
    {
        Assert.That(RoomiesTestNetcode.NoActiveSession, Is.True, "Run outside an active game session.");
        RoomiesTestNetcode.EnsureInitialized();
        var go = NewObject("Network");
        network = go.AddComponent<NetworkManager>();
        network.SetSingleton(); // EditModeではOnEnableが呼ばれず、Singletonが入らないため
        network.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = go.AddComponent<RoomiesTestTransport>(),
            EnableSceneManagement = false,
            ForceSamePrefabs = false
        };
        Assert.That(network.StartHost(), Is.True);
    }

    private NetworkBehaviour Spawn(string type)
    {
        var go = NewObject(type);
        var no = go.AddComponent<NetworkObject>();
        var behaviour = (NetworkBehaviour)go.AddComponent(GameType(type));
        behaviour.enabled = false; // 演出や乱数イベントは動かさない
        // EditModeではAwakeが呼ばれないので、Instance（シングルトン）を入れるために自分で呼ぶ
        behaviour.GetType().GetMethod("Awake", Private)?.Invoke(behaviour, null);
        no.Spawn();
        return behaviour;
    }

    private Component NewSaveManager()
    {
        var manager = NewObject("RunSaveManager").AddComponent(GameType("RunSaveManager"));
        ((Behaviour)manager).enabled = false; // Update での自動保存は止め、テストから直接呼ぶ
        return manager;
    }

    [Test]
    public void RoomStateSurvivesSaveAndContinue()
    {
        StartHost();
        var money = Spawn("SharedMoneyManager");
        var day = Spawn("DayManager");
        var fridge = Spawn("NightFridge");
        var manager = NewSaveManager();

        // 3日目の夜、冷蔵庫に食材あり、家賃の上乗せあり
        SetValue(money, "sharedMoney", 1750);
        SetValue(day, "currentDay", 3);
        SetValue(day, "currentTime", 1);
        SetValue(day, "remainingTime", 75f);
        SetValue(day, "rentSurcharge", 200);
        SetValue(fridge, "stocked", true);

        object captured = Call(manager, "ServerCapture");
        Assert.That(captured, Is.Not.Null);
        Assert.That(Static("Write", captured), Is.True);

        // ゲームを閉じて新しい部屋を立てた状態（すべて初期値）にする
        SetValue(money, "sharedMoney", 0);
        SetValue(day, "currentDay", 1);
        SetValue(day, "currentTime", 0);
        SetValue(day, "remainingTime", 180f);
        SetValue(day, "rentSurcharge", 0);
        SetValue(fridge, "stocked", false);

        object loaded = Static("Load");
        Assert.That(loaded, Is.Not.Null);
        Assert.That(Call(manager, "ServerApply", loaded), Is.True);

        Assert.That(Value<int>(money, "sharedMoney"), Is.EqualTo(1750));
        Assert.That(Value<int>(day, "currentDay"), Is.EqualTo(3));
        Assert.That(Value<int>(day, "currentTime"), Is.EqualTo(1));
        Assert.That(Value<float>(day, "remainingTime"), Is.EqualTo(75f));
        Assert.That(Value<int>(day, "rentSurcharge"), Is.EqualTo(200));
        Assert.That(Value<bool>(day, "isGameOver"), Is.False);
        Assert.That(Value<bool>(fridge, "stocked"), Is.True);
        Assert.That(Value<bool>(fridge, "broken"), Is.False);
    }

    [Test]
    public void BrokenFridgeIsRestoredBrokenAndEmpty()
    {
        StartHost();
        Spawn("SharedMoneyManager");
        Spawn("DayManager");
        var fridge = Spawn("NightFridge");
        var manager = NewSaveManager();

        SetValue(fridge, "broken", true);
        object captured = Call(manager, "ServerCapture");
        Assert.That(Get<bool>(captured, "fridgeBroken"), Is.True);

        SetValue(fridge, "broken", false);
        Set(captured, "fridgeStocked", true); // 壊れているのに食材あり、という矛盾したセーブ
        Assert.That(Call(manager, "ServerApply", captured), Is.True);

        Assert.That(Value<bool>(fridge, "broken"), Is.True);
        Assert.That(Value<bool>(fridge, "stocked"), Is.False);
    }

    [Test]
    public void FurnitureThatCannotBePlacedDoesNotBlockTheRest()
    {
        // 家具そのものの置き直しは EditMode では確かめられない（NetworkFurniture が必ず持つ
        // NetworkTransform が EditMode では初期化されず、Spawn で例外になる。Netcode側の制約）。
        // ここではその失敗を逆手に取り、「家具が1つも置けなくても、日数とお金は戻り、落ちない」ことを見る。
        // 家具の番号・位置・向き・効果がファイルを往復することは WriteThenLoadRoundTripsEveryField で見ている。
        // 失敗した家具の片付けは Destroy を使うので、EditMode では Unity がエラーを1行出す。それは無視する。
        LogAssert.ignoreFailingMessages = true;
        StartHost();
        var money = Spawn("SharedMoneyManager");
        var day = Spawn("DayManager");
        var manager = NewSaveManager();

        var template = NewObject("FurnitureTemplate");
        template.AddComponent<NetworkObject>();
        template.AddComponent(GameType("NetworkFurniture"));
        day.GetType().GetField("networkFurniturePrefab", Private).SetValue(day, template);

        object data = NewData();
        Set(data, "day", 6);
        Set(data, "sharedMoney", 910);
        var list = (IList)Get(data, "furniture");
        list.Add(NewFurniture(0, new Vector3(2f, 3.3f, 4f), 90f, true));
        list.Add(NewFurniture(9999, Vector3.zero, 0f, true)); // カタログに無い番号
        list.Add(null);

        Assert.That(Call(manager, "ServerApply", data), Is.True);
        Assert.That(Value<int>(day, "currentDay"), Is.EqualTo(6));
        Assert.That(Value<int>(money, "sharedMoney"), Is.EqualTo(910));

        foreach (var no in network.SpawnManager.SpawnedObjectsList)
            Assert.That(no.GetComponent(GameType("NetworkFurniture")), Is.Null, "置けなかった家具の残骸が残っている");
    }

    [Test]
    public void SaveNowWritesForHostAndRefusesAfterGameOver()
    {
        StartHost();
        var money = Spawn("SharedMoneyManager");
        var day = Spawn("DayManager");
        var manager = NewSaveManager();

        // まだ部屋の準備（続きの反映）が済んでいない間は書かない
        Assert.That(Call(manager, "ServerSaveNow"), Is.False);
        Assert.That(File.Exists(savePath), Is.False);

        Assert.That(Call(manager, "ServerApply", NewData()), Is.True);
        SetValue(money, "sharedMoney", 640);
        SetValue(day, "currentDay", 5);

        Assert.That(Call(manager, "ServerSaveNow"), Is.True);
        object loaded = Static("Load");
        Assert.That(Get<int>(loaded, "sharedMoney"), Is.EqualTo(640));
        Assert.That(Get<int>(loaded, "day"), Is.EqualTo(5));

        // 負けた後は保存しない（「負ける直前からやり直す」を作らない）
        SetValue(money, "sharedMoney", 1);
        SetValue(day, "isGameOver", true);
        Assert.That(Call(manager, "ServerSaveNow"), Is.False);
        Assert.That(Get<int>(Static("Load"), "sharedMoney"), Is.EqualTo(640));
    }
}
