using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public enum SharedMoneyReason
{
    Unknown,
    DebugGrant,
    DeliveryReward,
    DeliveryDamagePenalty,
    SmugglingReward,
    ArrestFine,
    SlotBet,
    SlotReward,
    BlackjackBet,
    BlackjackPayout,
    FurniturePurchase,
    FurniturePassiveIncome,
    UtilityBill,
    Rent,
    MiningSale
}

public class SharedMoneyManager : NetworkBehaviour
{
    public static SharedMoneyManager Instance;
    public static event System.Action<int, int> SharedMoneyChanged;

    [Header("共同金庫設定")]
    [SerializeField] private int startSharedMoney = 0;

    [Header("開発用")]
    [Tooltip("Editor/Development Buildでのみ、Kキーによる+100Rを許可します。")]
    [SerializeField] private bool enableDebugGrant;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI sharedMoneyText;

    private NetworkVariable<int> sharedMoney = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private ulong transactionSequence;

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
            sharedMoney.Value = Mathf.Max(0, startSharedMoney);
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!enableDebugGrant) return;
        if (!IsServer) return;

        // テスト用：HostだけKキーで共同金庫に100円追加
        if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
        {
            TryAdd(100, SharedMoneyReason.DebugGrant, "Host K key");
        }
#endif
    }

    /// <summary>
    /// Server側で共有口座へ正の金額を入金する。
    /// 不正値・Server以外・int上限超過は拒否する。
    /// </summary>
    public bool TryAdd(
        int amount,
        SharedMoneyReason reason,
        string context = null)
    {
        if (!CanApplyTransaction(amount, "入金", reason)) return false;

        long nextBalance = (long)sharedMoney.Value + amount;
        if (nextBalance > int.MaxValue)
        {
            Debug.LogError(
                $"[Money] 入金拒否: int上限超過 amount={amount} reason={reason}");
            return false;
        }

        ApplyTransaction((int)nextBalance, amount, reason, context);
        return true;
    }

    /// <summary>現在の共同口座残高（全員が参照可）。</summary>
    public int CurrentMoney => sharedMoney.Value;

    public bool CanPay(int amount)
    {
        return amount >= 0 && sharedMoney.Value >= amount;
    }

    /// <summary>
    /// 購入・BET・家賃など、全額を払える場合だけServer側で減算する。
    /// 残高不足時は残高を変更しない。
    /// </summary>
    public bool TrySpend(
        int amount,
        SharedMoneyReason reason,
        string context = null)
    {
        if (!CanApplyTransaction(amount, "支払い", reason)) return false;

        if (sharedMoney.Value < amount)
        {
            Debug.LogWarning(
                $"[Money] 支払い拒否: 残高不足 amount={amount} " +
                $"balance={sharedMoney.Value} reason={reason} context={context}");
            return false;
        }

        ApplyTransaction(sharedMoney.Value - amount, -amount, reason, context);
        return true;
    }

    /// <summary>
    /// 罰金・請求など、残高を下限0として払える分だけServer側で徴収する。
    /// 実際に徴収した金額を返す。
    /// </summary>
    public int SpendUpTo(
        int requestedAmount,
        SharedMoneyReason reason,
        string context = null)
    {
        if (!CanApplyTransaction(requestedAmount, "上限付き徴収", reason))
            return 0;

        int actualAmount = Mathf.Min(requestedAmount, sharedMoney.Value);
        if (actualAmount <= 0) return 0;

        ApplyTransaction(
            sharedMoney.Value - actualAmount,
            -actualAmount,
            reason,
            context);
        return actualAmount;
    }

    private bool CanApplyTransaction(
        int amount,
        string operation,
        SharedMoneyReason reason)
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                $"[Money] {operation}拒否: Server以外からの変更 reason={reason}");
            return false;
        }

        if (amount <= 0)
        {
            Debug.LogWarning(
                $"[Money] {operation}拒否: amount={amount} reason={reason}");
            return false;
        }

        return true;
    }

    private void ApplyTransaction(
        int newBalance,
        int signedAmount,
        SharedMoneyReason reason,
        string context)
    {
        int oldBalance = sharedMoney.Value;
        sharedMoney.Value = Mathf.Max(0, newBalance);
        transactionSequence++;

        string sign = signedAmount >= 0 ? "+" : "";
        Debug.Log(
            $"[Money #{transactionSequence}] {reason} {sign}{signedAmount}R " +
            $"{oldBalance}R -> {sharedMoney.Value}R" +
            (string.IsNullOrEmpty(context) ? "" : $" ({context})"));
    }

    private void OnSharedMoneyChanged(int oldValue, int newValue)
    {
        UpdateSharedMoneyUI(newValue);

        if (oldValue != newValue)
            SharedMoneyChanged?.Invoke(oldValue, newValue);
    }

    private void UpdateSharedMoneyUI(int value)
    {
        if (sharedMoneyText == null) return;

        // 見出し（共同金庫）はHUDカード側が出すので金額のみ表示
        sharedMoneyText.text = "¥" + value.ToString("N0");
    }
}
