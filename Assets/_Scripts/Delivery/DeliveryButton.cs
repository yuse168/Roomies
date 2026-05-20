using Unity.Netcode;
using UnityEngine;

public class DeliveryButton : NetworkBehaviour
{
    [Header("納品エリア")]
    [SerializeField] private DeliveryZone deliveryZone;

    [Header("報酬")]
    [SerializeField] private int rewardMoney = 100;

    public void PressButton(PlayerMoney playerMoney)
    {
        if (playerMoney == null) return;

        PressButtonServerRpc(playerMoney.NetworkObjectId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void PressButtonServerRpc(ulong playerNetworkObjectId)
    {
        if (deliveryZone == null)
        {
            Debug.Log("DeliveryZoneが設定されていません");
            return;
        }

        if (!deliveryZone.HasBox())
        {
            Debug.Log("納品する箱がありません");
            return;
        }

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkObjectId, out NetworkObject playerObj))
        {
            PlayerMoney playerMoney = playerObj.GetComponent<PlayerMoney>();

            if (playerMoney != null)
            {
                playerMoney.AddMoneyServerRpc(rewardMoney);
            }
        }

        deliveryZone.RemoveBox();

        Debug.Log("納品成功 +" + rewardMoney);
    }
}