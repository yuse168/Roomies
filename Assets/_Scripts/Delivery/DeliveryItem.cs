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
        Debug.Log("見た目更新 rare = " + rare);

        if (normalVisual == null)
        {
            Debug.LogError("normalVisual が設定されていません");
        }
        else
        {
            normalVisual.SetActive(!rare);
            Debug.Log("normalVisual: " + normalVisual.name + " / active = " + normalVisual.activeSelf);
        }

        if (rareVisual == null)
        {
            Debug.LogError("rareVisual が設定されていません");
        }
        else
        {
            rareVisual.SetActive(rare);
            Debug.Log("rareVisual: " + rareVisual.name + " / active = " + rareVisual.activeSelf);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;
        if (!isRareItem.Value) return;
        if (isBroken) return;

        if (collision.collider.CompareTag(groundTag)) return;

        isBroken = true;

        if (SharedMoneyManager.Instance != null)
        {
            SharedMoneyManager.Instance.SpendSharedMoney(penaltyMoney);
        }

        Debug.Log("レアアイテムが壊れた -¥" + penaltyMoney);

        NetworkObject.Despawn(true);
    }
}