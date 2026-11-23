using Unity.Netcode;
using UnityEngine;

/// <summary>
/// カジノの店の人。見て E で、上の階の鍵を売る（共同口座から払う）。
/// 会話の画面は作らない。店の人のひとことは、画面上の知らせの帯で出す（冷蔵庫の補充と同じ作り）。
///
/// プレハブ：Assets/_Prefabs/Casino/CasinoClerk.prefab
///  ・ルート：NetworkObject・このスクリプト・話しかける当たり判定
///  ・Visual：見た目だけ（差し替えるときは Visual の中身を入れ替える）
///  ・Pivot_Body：うなずきで動かす軸（中の Visual が体）
/// どの階の鍵を売るか（sellsFloor）はシーンに置いた物ごとにインスペクターで決める。値段は GameBalance。
/// </summary>
public class CasinoClerk : HomeAppliance
{
    [Header("売る鍵")]
    [Tooltip("この店の人が売る鍵の階（2 か 3）")]
    [SerializeField, Range(2, 3)] private int sellsFloor = 2;

    [Header("ひとこと（知らせの帯の見出し）")]
    [SerializeField] private string clerkName = "カジノの店員";

    [Tooltip("鍵を売ったあとに話しかけたときのひとこと（順番に出す）")]
    [SerializeField] private string[] smallTalk =
    {
        "上の階ほど、台はやさしいですよ。",
        "賭けすぎて家賃を忘れないでくださいね。",
        "今夜もごゆっくり。",
    };

    [Header("うなずき（見た目だけ）")]
    [Tooltip("売れたときに動かす軸。`Pivot_Body` を割り当てる")]
    [SerializeField] private Transform bodyPivot;
    [SerializeField] private float nodAngle = 14f;
    [SerializeField, Min(0.05f)] private float nodSeconds = 0.45f;

    private int talkIndex;
    private float nodUntil;

    public int SellsFloor => sellsFloor;

    public override bool CanInteract() => IsSpawned && CasinoAccess.Instance != null;

    public override string Label
    {
        get
        {
            CasinoAccess access = CasinoAccess.Instance;
            if (access == null || access.HasBoughtKeyFor(sellsFloor)) return "店の人と話す";
            return $"{sellsFloor}階の鍵を買う ¥{CasinoAccess.KeyPrice(sellsFloor):N0}";
        }
    }

    public override void Interact()
    {
        if (!IsSpawned) return;
        TalkServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void TalkServerRpc(RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!ServerIsNear(sender)) return;

        CasinoAccess access = CasinoAccess.Instance;
        if (access == null) return;

        switch (access.ServerBuyKey(sellsFloor, sender))
        {
            case CasinoAccess.BuyResult.Bought:
                Announce($"{sellsFloor}階の鍵を買った",
                    "エレベーターの鍵穴に挿すと、上の階へ行けるようになる。", NightEventManager.StyleFun);
                NodRpc(true);
                break;

            case CasinoAccess.BuyResult.NotEnoughMoney:
                Say(sender, $"{sellsFloor}階の鍵は ¥{CasinoAccess.KeyPrice(sellsFloor):N0} です。共同口座が足りないようですね。");
                break;

            case CasinoAccess.BuyResult.AlreadyBought:
                if (access.HasUnusedKey)
                    Say(sender, "鍵はもうお渡ししました。エレベーターの中の鍵穴に挿してください。");
                else
                {
                    string line = smallTalk != null && smallTalk.Length > 0
                        ? smallTalk[talkIndex++ % smallTalk.Length]
                        : "ごゆっくり。";
                    Say(sender, line);
                }
                NodRpc(false);
                break;

            case CasinoAccess.BuyResult.NeedLowerKeyFirst:
                Say(sender, "先に下の階の鍵をお求めください。");
                break;
        }
    }

    private void Say(ulong clientId, string line)
    {
        if (NightEventManager.Instance != null)
            NightEventManager.Instance.ServerAnnounceTo(clientId, clerkName, line, NightEventManager.StyleInfo);
    }

    // ---- うなずき（見た目だけ。各クライアントで動かす） ----

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void NodRpc(bool sold)
    {
        nodUntil = Time.time + nodSeconds;
        if (sold) GameAudio.PlayAt("ui/confirm", transform.position);
    }

    private void Update()
    {
        if (bodyPivot == null) return;

        float left = nodUntil - Time.time;
        float angle = left > 0f ? Mathf.Sin((1f - left / nodSeconds) * Mathf.PI) * nodAngle : 0f;
        bodyPivot.localRotation = Quaternion.Euler(angle, 0f, 0f);
    }
}
