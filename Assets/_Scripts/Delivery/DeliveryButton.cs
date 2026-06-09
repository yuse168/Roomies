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
        if (playerEarning == null)
        {
            Debug.LogWarning("PlayerEarning が null のため納品できません");
            return;
        }

        PressButtonServerRpc(playerEarning.NetworkObjectId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PressButtonServerRpc(ulong playerNetworkObjectId)
    {
        if (deliveryZone == null)
        {
            ShowResultClientRpc("DeliveryZoneが設定されていません");
            return;
        }

        if (!deliveryZone.HasBox())
        {
            ShowResultClientRpc("納品する箱がありません");
            return;
        }

        int finalReward = rewardMoney;

        DeliveryItem deliveryItem = deliveryZone.GetCurrentItem();

        if (deliveryItem != null)
        {
            Debug.Log("納品時レア判定: " + deliveryItem.IsRareItem());
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

        ShowResultClientRpc("納品成功 +" + finalReward);
    }

    [ClientRpc]
    private void ShowResultClientRpc(string message)
    {
        Debug.Log(message);
    }
}