using TMPro;
using Unity.Netcode;
using UnityEngine;

public class PlayerEarning : NetworkBehaviour
{
    [Header("個人の稼ぎ記録")]
    [SerializeField] private int startEarning = 0;

    private NetworkVariable<int> earning = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            earning.Value = startEarning;
        }
    }

    public int GetEarning()
    {
        return earning.Value;
    }

    public void AddEarning(int amount)
    {
        if (!IsServer) return;
        earning.Value += amount;
    }
}