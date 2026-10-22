using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 家に置いてある「触れる設備」の共通の形（冷蔵庫・ブレーカーなど）。
///
/// 規約（AGENTS.md）：
///  ・見た目は `Visual` の下。動く部品は `Pivot_〇〇` を軸にする。
///  ・プレハブをシーンに置いて使う。実行時に生成しない。
///  ・判定や金額は `[SerializeField]` でインスペクターから触れるようにする。
///
/// 共有状態（在庫・故障など）はサーバーだけが変える。
/// </summary>
public abstract class HomeAppliance : NetworkBehaviour
{
    [Header("触れる範囲")]
    [Tooltip("プレイヤーが近づいたと見なす距離（サーバー側の検証にも使う）")]
    [SerializeField, Min(0.5f)] protected float interactDistance = 3f;

    /// <summary>いま操作できるか（できない時はHUDに出さない）。</summary>
    public abstract bool CanInteract();

    /// <summary>HUDに出す文言（「食材を補充 ¥300」など。キー表示は呼び出し側が付ける）。</summary>
    public abstract string Label { get; }

    /// <summary>プレイヤーが操作した（各クライアントから呼ばれる）。</summary>
    public abstract void Interact();

    /// <summary>サーバー側の距離チェック。手元の座標を信用しない。</summary>
    protected bool ServerIsNear(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.ConnectedClients.TryGetValue(clientId, out var client)) return false;

        NetworkObject player = client.PlayerObject;
        if (player == null) return false;

        float sqr = (player.transform.position - transform.position).sqrMagnitude;
        return sqr <= interactDistance * interactDistance * 2.25f;   // 身長ぶんの余裕
    }

    /// <summary>夜の自由行動中か（設備の多くは夜だけ使う）。</summary>
    protected static bool IsNight =>
        DayManager.Instance != null && DayManager.Instance.IsSpawned &&
        DayManager.Instance.IsNight && !DayManager.Instance.IsGameOver;

    /// <summary>その場の全員へ短い告知を出す（サーバーから）。</summary>
    protected static void Announce(string title, string body, byte style)
    {
        if (NightEventManager.Instance != null)
            NightEventManager.Instance.ServerAnnounce(title, body, style);
    }
}
