using Unity.Netcode;
using UnityEngine;

/// <summary>
/// カジノの上の階へ行く権利（鍵）の持ち主。GameRoom に 1 つ置く（配置ツール Roomies/Casino/3 Place In Open Scene）。
///
/// ・鍵は物として持ち歩かない。「部屋のみんなが鍵を持っている」という共有の状態にする（落とす・なくすが起きない）。
/// ・1 階の店の人から 2 階の鍵、2 階の店の人から 3 階の鍵を買う（共同口座から払う。値段は GameBalance）。
/// ・買った鍵をエレベーターの鍵穴に挿すと、その階へ行けるようになる。開けたあとは全員が使える。
/// ・買った数と挿した数はセーブに残る（RunSaveManager）。
///
/// 共有の状態はサーバーだけが変える。支払いは SharedMoneyManager.TrySpend だけで行う
/// （同時に 2 人が押しても、2 回目は「もう買ってある」で止まる）。
/// </summary>
public class CasinoAccess : NetworkBehaviour
{
    public static CasinoAccess Instance { get; private set; }

    /// <summary>いちばん上の階（1 階から数えて）。</summary>
    public const int TopFloor = 3;

    // 買った鍵の数（0〜2）と、鍵穴に挿した数（0〜2）。行ける階は 1 + 挿した数
    private readonly NetworkVariable<int> keysBought = new(0);
    private readonly NetworkVariable<int> keysUsed = new(0);

    /// <summary>買った鍵の数（セーブ用）。</summary>
    public int KeysBought => keysBought.Value;

    /// <summary>鍵穴に挿した鍵の数（セーブ用）。</summary>
    public int KeysUsed => keysUsed.Value;

    /// <summary>エレベーターで行ける、いちばん上の階（1〜3）。</summary>
    public int UnlockedFloor => 1 + keysUsed.Value;

    /// <summary>その階の鍵をもう買ってあるか（floor は 2 か 3）。</summary>
    public bool HasBoughtKeyFor(int floor) => keysBought.Value >= floor - 1;

    /// <summary>買ったけれど、まだ挿していない鍵があるか。</summary>
    public bool HasUnusedKey => keysBought.Value > keysUsed.Value;

    /// <summary>次に挿す鍵で開く階（挿していない鍵がないときは 0）。</summary>
    public int NextKeyFloor => HasUnusedKey ? keysUsed.Value + 2 : 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("CasinoAccess が複数あります。このオブジェクトを無効化します。");
            gameObject.SetActive(false);
            return;
        }
        Instance = this;
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    public enum BuyResult { Bought, AlreadyBought, NeedLowerKeyFirst, NotEnoughMoney, Unavailable }

    /// <summary>その階の鍵の値段。</summary>
    public static int KeyPrice(int floor) => GameBalance.Current.CasinoKeyPrice(floor);

    /// <summary>その階の鍵を買う（サーバーのみ）。払えたときだけ鍵が増える。</summary>
    public BuyResult ServerBuyKey(int floor, ulong clientId)
    {
        if (!IsServer || floor < 2 || floor > TopFloor) return BuyResult.Unavailable;
        if (HasBoughtKeyFor(floor)) return BuyResult.AlreadyBought;
        if (!HasBoughtKeyFor(floor - 1) && floor > 2) return BuyResult.NeedLowerKeyFirst;

        SharedMoneyManager money = SharedMoneyManager.Instance;
        if (money == null) return BuyResult.Unavailable;

        int price = KeyPrice(floor);
        if (price > 0 && !money.TrySpend(price, SharedMoneyReason.CasinoKey, $"floor={floor} client={clientId}"))
            return BuyResult.NotEnoughMoney;

        keysBought.Value = floor - 1;
        return BuyResult.Bought;
    }

    /// <summary>買ってある鍵を 1 本、鍵穴に挿す（サーバーのみ）。開いた階を返す（挿す鍵がなければ 0）。</summary>
    public int ServerUseKey()
    {
        if (!IsServer || !HasUnusedKey) return 0;
        keysUsed.Value = keysUsed.Value + 1;
        return UnlockedFloor;
    }

    /// <summary>セーブから戻す（サーバーのみ）。お金は動かさない。</summary>
    public void ServerRestore(int bought, int used)
    {
        if (!IsServer) return;
        bought = Mathf.Clamp(bought, 0, TopFloor - 1);
        keysBought.Value = bought;
        keysUsed.Value = Mathf.Clamp(used, 0, bought);
    }
}
