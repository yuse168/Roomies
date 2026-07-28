using Unity.Netcode;
using UnityEngine;

public class DeliveryItem : NetworkBehaviour
{
    [Header("ペナルティ設定")]
    [SerializeField] private int penaltyMoney = 300;

    [Header("地面タグ")]
    [SerializeField] private string groundTag = "Ground";

    private NetworkVariable<bool> isRareItem = new NetworkVariable<bool>(false);
    private bool isBroken = false;

    private DeliveryZone ownerZone;

    public void SetOwnerZone(DeliveryZone zone)
    {
        ownerZone = zone;
    }

    public bool IsRareItem()
    {
        return isRareItem.Value;
    }

    public void SetRare(bool rare)
    {
        if (!IsServer) return;

        isRareItem.Value = rare;

        Debug.Log("レア設定: " + rare);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;
        if (!isRareItem.Value) return;
        if (isBroken) return;

        if (collision.collider.CompareTag(groundTag)) return;
        if (collision.collider.GetComponentInParent<DeliveryItem>() != null) return;

        isBroken = true;

        int charged = SharedMoneyManager.Instance != null
            ? SharedMoneyManager.Instance.SpendUpTo(
                penaltyMoney,
                SharedMoneyReason.DeliveryDamagePenalty,
                name)
            : 0;

        Debug.Log("レアアイテムが壊れた -¥" + charged);

        if (ownerZone != null)
        {
            ownerZone.RespawnBox();
        }

        NetworkObject.Despawn(true);
    }
}
