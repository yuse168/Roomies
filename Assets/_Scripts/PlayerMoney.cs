using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMoney : NetworkBehaviour
{
    [Header("お金設定")]
    [SerializeField] private int startMoney = 1000;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI moneyText;

    private NetworkVariable<int> money = new NetworkVariable<int>(
        1000,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            money.Value = startMoney;
        }

        money.OnValueChanged += OnMoneyChanged;

        if (IsOwner)
        {
            UpdateMoneyUI(money.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        money.OnValueChanged -= OnMoneyChanged;
    }

    private void Update()
    {
        if (!IsOwner) return;

        // テスト用
        // Mキーで100円追加
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
        {
            AddMoneyServerRpc(100);
        }
    }

    [ServerRpc]
    public void AddMoneyServerRpc(int amount)
    {
        money.Value += amount;
    }

    [ServerRpc]
    public void SpendMoneyServerRpc(int amount)
    {
        money.Value -= amount;
    }

    // 家賃が払えるか確認
    public bool CanPay(int amount)
    {
        return money.Value >= amount;
    }

    // 家賃支払い
    public void PayRent(int amount)
    {
        if (!IsServer) return;

        money.Value -= amount;
    }

    private void OnMoneyChanged(int oldValue, int newValue)
    {
        if (!IsOwner) return;

        UpdateMoneyUI(newValue);
    }

    private void UpdateMoneyUI(int value)
    {
        if (moneyText == null) return;

        moneyText.text = "Money ¥" + value.ToString("N0");
    }
}