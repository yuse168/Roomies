using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(CarryableObject))]
public class MiningOre : NetworkBehaviour
{
    public static readonly HashSet<MiningOre> Active = new();
    public string displayName;
    [Min(1)] public int price = 20;
    public bool mystery;
    public bool rare;
    public CarryableObject Carry { get; private set; }
    private bool sold;
    private readonly NetworkVariable<int> appraisedPrice = new(0,
        NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
    public string ValueLabel => mystery ? "鑑定まで価格不明" : $"¥{price:N0}";
    public int ServerPrice => mystery ? appraisedPrice.Value : price;

    private void Awake() => Carry = GetComponent<CarryableObject>();
    public override void OnNetworkSpawn()
    {
        Active.Add(this);
        if (IsServer && mystery) appraisedPrice.Value = Random.value < .8f ? 40 : 420;
        if (IsServer) GetComponent<Rigidbody>().mass = Carry.weightLevel;
    }
    public override void OnNetworkDespawn() => Active.Remove(this);

    public bool ServerTrySell(PlayerEarning earner)
    {
        if (!IsServer || !IsSpawned || sold || earner == null || !Carry.IsHeldBy(earner.OwnerClientId)) return false;
        var bank = SharedMoneyManager.Instance;
        if (bank == null) return false;
        sold = true;
        if (!bank.TryAdd(ServerPrice, SharedMoneyReason.MiningSale, displayName)) { sold = false; return false; }
        earner.AddEarning(ServerPrice);
        Carry.ServerRelease();
        NetworkObject.Despawn();
        return true;
    }
}
