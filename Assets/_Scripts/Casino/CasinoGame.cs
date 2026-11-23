using Unity.Netcode;
using UnityEngine;

/// <summary>
/// カジノの新しいゲーム（ルーレット・ハイ＆ロー・チンチロ）の共通の形。
/// プレイヤーの操作は、いまあるものだけを使う：見て E（決める）、R（2 つ目の操作）、ホイール／←→（賭け金）。
/// `PlayerInteract` は、見ている当たり判定の親にこの型があれば、ここへ渡すだけ。
///
/// 規約（AGENTS.md）：
///  ・台・画面・賭ける場所はプレハブに置く。実行時に作らない。数値はインスペクターで直せる。
///  ・お金と勝ち負けはサーバーだけが決める。支払いは SharedMoneyManager.TrySpend（払えたときだけ進む）。
/// </summary>
public abstract class CasinoGame : NetworkBehaviour
{
    [Header("Server検証")]
    [Tooltip("この距離より遠い人の操作は受け付けない（台の当たり判定のいちばん近い点まで）")]
    [SerializeField, Min(0.5f)] protected float serverInteractDistance = 4f;
    [Tooltip("距離を測る当たり判定（台の本体）")]
    [SerializeField] protected Collider bodyCollider;

    /// <summary>画面に出す操作の案内（キーの表示も含めた 1 行）。出すものがなければ null。</summary>
    public abstract string GetLabel(Collider lookedAt, ulong clientId);

    /// <summary>E を押した。</summary>
    public abstract void Interact(Collider lookedAt, PlayerEarning earning);

    /// <summary>R を押した（取り消す・降りる など）。使わないゲームは何もしない。</summary>
    public virtual void Secondary(Collider lookedAt, PlayerEarning earning) { }

    /// <summary>ホイール／←→で賭け金を変えた（+1 か -1）。自分の画面の中だけの値。</summary>
    public virtual void ChangeBet(int direction) { }

    // ---- 案内の文字 ----

    protected static string Key(GameAction action) =>
        $"<color=#FFA31F>{GameSettings.GetKeyLabel(action)}</color>";

    protected const string WheelTag = "<color=#FFA31F>WHEEL</color>";

    // ---- サーバーの共通の確かめ ----

    /// <summary>その人が、自分のプレイヤーの名義で、台の近くから操作しているか。</summary>
    protected bool ServerValidatePlayer(ulong playerObjectId, ulong senderClientId, out PlayerEarning earning)
    {
        earning = null;
        if (NetworkManager.Singleton == null) return false;
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerObjectId, out NetworkObject player)) return false;
        if (player.OwnerClientId != senderClientId) return false;

        earning = player.GetComponent<PlayerEarning>();
        if (earning == null) return false;

        Vector3 p = player.transform.position;
        Vector3 nearest = bodyCollider != null ? bodyCollider.ClosestPoint(p) : transform.position;
        return (nearest - p).sqrMagnitude <= serverInteractDistance * serverInteractDistance;
    }

    /// <summary>その人のプレイヤーの稼ぎの記録（もういなければ null）。</summary>
    protected static PlayerEarning ServerFindEarning(ulong playerObjectId)
    {
        if (NetworkManager.Singleton == null) return null;
        return NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerObjectId, out NetworkObject player)
            ? player.GetComponent<PlayerEarning>()
            : null;
    }

    /// <summary>その人だけに、画面上の知らせの帯を出す（サーバーから）。</summary>
    protected static void Tell(ulong clientId, string title, string body, byte style = NightEventManager.StyleInfo)
    {
        if (NightEventManager.Instance != null)
            NightEventManager.Instance.ServerAnnounceTo(clientId, title, body, style);
    }

    /// <summary>サーバーの時計（全員で同じ値。演出の始まりをそろえるのに使う）。</summary>
    protected double ServerNow =>
        NetworkManager != null ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
}
