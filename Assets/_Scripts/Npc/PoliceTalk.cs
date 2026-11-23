using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 巡回中の警察に話しかける（見て E）。悪いこと（紙袋を運ぶ）をしていない人だけが話せる。
/// 話しかけると、警察は立ち止まってこちらを向き、ランダムなひとことを吹き出しで言う（全員に見える）。
/// 追いかけている最中・捕まえる流れには関わらない（SmugglingPolicePatrol が断る）。
///
/// プレハブ：Police_Placeholder.prefab のルートに置く（配置ツール Roomies/NPC/Build Talk）。
/// </summary>
public class PoliceTalk : HomeAppliance
{
    [Header("参照（同じルートの部品）")]
    [SerializeField] private SmugglingPolicePatrol patrol;
    [SerializeField] private NpcSpeechBubble bubble;

    [Header("せりふ（話しかけるたびに、前と違う物をランダムに 1 つ）")]
    [SerializeField] private string[] lines =
    {
        "夜は危ないから、早く帰りなさい。",
        "こんな時間に散歩かい？ 気をつけて。",
        "このあたりは最近、物騒でね。",
        "怪しい紙袋を見かけたら、教えてくれ。",
        "寄り道せずに、まっすぐ帰るんだぞ。",
        "カジノは、ほどほどにな。",
        "家賃はちゃんと払っているかい？",
        "巡回中だ。何かあったら声をかけてくれ。",
    };

    private int lastLine = -1;

    public override bool CanInteract()
    {
        if (!IsSpawned || patrol == null || !patrol.IsOnDuty) return false;
        // 紙袋を運んでいる間は、話しかける案内を出さない（近づけば捕まる）
        var nm = NetworkManager.Singleton;
        NetworkObject me = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
        SmugglingPlayer smuggler = me != null ? me.GetComponent<SmugglingPlayer>() : null;
        return smuggler == null || (!smuggler.HasBag && !smuggler.IsJailed);
    }

    public override string Label => "警察と話す";

    public override void Interact()
    {
        if (!IsSpawned) return;
        TalkServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void TalkServerRpc(RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (patrol == null || !ServerIsNear(sender)) return;
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.ConnectedClients.TryGetValue(sender, out var client) || client.PlayerObject == null) return;
        SmugglingPlayer who = client.PlayerObject.GetComponent<SmugglingPlayer>();
        if (who == null || !patrol.ServerTryTalk(who)) return;

        if (bubble != null && lines != null && lines.Length > 0) bubble.ServerSay(lines[NextLine()]);
    }

    private int NextLine()
    {
        int i = Random.Range(0, lines.Length);
        if (lines.Length > 1 && i == lastLine) i = (i + 1) % lines.Length;
        lastLine = i;
        return i;
    }
}
