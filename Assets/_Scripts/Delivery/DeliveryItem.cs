using Unity.Netcode;
using UnityEngine;

public class DeliveryItem : NetworkBehaviour
{
    [Header("見た目")]
    [SerializeField] private GameObject normalVisual;
    [SerializeField] private GameObject rareVisual;

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

    public override void OnNetworkSpawn()
    {
        isRareItem.OnValueChanged += OnRareChanged;
        UpdateVisual(isRareItem.Value);
    }

    public override void OnNetworkDespawn()
    {
        isRareItem.OnValueChanged -= OnRareChanged;
    }

    private void OnRareChanged(bool previousValue, bool newValue)
    {
        UpdateVisual(newValue);
    }

    public bool IsRareItem()
    {
        return isRareItem.Value;
    }

    public void SetRare(bool rare)
    {
        if (!IsServer) return;

        isRareItem.Value = rare;
        UpdateVisual(rare);

        Debug.Log("レア設定: " + rare);
    }

    private void UpdateVisual(bool rare)
    {
        if (normalVisual != null)
        {
            normalVisual.SetActive(!rare);
        }

        if (rareVisual != null)
        {
            rareVisual.SetActive(rare);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;
        if (!isRareItem.Value) return;
        if (isBroken) return;

        if (collision.collider.CompareTag(groundTag)) return;
        if (collision.collider.GetComponentInParent<DeliveryItem>() != null) return;

        isBroken = true;

        if (SharedMoneyManager.Instance != null)
        {
            SharedMoneyManager.Instance.SpendSharedMoney(penaltyMoney);
        }

        Debug.Log("レアアイテムが壊れた -¥" + penaltyMoney);

        if (ownerZone != null)
        {
            ownerZone.RespawnBox();
        }

        NetworkObject.Despawn(true);
    }
}