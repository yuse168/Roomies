using Unity.Netcode;
using UnityEngine;

/// <summary>
/// カジノの店の人。見て E で、上の階の鍵を売る（共同口座から払う）。鍵を売り終わったあと（と、鍵を売らない店の人）は、話しかけるとランダムなひとことを言う。
/// 会話の画面は作らない。ひとことは頭の上の吹き出し（NpcSpeechBubble。全員に見える）に出し、お金や鍵の案内は、話しかけた人の画面の知らせの帯にも出す。
/// 近くの人のほうへ体を向ける（各自の画面で。見た目だけ）。
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
    [Tooltip("この店の人が売る鍵の階（2 か 3）。0 にすると、鍵を売らずに話すだけの店の人")]
    [SerializeField, Range(0, 3)] private int sellsFloor = 2;

    [Header("ひとこと（知らせの帯の見出し）")]
    [SerializeField] private string clerkName = "カジノの店員";

    [Tooltip("話しかけたときのひとこと（前と違う物をランダムに 1 つ）")]
    [SerializeField] private string[] smallTalk =
    {
        "いらっしゃいませ。今夜もごゆっくり。",
        "上の階ほど、台はやさしいですよ。",
        "賭けすぎて、家賃を忘れないでくださいね。",
        "スロットは、7 がそろうとフィーバーです。",
        "ブラックジャックは、17 より下なら親がもう 1 枚引きます。",
        "ルーレットは、赤と黒がいちばん当たりやすいですよ。",
        "勝っているうちに帰るのが、いちばんの必勝法です。",
        "お友だちと一緒だと、口座もにぎやかですね。",
        "今日はツイている顔をしていますよ。",
    };

    [Header("吹き出し（全員に見える）")]
    [SerializeField] private NpcSpeechBubble bubble;

    [Header("近くの人のほうを向く（見た目だけ）")]
    [SerializeField, Min(0.5f)] private float lookDistance = 5f;
    [SerializeField, Range(0f, 90f)] private float lookMaxAngle = 55f;
    [SerializeField, Min(10f)] private float lookTurnSpeed = 140f;

    [Header("うなずき（見た目だけ）")]
    [Tooltip("売れたときに動かす軸。`Pivot_Body` を割り当てる")]
    [SerializeField] private Transform bodyPivot;
    [SerializeField] private float nodAngle = 14f;
    [SerializeField, Min(0.05f)] private float nodSeconds = 0.45f;

    private int lastTalk = -1;
    private float nodUntil;
    private float lookYaw;
    private float lookGoal;
    private float nextLookSearch;

    public int SellsFloor => sellsFloor;

    public override bool CanInteract() => IsSpawned && CasinoAccess.Instance != null;

    public override string Label
    {
        get
        {
            CasinoAccess access = CasinoAccess.Instance;
            if (sellsFloor < 2 || access == null || access.HasBoughtKeyFor(sellsFloor)) return "店の人と話す";
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

        // 鍵を売らない店の人：話すだけ
        if (sellsFloor < 2)
        {
            Chat();
            NodRpc(false);
            return;
        }

        switch (access.ServerBuyKey(sellsFloor, sender))
        {
            case CasinoAccess.BuyResult.Bought:
                Announce($"{sellsFloor}階の鍵を買った",
                    "エレベーターの鍵穴に挿すと、上の階へ行けるようになる。", NightEventManager.StyleFun);
                if (bubble != null) bubble.ServerSay("お買い上げ、ありがとうございます。");
                NodRpc(true);
                break;

            case CasinoAccess.BuyResult.NotEnoughMoney:
                Say(sender, $"{sellsFloor}階の鍵は ¥{CasinoAccess.KeyPrice(sellsFloor):N0} です。共同口座が足りないようですね。");
                break;

            case CasinoAccess.BuyResult.AlreadyBought:
                if (access.HasUnusedKey)
                    Say(sender, "鍵はもうお渡ししました。エレベーターの中の鍵穴に挿してください。");
                else
                    Chat();
                NodRpc(false);
                break;

            case CasinoAccess.BuyResult.NeedLowerKeyFirst:
                Say(sender, "先に下の階の鍵をお求めください。");
                break;
        }
    }

    /// <summary>お金や鍵の案内：吹き出し（全員）と、話しかけた人の画面の知らせの帯の両方に出す。</summary>
    private void Say(ulong clientId, string line)
    {
        if (bubble != null) bubble.ServerSay(line);
        if (NightEventManager.Instance != null)
            NightEventManager.Instance.ServerAnnounceTo(clientId, clerkName, line, NightEventManager.StyleInfo);
    }

    /// <summary>世間話：前と違うひとことを、ランダムに 1 つ。吹き出しだけに出す。</summary>
    private void Chat()
    {
        string line = "ごゆっくり。";
        if (smallTalk != null && smallTalk.Length > 0)
        {
            int i = Random.Range(0, smallTalk.Length);
            if (smallTalk.Length > 1 && i == lastTalk) i = (i + 1) % smallTalk.Length;
            lastTalk = i;
            line = smallTalk[i];
        }
        if (bubble != null) bubble.ServerSay(line);
        LastChatLine = line;
    }

    /// <summary>最後に言った世間話（確認の道具用。サーバーのみ）。</summary>
    public string LastChatLine { get; private set; } = "";

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

        // いちばん近い人のほうへ、体を向ける（探すのは 0.3 秒に 1 回）
        if (Time.time >= nextLookSearch)
        {
            nextLookSearch = Time.time + 0.3f;
            lookGoal = 0f;
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.SpawnManager != null)
            {
                float best = lookDistance * lookDistance;
                foreach (NetworkObject obj in nm.SpawnManager.SpawnedObjectsList)
                {
                    if (!obj.IsPlayerObject) continue;
                    Vector3 to = obj.transform.position - transform.position;
                    to.y = 0f;
                    if (to.sqrMagnitude > best || to.sqrMagnitude < 0.01f) continue;
                    best = to.sqrMagnitude;
                    lookGoal = Mathf.Clamp(Vector3.SignedAngle(transform.forward, to, Vector3.up), -lookMaxAngle, lookMaxAngle);
                }
            }
        }
        lookYaw = Mathf.MoveTowards(lookYaw, lookGoal, lookTurnSpeed * Time.deltaTime);
        bodyPivot.localRotation = Quaternion.Euler(angle, lookYaw, 0f);
    }
}
