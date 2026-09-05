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
        no.Spawn();
        return behaviour;
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
        Assert.That(network.StartHost(), Is.True);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (network != null) network.Shutdown();
        double deadline = EditorApplication.timeSinceStartup + 5;
        while (network != null && network.ShutdownInProgress && EditorApplication.timeSinceStartup < deadline)
            yield return null;
        foreach (var go in objects)
            if (go != null) Object.DestroyImmediate(go);
        var effects = GameType("FurnitureEffectManager").GetProperty("InstanceOrNull").GetValue(null) as Component;
        if (effects != null) Object.DestroyImmediate(effects.gameObject);
        objects.Clear();
    }

    private NetworkBehaviour SpawnOre(int index)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Prefabs/Mining/MiningOre_{index}.prefab");
        network.AddNetworkPrefab(prefab);
        var go = Object.Instantiate(prefab); objects.Add(go);
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
        site.GetType().GetField("targets").SetValue(site, Array.CreateInstance(GameType("MiningInteractable"), 0));
        site.GetType().GetField("mineLights").SetValue(site, Array.Empty<Light>());
        var exit = NewObject("Exit").transform; exit.position = new Vector3(5,1,5);
        site.GetType().GetField("surfaceReturn").SetValue(site, exit);
        no.Spawn();
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
        network.AddNetworkPrefab(prefab);
        SetField(day, "networkFurniturePrefab", prefab);
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
        var furniture = Object.FindObjectsByType(GameType("NetworkFurniture"));
        Assert.That(furniture.Length, Is.EqualTo(2));
        Assert.That(((Component)furniture[0]).transform.position,
            Is.Not.EqualTo(((Component)furniture[1]).transform.position));
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
        SetField(zone, "normalBoxPrefab", prefab.GetComponent<NetworkObject>());
        SetField(zone, "boxSpawnPoint", NewObject("BoxSpawn").transform);
        SetField(zone, "maxBoxCount", 2);
        SetField(zone, "rarePercent", 0);
        no.Spawn();
        Assert.That(Object.FindObjectsByType(GameType("DeliveryItem")).Length, Is.EqualTo(2));
    }

    [Test]
    public void UntaggedChildCollidersRemainDeliverableUntilLastColliderLeaves()
    {
        var go = NewObject("Zone");
        var no = go.AddComponent<NetworkObject>();
        var zone = (NetworkBehaviour)go.AddComponent(GameType("DeliveryZone"));
        zone.enabled = false;
        SetField(zone, "maxBoxCount", 0);
        no.Spawn();
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
