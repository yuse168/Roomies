using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

/// <summary>One scene authority; existing DayManager is the only closure clock.</summary>
public class MiningSite : NetworkBehaviour
{
    public static MiningSite Instance { get; private set; }
    public MiningInteractable[] targets;
    public MiningOre[] ores;
    public Transform surfaceReturn;
    public Light[] mineLights;
    public Transform[] eventCenters;
    public AudioClip hitSound, breakSound, saleSound, warningSound;
    private NetworkList<int> health;
    private readonly NetworkVariable<int> hazard = new(0); // 1 blackout, 2 gas, 3 cave-in warning
    private readonly NetworkVariable<int> hazardLevel = new(2);
    private readonly NetworkVariable<double> hazardEnds = new(0);
    private readonly Dictionary<ulong, float> nextAction = new();
    private readonly Dictionary<ulong, float> nextTravel = new();
    private float nextHazard;
    private bool dayWasOpen;
    private int lastWarning;
    private float[] lightIntensity;
    private GameObject gasVisual;
    public bool IsOpen => DayManager.Instance != null && DayManager.Instance.IsSpawned &&
        !DayManager.Instance.IsNight && !DayManager.Instance.IsGameOver && DayManager.Instance.RemainingSeconds > 0;
    public int Health(int index) => health != null && index >= 0 && index < health.Count ? health[index] :
        targets != null && index >= 0 && index < targets.Length ? targets[index].maxHits : 0;
    public string HazardLabel => hazard.Value == 1 ? "停電中・ヘッドライトで帰ろう" :
        hazard.Value == 2 ? $"地下{hazardLevel.Value}層にガス・足元注意" :
        hazard.Value == 3 ? $"地下{hazardLevel.Value}層で落盤予告！離れて！" : "";

    private void Awake()
    {
        Instance = this;
        health = new NetworkList<int>();
    }
    public override void OnNetworkSpawn()
    {
        health.OnListChanged += HealthChanged;
        if (IsServer) ResetRocks();
        RefreshRocks();
        dayWasOpen = IsOpen;
        nextHazard = Time.time + 24;
        lightIntensity = mineLights.Select(l => l != null ? l.intensity : 0).ToArray();
    }
    public override void OnNetworkDespawn()
    {
        health.OnListChanged -= HealthChanged;
        if (Instance == this) Instance = null;
        if (gasVisual != null) Destroy(gasVisual);
    }
    private void HealthChanged(NetworkListEvent<int> change) => RefreshRocks();
    private void RefreshRocks()
    {
        for (int i = 0; i < targets.Length; i++) targets[i].Refresh(Health(i));
    }
    private void ResetRocks()
    {
        health.Clear();
        foreach (var target in targets) health.Add(target.maxHits);
        // Surface stock remains valuable; only abandoned underground stock is cleared each morning.
        foreach (var ore in MiningOre.Active.ToArray())
            if (ore != null && !ore.Carry.IsHeld && ore.transform.position.y < -5) ore.NetworkObject.Despawn();
        lastWarning = 0;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestUseServerRpc(int index, ulong playerId, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!IsSpawned || index < 0 || index >= targets.Length ||
            !NetworkManager.ConnectedClients.TryGetValue(sender, out var client) ||
            client.PlayerObject == null || client.PlayerObject.NetworkObjectId != playerId) return;
        var player = client.PlayerObject;
        var miner = player.GetComponent<MiningPlayer>();
        var job = player.GetComponent<SmugglingPlayer>();
        if (miner == null || job != null && (job.IsJailed || job.IsControlLocked)) return;
        if (nextAction.TryGetValue(sender, out float next) && Time.time < next) return;
        var target = targets[index];
        if (!CanReach(player.transform.position, target)) return;
        nextAction[sender] = Time.time + .18f;
        if (target.action == MiningAction.Travel)
        {
            foreach (var obj in NetworkManager.SpawnManager.SpawnedObjectsList)
                if (obj.TryGetComponent<NetworkFurniture>(out var furniture) && furniture.IsHeldBy(sender))
                { miner.ServerNotice("家具を置いてからエレベーターを使おう", false); return; }
            if (target.level > 0 && !IsOpen) { miner.ServerNotice("採掘場は日没で閉鎖。朝に来よう", false); return; }
            if (nextTravel.TryGetValue(sender, out float travel) && Time.time < travel) return;
            if (target.destination == null) return;
            nextTravel[sender] = Time.time + 1.2f;
            Relocate(player, miner, target.destination.position, target.level, target.destination.eulerAngles.y);
            miner.ServerNotice(target.level == 0 ? "地上に到着・隣の買取機で換金しよう" : $"地下{target.level}層 / 日没までに地上へ！", false);
        }
        else if (target.action == MiningAction.Pickaxe && IsOpen)
        {
            miner.ServerEquip();
            miner.ServerNotice("ツルハシを借りた！ 岩に左クリック / Fで鉱石を運ぶ", false);
        }
        else if (target.action == MiningAction.Sell)
        {
            if (DayManager.Instance == null || DayManager.Instance.IsGameOver || miner.Level != 0) return;
            var ore = MiningOre.Active.FirstOrDefault(o => o != null && o.Carry.IsHeldBy(sender));
            if (ore == null) { miner.ServerNotice("鉱石をFで持ってから換金してね", false); return; }
            int value = ore.ServerPrice;
            string label = ore.displayName;
            if (ore.ServerTrySell(player.GetComponent<PlayerEarning>()))
                miner.ServerNotice($"{label}  +¥{value:N0}  共同口座へ", true);
        }
        else if (target.action == MiningAction.Rock && IsOpen && miner.Level == target.level)
        {
            if (!miner.HasPickaxe) { miner.ServerNotice("入口の工具ラックでツルハシを借りよう", false); return; }
            if (HasHeldItem(sender)) { miner.ServerNotice("Fで荷物を置いてから掘ろう", false); return; }
            if (health[index] <= 0) return;
            if (MiningOre.Active.Count >= 100) { miner.ServerNotice("鉱石がいっぱい！先に地上で換金しよう", false); return; }
            nextAction[sender] = Time.time + .55f;
            health[index]--;
            miner.SwingRpc();
            ImpactRpc(index, health[index] == 0);
            if (health[index] == 0) SpawnOre(target);
        }
    }

    public static bool CanReach(Vector3 playerPosition, MiningInteractable target)
    {
        if (!float.IsFinite(playerPosition.x) || !float.IsFinite(playerPosition.y) || !float.IsFinite(playerPosition.z) ||
            target == null || target.targetCollider == null || !target.targetCollider.enabled) return false;
        Vector3 point = target.targetCollider.ClosestPoint(playerPosition);
        if ((point - playerPosition).sqrMagnitude > 16f) return false;
        Vector3 eye = playerPosition + Vector3.up * .65f;
        Vector3 toward = target.targetCollider.bounds.center - eye;
        foreach (var hit in Physics.RaycastAll(eye, toward.normalized, toward.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider == target.targetCollider || hit.collider.GetComponentInParent<MiningInteractable>() == target) continue;
            if (hit.collider.GetComponentInParent<CarryableObject>() != null) continue;
            if (hit.collider.GetComponentInParent<PlayerInteract>() != null) continue;
            return false;
        }
        return true;
    }

    public static bool HasHeldItem(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return false;
        foreach (var obj in nm.SpawnManager.SpawnedObjectsList)
        {
            if (obj.TryGetComponent<CarryableObject>(out var c) && c.IsHeldBy(clientId)) return true;
            if (obj.TryGetComponent<NetworkFurniture>(out var f) && f.IsHeldBy(clientId)) return true;
        }
        return false;
    }

    // Loot indexes are authored once; random choice and all spawn impulses are server-only.
    public static int ChooseOre(int level, float roll)
    {
        if (level == 1) return roll < .3f ? 0 : roll < .6f ? 1 : roll < .86f ? 2 : 8;
        if (level == 2) return roll < .35f ? 3 : roll < .58f ? 2 : roll < .83f ? 4 : 9;
        return roll < .04f ? 7 : roll < .37f ? 5 : roll < .6f ? 6 : roll < .8f ? 4 : 10;
    }
    private void SpawnOre(MiningInteractable target)
    {
        int count = Mathf.Min(target.level == 1 ? 2 : 1, 100 - MiningOre.Active.Count);
        for (int i = 0; i < count; i++)
        {
            int kind = ChooseOre(target.level, Random.value);
            var ore = Instantiate(ores[kind], target.dropPoint.position + Vector3.up * .5f + Vector3.right * (i * .5f - .25f), Random.rotation);
            ore.NetworkObject.Spawn(true);
            ore.GetComponent<Rigidbody>().AddForce(Vector3.up * 2f + Random.insideUnitSphere, ForceMode.Impulse);
            if (ore.rare) RareRpc(ore.transform.position, kind);
        }
    }

    private void Relocate(NetworkObject player, MiningPlayer miner, Vector3 point, int level, float yaw)
    {
        // Furniture and delivery parcels stay in their own job areas. Ore follows its existing holder.
        foreach (var obj in NetworkManager.SpawnManager.SpawnedObjectsList)
        {
            if (!obj.TryGetComponent<CarryableObject>(out var carry) || !carry.IsHeldBy(player.OwnerClientId)) continue;
            if (obj.GetComponent<MiningOre>() != null) carry.ServerRelocate(point + Vector3.up * .3f + Quaternion.Euler(0,yaw,0) * Vector3.forward * 1.8f);
            else carry.ServerRelease();
        }
        miner.ServerSetLevel(level);
        player.GetComponent<PlayerSpawnSync>().ServerTeleport(point, yaw);
    }

    private void Update()
    {
        if (!IsSpawned) return;
        UpdateLights();
        if (!IsServer) return;
        bool open = IsOpen;
        if (dayWasOpen && !open) CloseMine();
        if (!dayWasOpen && open) { ResetRocks(); nextHazard = Time.time + 24; }
        dayWasOpen = open;
        if (!open) return;
        float seconds = DayManager.Instance.RemainingSeconds;
        int warning = seconds <= 30 ? 3 : seconds <= 60 ? 2 : seconds <= 120 ? 1 : 0;
        if (warning > lastWarning) { lastWarning = warning; WarningRpc(Mathf.CeilToInt(seconds)); }
        double now = NetworkManager.ServerTime.Time;
        if (hazard.Value != 0 && now >= hazardEnds.Value)
        {
            if (hazard.Value == 3) CaveIn();
            hazard.Value = 0;
        }
        if (Time.time >= nextHazard && hazard.Value == 0)
        {
            var miners = NetworkManager.ConnectedClientsList.Select(c => c.PlayerObject != null ? c.PlayerObject.GetComponent<MiningPlayer>() : null)
                .Where(m => m != null && m.Level >= 2).ToArray();
            nextHazard = Time.time + Random.Range(22f, 34f);
            if (miners.Length == 0) return;
            hazardLevel.Value = miners[Random.Range(0, miners.Length)].Level;
            hazard.Value = Random.Range(1, 4);
            hazardEnds.Value = now + (hazard.Value == 3 ? 2.5 : 9);
            EventRpc(HazardLabel);
        }
    }
    private void CloseMine()
    {
        hazard.Value = 0;
        foreach (var client in NetworkManager.ConnectedClientsList)
        {
            var player = client.PlayerObject;
            if (player == null) continue;
            var miner = player.GetComponent<MiningPlayer>();
            if (miner == null || miner.Level == 0) continue;
            foreach (var ore in MiningOre.Active.ToArray())
                if (ore != null && ore.Carry.IsHeldBy(client.ClientId)) { ore.Carry.ServerRelease(); ore.NetworkObject.Despawn(); }
            var jail = player.GetComponent<SmugglingPlayer>();
            if (jail != null && jail.IsJailed) { miner.ServerSetLevel(0); continue; }
            Relocate(player, miner, surfaceReturn.position + Vector3.right * ((int)(client.ClientId % 4) * 1.1f), 0, 180);
            miner.ServerNotice("閉鎖！未売却の手荷物を失った。地上へ避難しました", false);
        }
    }
    private void CaveIn()
    {
        Vector3 center = eventCenters[hazardLevel.Value - 1].position;
        CaveInRpc(center);
        foreach (var client in NetworkManager.ConnectedClientsList)
        {
            var player = client.PlayerObject;
            if (player == null || Vector3.Distance(player.transform.position, center) > 5) continue;
            foreach (var ore in MiningOre.Active)
                if (ore != null && ore.Carry.IsHeldBy(client.ClientId)) ore.Carry.ServerRelease();
            player.GetComponent<MiningPlayer>()?.ImpactRpc((player.transform.position - center).normalized * 4 + Vector3.up * 3);
        }
    }
    public bool IsGasAt(Vector3 position) => IsSpawned && hazard.Value == 2 &&
        Vector3.Distance(position, eventCenters[hazardLevel.Value - 1].position) < 5;

    private void UpdateLights()
    {
        if (lightIntensity == null) return;
        bool urgent = IsOpen && DayManager.Instance.RemainingSeconds < 30;
        float multiplier = hazard.Value == 1 ? .08f : urgent ? .55f + .45f * Mathf.Abs(Mathf.Sin((float)NetworkManager.ServerTime.Time * 5)) : 1;
        for (int i = 0; i < mineLights.Length; i++) if (mineLights[i] != null) mineLights[i].intensity = lightIntensity[i] * multiplier;
        if (hazard.Value == 2)
        {
            if (gasVisual == null) gasVisual = MiningFeedback.Gas(eventCenters[hazardLevel.Value - 1].position);
            gasVisual.transform.position = eventCenters[hazardLevel.Value - 1].position;
        }
        else if (gasVisual != null) { Destroy(gasVisual); gasVisual = null; }
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)] private void ImpactRpc(int index, bool broken)
    {
        var target = targets[index];
        MiningFeedback.Chips(target.dropPoint.position, broken ? 18 : 5, broken ? breakSound : hitSound);
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)] private void RareRpc(Vector3 point, int kind)
    {
        MiningFeedback.Chips(point, 14, saleSound, true);
        var local = NetworkManager.LocalClient?.PlayerObject?.GetComponent<MiningPlayer>();
        if (local != null && local.Level > 0) local.Notice($"発見！ {ores[kind].displayName} / 誰でも拾える", false);
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)] private void WarningRpc(int seconds)
    {
        var local = NetworkManager.LocalClient?.PlayerObject?.GetComponent<MiningPlayer>();
        if (local != null && local.Level > 0) local.Notice($"採掘場閉鎖まで {seconds}秒！地上へ戻ろう", false, true);
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)] private void EventRpc(string label)
    {
        var local = NetworkManager.LocalClient?.PlayerObject?.GetComponent<MiningPlayer>();
        if (local != null && local.Level > 0) local.Notice(label, false, true);
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)] private void CaveInRpc(Vector3 center) => MiningFeedback.Chips(center + Vector3.up * 2.2f, 28, breakSound);
}
