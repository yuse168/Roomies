using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// The game uses Assembly-CSharp. Reflection keeps tests isolated without moving
// existing scripts into a new runtime assembly or changing their serialized types.
public class RoomiesRegressionTests
{
    private readonly List<GameObject> objects = new();
    private NetworkManager network;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    private static Type GameType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static object Field(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    private static void SetField(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static object Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, Private | BindingFlags.Public).Invoke(target, args);
    private static T Value<T>(object target, string name) => ((NetworkVariable<T>)Field(target, name)).Value;
    private static void SetValue<T>(object target, string name, T value) => ((NetworkVariable<T>)Field(target, name)).Value = value;

    private GameObject NewObject(string name)
    {
        var go = new GameObject("RoomiesTest_" + name);
        objects.Add(go);
        return go;
    }

    private NetworkBehaviour Spawn(string type)
    {
        var go = NewObject(type);
        var no = go.AddComponent<NetworkObject>();
        var behaviour = (NetworkBehaviour)go.AddComponent(GameType(type));
        behaviour.enabled = false; // No runtime UI / random events in these focused tests.
        Wake(go);
        RegisterInstance(behaviour);
        no.Spawn();
        return behaviour;
    }

    // EditMode では、MonoBehaviour の Awake が動かない。ゲームも NGO の部品（NetworkTransform・NetworkRigidbody）も、Awake で中身を用意するので、
    // テストで作った物には、ここで Awake を呼ぶ（Play Mode で起きることを、同じ順でなぞる）。
    private static void Wake(GameObject go)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly;
        foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null) continue;
            MethodInfo awake = null;
            for (Type t = behaviour.GetType(); t != null && t != typeof(MonoBehaviour) && awake == null; t = t.BaseType)
                awake = t.GetMethod("Awake", flags, null, Type.EmptyTypes, null);
            if (awake != null) awake.Invoke(behaviour, null);
        }
    }

    // ゲームの管理役（SharedMoneyManager・DayManager など）は Awake で static の Instance に自分を入れる。
    // EditMode では Awake が動かないので、ここで入れる（入れないと、ほかの管理役から見つけられず、支払いなどが何も起きない）。後始末は TearDown
    private readonly List<Action> clearInstances = new();
    private void RegisterInstance(NetworkBehaviour behaviour)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        Type type = behaviour.GetType();
        FieldInfo field = type.GetField("Instance", flags) ?? type.GetField("<Instance>k__BackingField", flags);
        if (field == null || !field.FieldType.IsAssignableFrom(type)) return;
        var current = field.GetValue(null) as Object;
        if (current != null && current != behaviour) return;
        field.SetValue(null, behaviour);
        clearInstances.Add(() => field.SetValue(null, null));
    }

    // NGO はメッセージ型の一覧を Play Mode に入るときにしか作らず、抜けるときに空にする。
    // そのため EditMode では一覧が空で StartHost が例外になる。ここで1回だけ登録する。
    private static System.Collections.IList NetcodeMessageTypes =>
        (System.Collections.IList)typeof(NetworkManager).Assembly
            .GetType("Unity.Netcode.ILPPMessageProvider", true)
            .GetField("__network_message_types", BindingFlags.Static | BindingFlags.NonPublic)
            .GetValue(null);
    private bool registeredMessagesHere;

    [OneTimeSetUp]
    public void RegisterNetcodeMessages()
    {
        if (NetcodeMessageTypes.Count > 0) return;
        typeof(NetworkManager).Assembly
            .GetType("__GEN.INetworkMessageHelper", true)
            .GetMethod("InitializeMessages", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Invoke(null, null);
        registeredMessagesHere = true;
    }

    [OneTimeTearDown]
    public void UnregisterNetcodeMessages()
    {
        // 残すとドメインリロード無しで Play Mode に入ったとき二重登録になり、ゲームの StartHost が壊れる
        if (registeredMessagesHere) NetcodeMessageTypes.Clear();
        registeredMessagesHere = false;
    }

    [SetUp]
    public void SetUp()
    {
        Assert.That(NetworkManager.Singleton, Is.Null, "Run outside an active game session.");
        var go = NewObject("Network");
        network = go.AddComponent<NetworkManager>();
        network.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = go.AddComponent<RoomiesTestTransport>(),
            EnableSceneManagement = false,
            ForceSamePrefabs = false
        };
        // EditMode では NetworkManager の Awake / OnEnable が動かないので、NetworkManager.Singleton が空のまま。
        // NetworkObject.Spawn() は Singleton から NetworkManager を探すので、ここで自分で登録する（後始末は TearDown）
        network.SetSingleton();
        Assert.That(network.StartHost(), Is.True);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        // Shutdown は残っている NetworkObject を Object.Destroy で消す（EditMode ではエラーのログになる）。先に「消さずに」Despawn しておき、下で DestroyImmediate する
        if (network != null && network.SpawnManager != null)
            foreach (var no in new List<NetworkObject>(network.SpawnManager.SpawnedObjectsList))
                if (no != null && no.IsSpawned) { no.Despawn(false); if (!objects.Contains(no.gameObject)) objects.Add(no.gameObject); }
        if (network != null) network.Shutdown();
        double deadline = EditorApplication.timeSinceStartup + 5;
        while (network != null && network.ShutdownInProgress && EditorApplication.timeSinceStartup < deadline)
            yield return null;
        foreach (var clear in clearInstances) clear();
        clearInstances.Clear();
        foreach (var go in objects)
            if (go != null) Object.DestroyImmediate(go);
        // SetUp で登録した Singleton を外す（EditMode では OnDestroy も動かないので、自分で外す。残すとゲームの起動に響く）
        typeof(NetworkManager).GetMethod("ResetSingleton", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        var effects = GameType("FurnitureEffectManager").GetProperty("InstanceOrNull").GetValue(null) as Component;
        if (effects != null) Object.DestroyImmediate(effects.gameObject);
        objects.Clear();
    }

    // NGO は、Despawn(destroy: true) のとき Object.Destroy を呼ぶ。EditMode では Destroy が使えず、Unity がエラーのログを出す
    //（物は残るが、IsSpawned は false になる）。テストの対象ではないので、そのログが 1 回出ることを見込んでおく
    private static void ExpectEditModeDestroyLog() =>
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Destroy may not be called from edit mode"));

    // ゲームのコードが自分で Instantiate する物（家具・配達の箱）には、テストから Awake を呼べない。
    // NetworkTransform は補間の入れ物を Awake で作るので、EditMode ではそのまま出現させると例外になる。
    // そこで、プレハブを写して補間だけ切った「見本」を作り、ゲームにはそれを渡す（写した物も、同じネットワーク用の番号を持つ）。
    // 見本そのものは出現させない（数えるときは、出現した物だけを数える）。
    private GameObject EditModeTemplate(GameObject prefab)
    {
        var template = Object.Instantiate(prefab); objects.Add(template);
        template.name = prefab.name + " (EditMode template)";
        foreach (var nt in template.GetComponentsInChildren<Unity.Netcode.Components.NetworkTransform>(true)) nt.Interpolate = false;
        return template;
    }

    private static Component[] Spawned(Object[] found)
    {
        var list = new List<Component>();
        foreach (var o in found)
        {
            var c = (Component)o;
            var no = c.GetComponentInParent<NetworkObject>();
            if (no != null && no.IsSpawned) list.Add(c);
        }
        return list.ToArray();
    }

    private NetworkBehaviour SpawnOre(int index)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Prefabs/Mining/MiningOre_{index}.prefab");
        network.AddNetworkPrefab(prefab);
        var go = Object.Instantiate(prefab); objects.Add(go);
        // NetworkTransform は、補間の入れ物を Awake で作る。EditMode では Awake が動かないので、補間を切っておく
        // （切らないと、鉱石を持つ・置く（親を替える）ときに、空の入れ物を触って例外になる）
        foreach (var nt in go.GetComponentsInChildren<Unity.Netcode.Components.NetworkTransform>(true)) nt.Interpolate = false;
        Wake(go);
        go.GetComponent<NetworkObject>().Spawn();
        return (NetworkBehaviour)go.GetComponent(GameType("MiningOre"));
    }

    [Test]
    public void MiningSaleCreditsSharedAndIndividualExactlyOnce()
    {
        var money = Spawn("SharedMoneyManager");
        var earner = Spawn("PlayerEarning");
        var ore = SpawnOre(5);
        var carry = ore.GetComponent(GameType("CarryableObject"));
        SetValue(carry, "isHeld", true); SetValue(carry, "holderClientId", network.LocalClientId);
        ExpectEditModeDestroyLog();
        Assert.That(Call(ore, "ServerTrySell", earner), Is.True);
        Assert.That(Value<int>(money, "sharedMoney"), Is.EqualTo(220));
        Assert.That(Call(earner, "GetEarning"), Is.EqualTo(220));
        Assert.That(Call(ore, "ServerTrySell", earner), Is.False);
        Assert.That(Value<int>(money, "sharedMoney"), Is.EqualTo(220));
    }

    [Test]
    public void MiningCannotSellLooseOreOrAnotherPlayersCargo()
    {
        var money = Spawn("SharedMoneyManager"); var earner = Spawn("PlayerEarning");
        var ore = SpawnOre(6); var carry = ore.GetComponent(GameType("CarryableObject"));
        Assert.That(Call(ore, "ServerTrySell", earner), Is.False);
        SetValue(carry, "isHeld", true); SetValue(carry, "holderClientId", 55UL);
        Assert.That(Call(ore, "ServerTrySell", earner), Is.False);
        Assert.That(Value<int>(money, "sharedMoney"), Is.Zero);
        Assert.That(ore.IsSpawned, Is.True);
    }

    [Test]
    public void MiningFailedDepositKeepsOreForRetry()
    {
        var money = Spawn("SharedMoneyManager"); var earner = Spawn("PlayerEarning");
        var ore = SpawnOre(0); var carry = ore.GetComponent(GameType("CarryableObject"));
        SetValue(carry, "isHeld", true); SetValue(carry, "holderClientId", network.LocalClientId);
        SetValue(money, "sharedMoney", int.MaxValue);
        LogAssert.Expect(LogType.Error, "[Money] 入金拒否: int上限超過 amount=10 reason=MiningSale");
        Assert.That(Call(ore, "ServerTrySell", earner), Is.False);
        Assert.That(ore.IsSpawned, Is.True);
        SetValue(money, "sharedMoney", 0);
        ExpectEditModeDestroyLog();
        Assert.That(Call(ore, "ServerTrySell", earner), Is.True);
        Assert.That(Value<int>(money, "sharedMoney"), Is.EqualTo(10));
    }

    [Test]
    public void MiningForcedDropReleasesHolderAndKeepsOrePublic()
    {
        var ore = SpawnOre(7); var carry = ore.GetComponent(GameType("CarryableObject"));
        SetValue(carry, "isHeld", true); SetValue(carry, "holderClientId", network.LocalClientId);
        ore.GetComponent<Rigidbody>().isKinematic = true;
        Call(carry, "ServerRelease");
        Assert.That(Value<bool>(carry, "isHeld"), Is.False);
        Assert.That(Value<ulong>(carry, "holderClientId"), Is.EqualTo(ulong.MaxValue));
        Assert.That(ore.GetComponent<Rigidbody>().isKinematic, Is.False);
        Assert.That(ore.GetComponent<Rigidbody>().mass, Is.EqualTo(5));
        Assert.That(ore.IsSpawned, Is.True);
    }

    [Test]
    public void MiningClosureConfiscatesOnlyUndergroundHeldCargoWithoutChargingMoney()
    {
        var money = Spawn("SharedMoneyManager"); SetValue(money, "sharedMoney", 500);
        var spawnPoint = NewObject("MiningTestSpawn"); spawnPoint.AddComponent(GameType("SpawnPoint"));
        spawnPoint.transform.position = new Vector3(5,1,5);
        var player = NewObject("Miner"); var playerNo = player.AddComponent<NetworkObject>();
        var miner = (NetworkBehaviour)player.AddComponent(GameType("MiningPlayer")); miner.enabled = false;
        var spawn = (NetworkBehaviour)player.AddComponent(GameType("PlayerSpawnSync")); spawn.enabled = false;
        // Keep the test independent of scene spawning and presentation coroutines.
        playerNo.SpawnAsPlayerObject(network.LocalClientId);
        Call(miner, "ServerSetLevel", 2);
        var held = SpawnOre(5); var carry = held.GetComponent(GameType("CarryableObject"));
        SetValue(carry, "isHeld", true); SetValue(carry, "holderClientId", network.LocalClientId);
        var surface = SpawnOre(0); surface.transform.position = Vector3.up;
        var siteGo = NewObject("MineAuthority"); var no = siteGo.AddComponent<NetworkObject>();
        var site = (NetworkBehaviour)siteGo.AddComponent(GameType("MiningSite")); site.enabled = false;
        Wake(siteGo);
        site.GetType().GetField("targets").SetValue(site, Array.CreateInstance(GameType("MiningInteractable"), 0));
        site.GetType().GetField("mineLights").SetValue(site, Array.Empty<Light>());
        var exit = NewObject("Exit").transform; exit.position = new Vector3(5,1,5);
        site.GetType().GetField("surfaceReturn").SetValue(site, exit);
        no.Spawn();
        ExpectEditModeDestroyLog();      // 持っている鉱石 1 つが没収される
        Call(site, "CloseMine");
        Assert.That(held.IsSpawned, Is.False);
        Assert.That(surface.IsSpawned, Is.True);
        Assert.That(miner.GetType().GetProperty("Level").GetValue(miner), Is.Zero);
        Assert.That(player.transform.position, Is.EqualTo(exit.position));
        Assert.That(Value<int>(money, "sharedMoney"), Is.EqualTo(500));
    }

    [Test]
    public void RentCyclesContinueBeyondDayThree()
    {
        var day = Spawn("DayManager");
        int[] expected = { 3, 2, 1, 3, 2, 1, 3 };
        for (int i = 0; i < expected.Length; i++)
        {
            SetValue(day, "currentDay", i + 1);
            Assert.That(day.GetType().GetProperty("DaysUntilRent").GetValue(day), Is.EqualTo(expected[i]));
        }
    }

    [Test]
    public void RentSuccessAdvancesToDayFourAndChargesExactlyOnce()
    {
        var money = Spawn("SharedMoneyManager");
        SetValue(money, "sharedMoney", 900);
        var day = Spawn("DayManager");
        SetValue(day, "currentDay", 3);
        SetValue(day, "currentTime", 1);
        var routine = (IEnumerator)Call(day, "NightEndRoutine", 4, true);
        while (routine.MoveNext()) { } // Advance sequence boundaries without waiting on presentation time.
        Assert.That(Value<int>(money, "sharedMoney"), Is.EqualTo(400));
        Assert.That(Value<int>(day, "currentDay"), Is.EqualTo(4));
        Assert.That(Value<int>(day, "currentTime"), Is.Zero);
        Assert.That(Value<bool>(day, "isGameOver"), Is.False);
    }

    [Test]
    public void RentFailurePreservesBalanceAndEndsGame()
    {
        var money = Spawn("SharedMoneyManager");
        SetValue(money, "sharedMoney", 120);
        var day = Spawn("DayManager");
        SetValue(day, "currentDay", 3);
        SetValue(day, "currentTime", 1);
        var routine = (IEnumerator)Call(day, "NightEndRoutine", 4, true);
        while (routine.MoveNext()) { }
        Assert.That(Value<int>(money, "sharedMoney"), Is.EqualTo(120));
        Assert.That(Value<int>(day, "currentDay"), Is.EqualTo(3));
        Assert.That(Value<bool>(day, "isGameOver"), Is.True);
    }

    [Test]
    public void FurniturePurchaseRejectsDayTransitionAndGameOverWithoutCharging()
    {
        var money = Spawn("SharedMoneyManager");
        SetValue(money, "sharedMoney", 1000);
        var day = Spawn("DayManager");
        AssertPurchase(day, 0, false);
        SetValue(day, "currentTime", 1);
        SetValue(day, "isTransitioning", true);
        AssertPurchase(day, 0, false);
        SetValue(day, "isTransitioning", false);
        SetValue(day, "isGameOver", true);
        AssertPurchase(day, 0, false);
        Assert.That(Value<int>(money, "sharedMoney"), Is.EqualTo(1000));
    }

    [Test]
    public void FurniturePurchasesUseServerDeliveryOrderAndRefuseInsufficientFunds()
    {
        var money = Spawn("SharedMoneyManager");
        SetValue(money, "sharedMoney", 650);
        var day = Spawn("DayManager");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/NetworkFurniture.prefab");
        Assert.That(prefab, Is.Not.Null);
        // ゲームは「渡された物が、ネットワーク用プレハブの一覧に入っているか」を確かめるので、見本のほうを一覧に入れる
        var template = EditModeTemplate(prefab);
        network.AddNetworkPrefab(template);
        SetField(day, "networkFurniturePrefab", template);
        var player = NewObject("Player").AddComponent<NetworkObject>();
        player.SpawnAsPlayerObject(network.LocalClientId);
        NewObject("DeliveryPoint").AddComponent(GameType("FurnitureDeliveryPoint"));
        SetValue(day, "currentTime", 1);
        AssertPurchase(day, -1, false);
        AssertPurchase(day, 0, true); // coffee maker: 300
        AssertPurchase(day, 0, true);
        AssertPurchase(day, 0, false);
        Assert.That(Value<int>(money, "sharedMoney"), Is.EqualTo(50));
        Assert.That(Field(day, "furnitureDeliveryCount"), Is.EqualTo(2));
        var furniture = Spawned(Object.FindObjectsByType(GameType("NetworkFurniture")));
        foreach (var f in furniture) objects.Add(f.transform.root.gameObject);      // 後始末
        Assert.That(furniture.Length, Is.EqualTo(2));
        Assert.That(furniture[0].transform.position,
            Is.Not.EqualTo(furniture[1].transform.position));
    }

    [Test]
    public void MissingFurniturePrefabDoesNotCharge()
    {
        var money = Spawn("SharedMoneyManager");
        SetValue(money, "sharedMoney", 1000);
        var day = Spawn("DayManager");
        var player = NewObject("Player").AddComponent<NetworkObject>();
        player.SpawnAsPlayerObject(network.LocalClientId);
        SetValue(day, "currentTime", 1);
        AssertPurchase(day, 0, false);
        Assert.That(Value<int>(money, "sharedMoney"), Is.EqualTo(1000));
    }

    private void AssertPurchase(object day, int index, bool expected)
    {
        object[] args = { network.LocalClientId, index, null };
        bool result = (bool)Call(day, "TryPurchaseFurniture", args);
        Assert.That(result, Is.EqualTo(expected), args[2] as string);
    }

    [Test]
    public void DeliveryCreatesInitialBoxesOnNetworkSpawn()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Nomal_Box.prefab");
        Assert.That(prefab, Is.Not.Null);
        network.AddNetworkPrefab(prefab);
        var go = NewObject("Zone");
        var no = go.AddComponent<NetworkObject>();
        var zone = (NetworkBehaviour)go.AddComponent(GameType("DeliveryZone"));
        zone.enabled = false;
        SetField(zone, "normalBoxPrefab", EditModeTemplate(prefab).GetComponent<NetworkObject>());
        SetField(zone, "boxSpawnPoint", NewObject("BoxSpawn").transform);
        SetField(zone, "rarePercent", 0);
        no.Spawn();
        // 出す数は GameBalance で決まる（町に出しておく数。全体の上限は超えない）
        object balance = GameType("GameBalance").GetProperty("Current").GetValue(null);
        int loose = (int)balance.GetType().GetField("deliveryLooseBoxes").GetValue(balance);
        int max = (int)balance.GetType().GetField("deliveryMaxBoxes").GetValue(balance);
        var boxes = Spawned(Object.FindObjectsByType(GameType("DeliveryItem")));
        foreach (var box in boxes) objects.Add(box.transform.root.gameObject);      // 後始末
        Assert.That(boxes.Length, Is.EqualTo(Mathf.Min(loose, max)));
    }

    [Test]
    public void UntaggedChildCollidersRemainDeliverableUntilLastColliderLeaves()
    {
        var go = NewObject("Zone");
        var no = go.AddComponent<NetworkObject>();
        var zone = (NetworkBehaviour)go.AddComponent(GameType("DeliveryZone"));
        zone.enabled = false;
        // 箱のプレハブを割り当てていないので、出現のとき「設定されていません」のエラーが出る（この確認では箱を出さない）。そのログは無視する
        LogAssert.ignoreFailingMessages = true;
        no.Spawn();
        LogAssert.ignoreFailingMessages = false;
        var box = NewObject("Box");
        box.tag = "DeliveryBox";
        box.AddComponent<NetworkObject>().Spawn();
        var first = NewObject("ChildA");
        first.transform.SetParent(box.transform);
        var a = first.AddComponent<BoxCollider>();
        var second = NewObject("ChildB");
        second.transform.SetParent(box.transform);
        var b = second.AddComponent<BoxCollider>();
        Call(zone, "OnTriggerEnter", a);
        Call(zone, "OnTriggerEnter", b);
        Assert.That(Call(zone, "HasBox"), Is.True);
        Call(zone, "OnTriggerExit", a);
        Assert.That(Call(zone, "HasBox"), Is.True);
        Call(zone, "OnTriggerExit", b);
        Assert.That(Call(zone, "HasBox"), Is.False);
    }
}

// Host-only test transport: no sockets, Steam API, or external traffic.
public class RoomiesTestTransport : NetworkTransport
{
    public override ulong ServerClientId => 0;
    public override bool StartClient() => false;
    public override bool StartServer() => true;
    public override void Initialize(NetworkManager networkManager = null) { }
    public override void Shutdown() { }
    public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery delivery) { }
    public override void DisconnectLocalClient() { }
    public override void DisconnectRemoteClient(ulong clientId) { }
    public override ulong GetCurrentRtt(ulong clientId) => 0;
    public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
    {
        clientId = 0;
        payload = default;
        receiveTime = Time.realtimeSinceStartup;
        return NetworkEvent.Nothing;
    }
}
