using Unity.Netcode;
using UnityEngine;

public class DeliveryButton : NetworkBehaviour
{
    [Header("納品エリア")]
    [SerializeField] private DeliveryZone deliveryZone;

    [Header("通常報酬")]
    [SerializeField] private int rewardMoney = 100;

    [Header("レア報酬")]
    [SerializeField] private int rareRewardMoney = 300;

    public void PressButton(PlayerEarning playerEarning)
    {
        if (playerEarning == null) return;

        PressButtonServerRpc(playerEarning.NetworkObjectId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
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

        int finalReward = rewardMoney;

        DeliveryItem deliveryItem = deliveryZone.GetCurrentItem();

        Debug.Log("deliveryItem: " + deliveryItem);

        if (deliveryItem != null)
        {
            Debug.Log("納品時レア判定: " + deliveryItem.IsRareItem());
        }
        else
        {
            Debug.Log("DeliveryItem が取得できませんでした");
        }

        if (deliveryItem != null && deliveryItem.IsRareItem())
        {
            finalReward = rareRewardMoney;
        }

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(playerNetworkObjectId, out NetworkObject playerObj))
        {
            PlayerEarning playerEarning = playerObj.GetComponent<PlayerEarning>();

            if (playerEarning != null)
            {
                playerEarning.AddEarning(finalReward);
            }

            if (SharedMoneyManager.Instance != null)
            {
                SharedMoneyManager.Instance.AddSharedMoney(finalReward);
            }
        }

        deliveryZone.RemoveBox();

        Debug.Log("納品成功 +" + finalReward);
    }
}