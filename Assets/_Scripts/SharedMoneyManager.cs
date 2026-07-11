using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class SharedMoneyManager : NetworkBehaviour
{
    public static SharedMoneyManager Instance;

    [Header("共同金庫設定")]
    [SerializeField] private int startSharedMoney = 0;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI sharedMoneyText;

    private NetworkVariable<int> sharedMoney = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("SharedMoneyManager が複数あります。このオブジェクトを無効化します。");
            gameObject.SetActive(false);
            return;
        }

        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            sharedMoney.Value = startSharedMoney;
        }

        sharedMoney.OnValueChanged += OnSharedMoneyChanged;
        UpdateSharedMoneyUI(sharedMoney.Value);
    }

    public override void OnNetworkDespawn()
    {
        sharedMoney.OnValueChanged -= OnSharedMoneyChanged;
    }

    private void Update()
    {
        if (!IsServer) return;

        // テスト用：HostだけKキーで共同金庫に100円追加
        if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
        {
            AddSharedMoney(100);
        }
    }

    public void AddSharedMoney(int amount)
    {
        if (!IsServer) return;

        sharedMoney.Value += amount;
    }

    /// <summary>現在の共同口座残高（全員が参照可）。</summary>
    public int CurrentMoney => sharedMoney.Value;

    public bool CanPay(int amount)
    {
        return sharedMoney.Value >= amount;
    }

    public void SpendSharedMoney(int amount)
    {
        if (!IsServer) return;

        sharedMoney.Value -= amount;
    }

    /// <summary>
    /// クライアントからの購入要求。サーバー権限で残高を確認して減算する。
    /// 家具の購入などに使う。
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestPurchaseServerRpc(int amount)
    {
        if (amount <= 0) return;
        if (sharedMoney.Value >= amount)
        {
            sharedMoney.Value -= amount;
        }
    }

    public void PayRent(int rentAmount)
    {
        if (!IsServer) return;

        if (CanPay(rentAmount))
        {
            sharedMoney.Value -= rentAmount;
            Debug.Log("家賃支払い成功");
        }
        else
        {
            Debug.Log("家賃が払えない");
        }
    }

    private void OnSharedMoneyChanged(int oldValue, int newValue)
    {
        UpdateSharedMoneyUI(newValue);
    }

    private void UpdateSharedMoneyUI(int value)
    {
        if (sharedMoneyText == null) return;

        // 見出し（共同金庫）はHUDカード側が出すので金額のみ表示
        sharedMoneyText.text = "¥" + value.ToString("N0");
    }
}