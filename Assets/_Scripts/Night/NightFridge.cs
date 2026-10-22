using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 冷蔵庫。夜のうちに食材を買って入れておくと、次の食費請求を肩代わりしてくれる。
///
/// ・夜に E で「食材を補充」（共同口座から `stockCost` を支払う）
/// ・食費の夜イベントが来たとき、在庫があれば請求を肩代わりして在庫が無くなる
/// ・夜イベント「冷蔵庫の故障」でこの冷蔵庫が壊れる。壊れている間は補充できない
/// ・修理は夜に E（`repairCost` を支払う）。朝になっても直らない
///
/// 見た目：`Visual` の下。扉は `Pivot_Door` を回す（AGENTS.mdのプレハブ規約）。
/// </summary>
public class NightFridge : HomeAppliance
{
    public static NightFridge Instance { get; private set; }

    [Header("金額（インスペクターで調整）")]
    [Tooltip("食材の補充にかかる額")]
    [SerializeField, Min(0)] private int stockCost = 300;

    [Tooltip("故障を直すのにかかる額")]
    [SerializeField, Min(0)] private int repairCost = 400;

    [Header("扉の見た目")]
    [Tooltip("開閉させる軸。`Pivot_Door` を割り当てる")]
    [SerializeField] private Transform doorPivot;
    [SerializeField] private float doorOpenAngle = -100f;
    [SerializeField, Min(0.05f)] private float doorMoveSeconds = 0.35f;
    [Tooltip("補充したあと扉が開いたままにならないよう、自動で閉じるまでの秒数")]
    [SerializeField, Min(0.1f)] private float doorAutoCloseSeconds = 1.2f;

    // 共有状態（サーバーだけが変える）
    private readonly NetworkVariable<bool> stocked = new(false);
    private readonly NetworkVariable<bool> broken = new(false);

    private float doorAngle;
    private float doorTarget;
    private float doorCloseAt;

    /// <summary>食材が入っているか。</summary>
    public bool IsStocked => stocked.Value;

    /// <summary>壊れているか。</summary>
    public bool IsBroken => broken.Value;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    public override bool CanInteract()
    {
        if (!IsSpawned || !IsNight) return false;
        if (broken.Value) return true;        // 修理できる
        return !stocked.Value;                // 既に入っていれば用はない
    }

    public override string Label
    {
        get
        {
            if (broken.Value) return $"冷蔵庫を直す ¥{repairCost:N0}";
            if (stocked.Value) return "食材は入っている";
            return $"食材を補充 ¥{stockCost:N0}";
        }
    }

    public override void Interact()
    {
        if (!IsSpawned) return;
        UseServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void UseServerRpc(RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!IsNight || !ServerIsNear(sender)) return;

        var money = SharedMoneyManager.Instance;
        if (money == null) return;

        if (broken.Value)
        {
            if (!money.TrySpend(repairCost, SharedMoneyReason.UtilityBill, "Fridge repair"))
            {
                Announce("直せない", $"修理に ¥{repairCost:N0} 足りない……", NightEventManager.StyleInfo);
                return;
            }

            broken.Value = false;
            Announce("冷蔵庫を直した", "これでまた食材をしまえる。", NightEventManager.StyleFun);
            return;
        }

        if (stocked.Value) return;

        if (!money.TrySpend(stockCost, SharedMoneyReason.UtilityBill, "Fridge stock"))
        {
            Announce("買えなかった", $"食材代の ¥{stockCost:N0} が足りない……", NightEventManager.StyleInfo);
            return;
        }

        stocked.Value = true;
        OpenDoorRpc();
        Announce("食材を買い込んだ", "次の食費はこれでしのげる。", NightEventManager.StyleFun);
    }

    /// <summary>食費イベントから呼ぶ。在庫があれば使い切って true を返す（サーバーのみ）。</summary>
    public bool ServerConsumeStock()
    {
        if (!IsServer || !stocked.Value || broken.Value) return false;

        stocked.Value = false;
        OpenDoorRpc();
        return true;
    }

    /// <summary>夜イベント「冷蔵庫の故障」から呼ぶ（サーバーのみ）。</summary>
    public void ServerBreak()
    {
        if (!IsServer) return;
        broken.Value = true;
        stocked.Value = false;      // 中身は傷んだ
    }

    // ---- 扉の開閉（見た目だけ。各クライアントで動かす） ----

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void OpenDoorRpc()
    {
        doorTarget = doorOpenAngle;
        doorCloseAt = Time.time + doorAutoCloseSeconds;
    }

    private void Update()
    {
        if (doorPivot == null) return;

        if (doorTarget != 0f && Time.time >= doorCloseAt) doorTarget = 0f;

        if (!Mathf.Approximately(doorAngle, doorTarget))
        {
            doorAngle = Mathf.MoveTowards(
                doorAngle, doorTarget, Mathf.Abs(doorOpenAngle) / doorMoveSeconds * Time.deltaTime);
            doorPivot.localRotation = Quaternion.Euler(0f, doorAngle, 0f);
        }
    }
}
