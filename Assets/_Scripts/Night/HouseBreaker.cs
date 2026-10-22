using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ブレーカー。停電の夜に上げ直すと、朝を待たずに明かりが戻る。
///
/// ・停電していないときは触れない（HUDにも出ない）
/// ・停電中に E → サーバーが停電を解除し、全員の画面が明るくなる
/// ・レバーの見た目は `Pivot_Lever` を倒す／起こすだけ（AGENTS.mdのプレハブ規約）
/// </summary>
public class HouseBreaker : HomeAppliance
{
    public static HouseBreaker Instance { get; private set; }

    [Header("レバーの見た目")]
    [Tooltip("倒れる軸。`Pivot_Lever` を割り当てる")]
    [SerializeField] private Transform leverPivot;
    [SerializeField] private float trippedAngle = 35f;
    [SerializeField, Min(0.05f)] private float leverMoveSeconds = 0.2f;

    // 停電中かどうか（サーバーが夜イベントから設定する）
    private readonly NetworkVariable<bool> tripped = new(false);

    private float leverAngle;

    public bool IsTripped => tripped.Value;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        tripped.OnValueChanged += OnTrippedChanged;
        ApplyLever(tripped.Value, instant: true);
    }

    public override void OnNetworkDespawn()
    {
        tripped.OnValueChanged -= OnTrippedChanged;
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    public override bool CanInteract() => IsSpawned && tripped.Value;

    public override string Label => "ブレーカーを上げる";

    public override void Interact()
    {
        if (!IsSpawned) return;
        ResetServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ResetServerRpc(RpcParams rpcParams = default)
    {
        if (!tripped.Value) return;
        if (!ServerIsNear(rpcParams.Receive.SenderClientId)) return;

        tripped.Value = false;

        // 停電の解除は夜イベント側が全員へ配る（明かりを戻す演出もそこ）
        if (NightEventManager.Instance != null)
            NightEventManager.Instance.ServerFixBlackout();
    }

    /// <summary>夜イベント「停電」から呼ぶ（サーバーのみ）。</summary>
    public void ServerTrip()
    {
        if (!IsServer) return;
        tripped.Value = true;
    }

    /// <summary>朝になったら黙って戻す（サーバーのみ）。</summary>
    public void ServerReset()
    {
        if (!IsServer) return;
        tripped.Value = false;
    }

    private void OnTrippedChanged(bool oldValue, bool newValue) => ApplyLever(newValue, instant: false);

    private void ApplyLever(bool isTripped, bool instant)
    {
        float target = isTripped ? trippedAngle : 0f;
        if (instant) leverAngle = target;
        leverTarget = target;
        if (leverPivot != null && instant)
            leverPivot.localRotation = Quaternion.Euler(leverAngle, 0f, 0f);
    }

    private float leverTarget;

    private void Update()
    {
        if (leverPivot == null || Mathf.Approximately(leverAngle, leverTarget)) return;

        leverAngle = Mathf.MoveTowards(
            leverAngle, leverTarget, Mathf.Abs(trippedAngle) / leverMoveSeconds * Time.deltaTime);
        leverPivot.localRotation = Quaternion.Euler(leverAngle, 0f, 0f);
    }
}
