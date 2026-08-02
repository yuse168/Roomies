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

    [Header("Server検証")]
    [SerializeField, Min(0.1f)] private float serverInteractDistance = 4f;

    [Header("フィードバック")]
    [Tooltip("納品結果を既存のゲーム内バナーで表示します。オフの場合はログ表示だけになります。")]
    [SerializeField] private bool showResultBanner = true;
    [Tooltip("成功を全員へ知らせます。オフの場合は納品した本人だけに表示します。")]
    [SerializeField] private bool announceSuccessToEveryone = true;

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
    private void PressButtonServerRpc(
        ulong playerNetworkObjectId,
        RpcParams rpcParams = default)
    {
        if (deliveryZone == null)
        {
            ShowToRequester(
                rpcParams.Receive.SenderClientId,
                "納品できません",
                "DeliveryZoneが設定されていません",
                NightEventManager.StyleDanger);
            return;
        }

        if (!deliveryZone.HasBox())
        {
            ShowToRequester(
                rpcParams.Receive.SenderClientId,
                "納品できません",
                "納品エリアに箱がありません",
                NightEventManager.StyleInfo);
            return;
        }

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
                .TryGetValue(playerNetworkObjectId, out NetworkObject playerObj) ||
            playerObj.OwnerClientId != rpcParams.Receive.SenderClientId)
        {
            Debug.LogWarning("[Delivery] 不正なプレイヤー名義の納品要求を拒否しました。");
            return;
        }

        if (Vector3.Distance(playerObj.transform.position, transform.position) >
            serverInteractDistance)
        {
            Debug.LogWarning("[Delivery] 遠すぎる納品要求を拒否しました。");
            return;
        }

        PlayerEarning playerEarning = playerObj.GetComponent<PlayerEarning>();
        if (playerEarning == null || SharedMoneyManager.Instance == null)
        {
            ShowToRequester(
                rpcParams.Receive.SenderClientId,
                "納品できません",
                "報酬システムが見つかりません",
                NightEventManager.StyleDanger);
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

        bool credited = SharedMoneyManager.Instance.TryAdd(
            finalReward,
            SharedMoneyReason.DeliveryReward,
            $"client={rpcParams.Receive.SenderClientId}");

        if (!credited)
        {
            ShowToRequester(
                rpcParams.Receive.SenderClientId,
                "納品失敗",
                "報酬を共同口座へ反映できませんでした",
                NightEventManager.StyleDanger);
            return;
        }

        playerEarning.AddEarning(finalReward);
        deliveryZone.RemoveBox();

        if (showResultBanner && NightEventManager.Instance != null)
        {
            if (announceSuccessToEveryone)
            {
                NightEventManager.Instance.ServerAnnounce(
                    "納品成功！",
                    $"+¥{finalReward:N0}　共同口座へ",
                    NightEventManager.StylePeace);
            }
            else
            {
                NightEventManager.Instance.ServerAnnounceTo(
                    rpcParams.Receive.SenderClientId,
                    "納品成功！",
                    $"+¥{finalReward:N0}　共同口座へ",
                    NightEventManager.StylePeace);
            }
        }
        else
        {
            ShowResultClientRpc("納品成功 +" + finalReward);
        }
    }

    private void ShowToRequester(
        ulong clientId,
        string title,
        string message,
        byte style)
    {
        if (showResultBanner && NightEventManager.Instance != null)
        {
            NightEventManager.Instance.ServerAnnounceTo(
                clientId,
                title,
                message,
                style);
        }
        else
        {
            ShowResultClientRpc(title + ": " + message);
        }
    }

    [ClientRpc]
    private void ShowResultClientRpc(string message)
    {
        Debug.Log(message);
    }
}
