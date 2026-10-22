using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 寝るためのベッド。夜の自由行動中だけ使える。
///
/// ・E で寝る。寝ている間は動けない（もう一度 E で起きる）
/// ・1つのベッドに寝られるのは1人
/// ・**全員が寝たら朝になる**（<see cref="SleepManager"/> が判定する）
/// ・朝まで寝ていた人は、次の朝のあいだ体が軽い（ぐっすり）
///
/// 規約（AGENTS.md）：見た目は `Visual` の下。プレハブをシーンに置く。
/// 寝ている人の管理はサーバーだけが変える。
/// </summary>
public class Bed : HomeAppliance
{
    [Header("寝る位置")]
    [Tooltip("寝たときに移される位置。見た目の外に置いた空オブジェクト（Anchor_Sleep）。")]
    [SerializeField] private Transform sleepAnchor;

    [Header("音")]
    [SerializeField] private string sleepSoundKey = "furniture/place";

    // 誰が寝ているか（空は ulong.MaxValue）
    private readonly NetworkVariable<ulong> sleeper =
        new(ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>誰か寝ているか。</summary>
    public bool IsOccupied => sleeper.Value != ulong.MaxValue;

    /// <summary>自分がこのベッドで寝ているか（手元の判定）。</summary>
    public bool IsMine =>
        NetworkManager.Singleton != null && sleeper.Value == NetworkManager.Singleton.LocalClientId;

    public override void OnNetworkSpawn()
    {
        sleeper.OnValueChanged += OnSleeperChanged;
        DayManager.OnMorningArrived += OnMorning;
    }

    public override void OnNetworkDespawn()
    {
        sleeper.OnValueChanged -= OnSleeperChanged;
        DayManager.OnMorningArrived -= OnMorning;
    }

    private void OnMorning()
    {
        // 朝になったら全員起きる
        if (IsServer && IsOccupied) ServerWake();
    }

    public override bool CanInteract()
    {
        if (!IsSpawned || !IsNight) return false;
        return IsMine || !IsOccupied;
    }

    public override string Label => IsMine ? "起きる" : "寝る";

    public override void Interact()
    {
        if (!IsSpawned) return;
        ToggleServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ToggleServerRpc(RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;

        // 起きる（自分が寝ているときだけ）
        if (sleeper.Value == sender)
        {
            ServerWake();
            return;
        }

        if (!IsNight || IsOccupied || !ServerIsNear(sender)) return;

        sleeper.Value = sender;
        SleepManager.ServerNotifySleep(sender, true);
        PlaySoundRpc();
    }

    /// <summary>いま寝ている人を起こす（サーバーのみ）。</summary>
    public void ServerWake()
    {
        if (!IsServer || !IsOccupied) return;

        ulong who = sleeper.Value;
        sleeper.Value = ulong.MaxValue;
        SleepManager.ServerNotifySleep(who, false);
    }

    [Rpc(SendTo.Everyone)]
    private void PlaySoundRpc()
    {
        if (!string.IsNullOrEmpty(sleepSoundKey))
            GameAudio.PlayAt(sleepSoundKey, transform.position, 0.8f, 0.05f);
    }

    /// <summary>
    /// 寝ている人の見た目と操作を切り替える。
    /// 動けなくするのは本人の画面だけ（カメラと移動は手元が持っているため）。
    /// </summary>
    private void OnSleeperChanged(ulong previous, ulong current)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        if (previous == nm.LocalClientId) SetLocalSleeping(false);
        if (current == nm.LocalClientId) SetLocalSleeping(true);
    }

    private void SetLocalSleeping(bool sleeping)
    {
        var nm = NetworkManager.Singleton;
        NetworkObject player = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
        var movement = player != null ? player.GetComponent<PlayerMovement>() : null;
        if (movement == null) return;

        movement.SetSleeping(sleeping);

        // 寝るときだけベッドの位置へ寄せる（起きるときは動かさない）
        if (sleeping && sleepAnchor != null)
            movement.TeleportTo(sleepAnchor.position, sleepAnchor.rotation);
    }
}
