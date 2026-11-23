using Unity.Netcode;
using UnityEngine;

/// <summary>
/// エレベーターのボタン 1 つ（呼ぶ・行き先・鍵穴）。見て E で押す。
/// エレベーターのプレハブの中に、階ごとに置いてある（実行時に作らない）。
/// 押したあとの判定はサーバーだけで行う（距離・鍵・動いている最中かどうか）。
/// </summary>
public class CasinoElevatorButton : HomeAppliance
{
    public enum Kind
    {
        Call,       // 外の呼びボタン：その階へ箱を呼ぶ
        Go,         // 箱の中の行き先ボタン
        KeySlot     // 箱の中の鍵穴：買ってある鍵を挿す
    }

    [SerializeField] private Kind kind = Kind.Call;

    [Tooltip("呼びボタンは置いてある階、行き先ボタンは行き先の階（0 = 1階）")]
    [SerializeField, Min(0)] private int floor;

    [SerializeField] private CasinoElevator elevator;

    [Header("見た目（置いてある物の表示を切り替えるだけ）")]
    [Tooltip("行ける階のときだけ出す、点いたボタンの見た目（行き先ボタン用。なくてもよい）")]
    [SerializeField] private GameObject litVisual;

    private bool shownLit;
    private bool litInitialized;

    public override bool CanInteract()
    {
        if (!IsSpawned || elevator == null) return false;
        if (kind == Kind.KeySlot) return CasinoAccess.Instance != null && CasinoAccess.Instance.HasUnusedKey;
        return true;
    }

    public override string Label
    {
        get
        {
            CasinoAccess access = CasinoAccess.Instance;
            switch (kind)
            {
                case Kind.KeySlot:
                    return access != null && access.HasUnusedKey ? $"{access.NextKeyFloor}階の鍵を挿す" : "鍵穴";

                case Kind.Call:
                    if (!elevator.IsIdle) return "エレベーターは動いている";
                    return elevator.CarFloor == floor ? "エレベーターは来ている" : "エレベーターを呼ぶ";

                default:
                    if (elevator.CarFloor == floor) return $"{floor + 1}階（いまここ）";
                    if (access != null && floor + 1 > access.UnlockedFloor) return $"{floor + 1}階（鍵が必要）";
                    return $"{floor + 1}階へ";
            }
        }
    }

    public override void Interact()
    {
        if (!IsSpawned) return;
        PressServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PressServerRpc(RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (elevator == null || !ServerIsNear(sender)) return;

        if (kind == Kind.KeySlot)
        {
            CasinoAccess access = CasinoAccess.Instance;
            int opened = access != null ? access.ServerUseKey() : 0;
            if (opened > 0)
            {
                Announce($"{opened}階が開いた", "エレベーターの行き先ボタンが点いた。", NightEventManager.StyleFun);
                PressedRpc();
            }
            return;
        }

        string refused = elevator.ServerRequest(floor);
        if (!string.IsNullOrEmpty(refused))
        {
            if (NightEventManager.Instance != null)
                NightEventManager.Instance.ServerAnnounceTo(sender, "エレベーター", refused + "。", NightEventManager.StyleInfo);
            return;
        }
        PressedRpc();
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void PressedRpc()
    {
        GameAudio.PlayAt("casino/elevator_button", transform.position, 0.8f, 0.05f, 10f);
    }

    private void Update()
    {
        if (litVisual == null || kind != Kind.Go) return;

        CasinoAccess access = CasinoAccess.Instance;
        bool lit = access == null || floor + 1 <= access.UnlockedFloor;
        if (litInitialized && lit == shownLit) return;
        litInitialized = true;
        shownLit = lit;
        litVisual.SetActive(lit);
    }
}
