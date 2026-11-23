using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// ブラックジャック台の上の実物：カード・チップ・点数の札（見た目と音だけ。勝ち負けは BlackjackTable が決める）。
///
/// カードとチップはすべてプレハブに置いてあり、ここでは出し入れして動かすだけ（実行時に物を作らない）。
/// 配置ツール：Roomies/Casino/2 Build Gimmick Prefabs（RoomiesCasinoBuilder.BlackjackCards.cs）。
///
/// カードは BlackjackTable が配る「カードの並び」（全員に同じ物が届く）に合わせて動かす：
///   増えた分 … 靴（シュー）から滑ってくる。伏せ札（'#'）は裏のまま置く
///   伏せ札が表の札に変わった … その場でめくる
///   空になった … 捨て札の箱へ片付ける
/// 点数の札は、カードが置かれるたびに、見えている札だけで数え直す。
/// </summary>
public class BlackjackTableView : MonoBehaviour
{
    public const char Hidden = '#';
    private const char FirstCard = 'A';      // 'A' + (数字-1)*4 + スート（0=♠ 1=♥ 2=♦ 3=♣）

    [System.Serializable]
    public class CardSlot
    {
        [Tooltip("カードの軸（Pivot_Card_〇）。これだけを動かす")]
        public Transform pivot;
        [Tooltip("表の面（絵を切り替える）")]
        public Renderer face;
    }

    [Header("カード（プレハブに置いてある枚数まで。手前＝遊ぶ人、奥＝親）")]
    [SerializeField] private CardSlot[] playerCards = new CardSlot[0];
    [SerializeField] private CardSlot[] dealerCards = new CardSlot[0];

    [Header("位置（Anchor_〇）")]
    [Tooltip("カードが出てくる所（靴の口）")]
    [SerializeField] private Transform anchorShoe;
    [Tooltip("片付ける所（捨て札の箱）")]
    [SerializeField] private Transform anchorDiscard;
    [Tooltip("1 枚目を置く所。2 枚目からは cardStep ずつずらす")]
    [SerializeField] private Transform anchorPlayerHand;
    [SerializeField] private Transform anchorDealerHand;
    [Tooltip("手の位置（Anchor_〇Hand）から見た、1 枚ごとのずらし")]
    [SerializeField] private Vector3 cardStep = new Vector3(0.112f, 0.0012f, -0.014f);

    [Header("カードの絵（1 枚の絵に 14 列 × 4 段。最後の列のいちばん上が裏面）")]
    [SerializeField, Min(1)] private int atlasColumns = 14;
    [SerializeField, Min(1)] private int atlasRows = 4;
    [Tooltip("マスの幅が絵の横幅に占める割合（146 / 2048）")]
    [SerializeField] private float cellU = 146f / 2048f;

    [Header("動き（秒）")]
    [SerializeField, Min(0.05f)] private float dealSeconds = 0.32f;
    [SerializeField, Min(0.05f)] private float flipSeconds = 0.3f;
    [SerializeField, Min(0.05f)] private float clearSeconds = 0.35f;
    [SerializeField, Min(0f)] private float dealArc = 0.06f;

    [Header("チップ（賭け金の山・ダブルダウンの山・払い戻しの山。下から順に入れる）")]
    [SerializeField] private Transform pivotBet;
    [SerializeField] private GameObject[] betChips = new GameObject[0];
    [SerializeField] private Transform pivotDouble;
    [SerializeField] private GameObject[] doubleChips = new GameObject[0];
    [SerializeField] private Transform pivotPayout;
    [SerializeField] private GameObject[] payoutChips = new GameObject[0];
    [Tooltip("チップが出入りする所（親のチップ入れ）")]
    [SerializeField] private Transform anchorTray;
    [Tooltip("賭け金の山を置く所／払い戻しの山を置く所")]
    [SerializeField] private Transform anchorBet;
    [SerializeField] private Transform anchorPayout;
    [SerializeField, Min(0.05f)] private float chipSeconds = 0.45f;

    [Header("点数の札（台の上の小さな表示）")]
    [SerializeField] private TMP_Text playerScoreText;
    [SerializeField] private TMP_Text dealerScoreText;
    [SerializeField] private GameObject playerScorePlate;
    [SerializeField] private GameObject dealerScorePlate;
    [SerializeField] private Color scoreColor = new Color(1f, 0.93f, 0.75f);
    [SerializeField] private Color bustColor = new Color(1f, 0.4f, 0.4f);
    [SerializeField] private Color blackjackColor = new Color(1f, 0.82f, 0.3f);

    private readonly Queue<IEnumerator> queue = new();
    private Coroutine worker;
    private string shownPlayer = "", shownDealer = "";       // 届いている並び（これから動かす分も含む）
    private string landedPlayer = "", landedDealer = "";     // 台の上に置き終わった並び（点数の札はこれで数える）
    private MaterialPropertyBlock block;
    private Vector3 betHome, doubleHome, payoutHome;
    private bool homesTaken;
    private int betTier;

    /// <summary>カード 1 枚の符号（数字 1〜13、スート 0〜3）。</summary>
    public static char Encode(int rank, int suit) => (char)(FirstCard + (rank - 1) * 4 + suit);

    private static bool TryDecode(char code, out int rank, out int suit)
    {
        int i = code - FirstCard;
        rank = i / 4 + 1; suit = i % 4;
        return code != Hidden && i >= 0 && i < 52;
    }

    /// <summary>見えている札だけの点数（A は 11 か 1）。</summary>
    public static int HandValue(string codes, out bool hasHidden)
    {
        int total = 0, aces = 0;
        hasHidden = false;
        foreach (char c in codes)
        {
            if (!TryDecode(c, out int rank, out _)) { hasHidden = true; continue; }
            if (rank == 1) { total += 11; aces++; }
            else total += Mathf.Min(rank, 10);
        }
        while (total > 21 && aces > 0) { total -= 10; aces--; }
        return total;
    }

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        TakeHomes();
        SnapEmpty();
    }

    private void OnDisable()
    {
        // 見えない間は動かさない。戻ったときに、いまの並びへそろえ直す（BlackjackTable が Snap を呼ぶ）
        queue.Clear();
        worker = null;
    }

    private void TakeHomes()
    {
        if (homesTaken) return;
        homesTaken = true;
        if (pivotBet != null) betHome = pivotBet.localPosition;
        if (pivotDouble != null) doubleHome = pivotDouble.localPosition;
        if (pivotPayout != null) payoutHome = pivotPayout.localPosition;
    }

    // ================================================================ 外から呼ぶ

    /// <summary>動きなしで、その並びにそろえる（あとから入った人・表示が戻ったとき）。</summary>
    public void Snap(string player, string dealer, int betIndex, bool doubled)
    {
        TakeHomes();
        queue.Clear();
        if (worker != null) { StopCoroutine(worker); worker = null; }
        SnapEmpty();
        shownPlayer = player ?? ""; shownDealer = dealer ?? "";
        landedPlayer = shownPlayer; landedDealer = shownDealer;
        for (int i = 0; i < shownPlayer.Length && i < playerCards.Length; i++) PlaceCard(playerCards[i], anchorPlayerHand, i, shownPlayer[i]);
        for (int i = 0; i < shownDealer.Length && i < dealerCards.Length; i++) PlaceCard(dealerCards[i], anchorDealerHand, i, shownDealer[i]);
        RefreshScores();
        if (shownPlayer.Length > 0)
        {
            ShowStack(pivotBet, betChips, betIndex + 1, anchorBet);
            if (doubled) ShowStack(pivotDouble, doubleChips, betIndex + 1, anchorBet, doubleHome - betHome);
        }
    }

    /// <summary>カードの並びが変わった（BlackjackTable が、全員の画面で呼ぶ）。</summary>
    public void SetHands(string player, string dealer)
    {
        player ??= ""; dealer ??= "";
        if (!isActiveAndEnabled) { shownPlayer = player; shownDealer = dealer; return; }

        if (player.Length == 0 && dealer.Length == 0)
        {
            if (shownPlayer.Length > 0 || shownDealer.Length > 0) Enqueue(ClearRoutine());
            shownPlayer = shownDealer = "";
            return;
        }

        // 配る順：遊ぶ人 → 親 → 遊ぶ人 → 親 …（増えた分だけ）
        int count = Mathf.Max(player.Length, dealer.Length);
        for (int i = 0; i < count; i++)
        {
            if (i < player.Length && i < playerCards.Length)
            {
                if (i >= shownPlayer.Length) Enqueue(DealRoutine(playerCards[i], anchorPlayerHand, i, player[i], true));
                else if (shownPlayer[i] != player[i]) Enqueue(FlipRoutine(playerCards[i], i, player[i], true));
            }
            if (i < dealer.Length && i < dealerCards.Length)
            {
                if (i >= shownDealer.Length) Enqueue(DealRoutine(dealerCards[i], anchorDealerHand, i, dealer[i], false));
                else if (shownDealer[i] != dealer[i]) Enqueue(FlipRoutine(dealerCards[i], i, dealer[i], false));
            }
        }
        shownPlayer = player; shownDealer = dealer;
    }

    /// <summary>賭け金のチップを置く（index：賭け金の段。0 がいちばん低い）。</summary>
    public void PlaceBet(int betIndex)
    {
        betTier = betIndex;
        if (!isActiveAndEnabled) return;
        Enqueue(ChipRoutine(pivotBet, betChips, betIndex + 1, PlayerSide(anchorBet), LocalOf(anchorBet), Vector3.zero, true));
    }

    /// <summary>ダブルダウン：同じ高さの山を、もう 1 つ隣に置く。</summary>
    public void PlaceDouble()
    {
        if (!isActiveAndEnabled) return;
        Enqueue(ChipRoutine(pivotDouble, doubleChips, betTier + 1, PlayerSide(anchorBet), LocalOf(anchorBet), doubleHome - betHome, true));
    }

    /// <summary>結果：勝ちなら払い戻しの山が滑ってくる。負けなら賭け金の山が親のほうへ下がる。引き分けはそのまま。</summary>
    public void ShowResult(bool won, bool lost, bool blackjack)
    {
        if (!isActiveAndEnabled) return;
        if (won)
            Enqueue(ChipRoutine(pivotPayout, payoutChips, Mathf.Min(payoutChips.Length, betTier + 1 + (blackjack ? 2 : 0) + (pivotDouble != null && pivotDouble.gameObject.activeSelf ? betTier + 1 : 0)),
                LocalOf(anchorTray), LocalOf(anchorPayout), Vector3.zero, true));
        else if (lost)
        {
            Enqueue(ChipRoutine(pivotBet, betChips, -1, pivotBet != null ? pivotBet.localPosition : Vector3.zero, LocalOf(anchorTray), Vector3.zero, false));
            if (pivotDouble != null && pivotDouble.gameObject.activeSelf)
                Enqueue(ChipRoutine(pivotDouble, doubleChips, -1, pivotDouble.localPosition, LocalOf(anchorTray), Vector3.zero, false));
        }
    }

    /// <summary>まだ動かしている途中か（確認の道具用）。</summary>
    public bool IsBusy => worker != null;

    /// <summary>台の上に置き終わった並び（確認の道具用）。</summary>
    public string LandedPlayer => landedPlayer;
    public string LandedDealer => landedDealer;

    // ================================================================ 動きの列

    private void Enqueue(IEnumerator step)
    {
        queue.Enqueue(step);
        if (worker == null) worker = StartCoroutine(Work());
    }

    private IEnumerator Work()
    {
        while (queue.Count > 0) yield return queue.Dequeue();
        worker = null;
    }

    private IEnumerator DealRoutine(CardSlot slot, Transform hand, int index, char code, bool toPlayer)
    {
        if (slot == null || slot.pivot == null || hand == null) yield break;
        SetFace(slot, code);
        Transform parent = slot.pivot.parent;
        Vector3 from = anchorShoe != null ? parent.InverseTransformPoint(anchorShoe.position) : slot.pivot.localPosition;
        Vector3 to = HandPosition(hand, index, parent);
        Quaternion faceDown = hand.localRotation * Quaternion.Euler(0f, 0f, 180f), faceUp = hand.localRotation;
        Quaternion end = code == Hidden ? faceDown : faceUp;

        slot.pivot.localPosition = from;
        slot.pivot.localRotation = faceDown;
        slot.pivot.gameObject.SetActive(true);
        GameAudio.PlayAt("casino/card", transform.position, 0.9f, 0.08f, 12f);

        float t = 0f;
        bool counted = false;
        while (t < dealSeconds)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dealSeconds), eased = 1f - (1f - u) * (1f - u);
            Vector3 p = Vector3.Lerp(from, to, eased);
            p.y += Mathf.Sin(u * Mathf.PI) * dealArc;
            slot.pivot.localPosition = p;
            // 表向きに置く札は、滑ってくる途中の後ろ半分でめくる
            float turn = Mathf.InverseLerp(0.45f, 1f, u);
            slot.pivot.localRotation = Quaternion.Slerp(faceDown, end, turn);
            // 表が見えはじめた瞬間（半分まで回ったとき）に、点数の札へ足す。伏せ札は、置き終わったときに足す
            if (!counted && code != Hidden && turn >= 0.5f) { counted = true; CountDealt(code, toPlayer); }
            yield return null;
        }
        slot.pivot.localPosition = to;
        slot.pivot.localRotation = end;
        if (!counted) CountDealt(code, toPlayer);
    }

    private void CountDealt(char code, bool toPlayer)
    {
        if (toPlayer) landedPlayer += code; else landedDealer += code;
        RefreshScores();
    }

    private IEnumerator FlipRoutine(CardSlot slot, int index, char code, bool isPlayer)
    {
        if (slot == null || slot.pivot == null) yield break;
        SetFace(slot, code);
        Quaternion from = slot.pivot.localRotation;
        Quaternion to = code == Hidden ? from : from * Quaternion.Euler(0f, 0f, 180f);
        Vector3 home = slot.pivot.localPosition;
        GameAudio.PlayAt("casino/card", transform.position, 1f, 0.05f, 12f);
        float t = 0f;
        bool counted = false;
        while (t < flipSeconds)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / flipSeconds);
            slot.pivot.localRotation = Quaternion.Slerp(from, to, u);
            slot.pivot.localPosition = home + Vector3.up * (Mathf.Sin(u * Mathf.PI) * 0.07f);
            // 半分まで回ると表が見えはじめる。その瞬間に点数の札も数え直す（めくり終わるまで待つと、表が見えているのに「?」のままの間ができる）
            if (!counted && u >= 0.5f) { counted = true; CountFlipped(index, code, isPlayer); }
            yield return null;
        }
        slot.pivot.localRotation = to;
        slot.pivot.localPosition = home;
        if (!counted) CountFlipped(index, code, isPlayer);
    }

    private void CountFlipped(int index, char code, bool isPlayer)
    {
        if (isPlayer) landedPlayer = ReplaceAt(landedPlayer, index, code); else landedDealer = ReplaceAt(landedDealer, index, code);
        RefreshScores();
    }

    private IEnumerator ClearRoutine()
    {
        var moving = new List<Transform>();
        foreach (CardSlot s in playerCards) if (s != null && s.pivot != null && s.pivot.gameObject.activeSelf) moving.Add(s.pivot);
        foreach (CardSlot s in dealerCards) if (s != null && s.pivot != null && s.pivot.gameObject.activeSelf) moving.Add(s.pivot);
        if (moving.Count > 0) GameAudio.PlayAt("casino/card", transform.position, 0.8f, 0.05f, 12f);

        var from = new Vector3[moving.Count];
        for (int i = 0; i < moving.Count; i++) from[i] = moving[i].localPosition;
        landedPlayer = landedDealer = "";
        SetScoresVisible(false, false);
        float t = 0f;
        while (t < clearSeconds && moving.Count > 0)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / clearSeconds);
            for (int i = 0; i < moving.Count; i++)
            {
                Vector3 to = anchorDiscard != null ? moving[i].parent.InverseTransformPoint(anchorDiscard.position) : from[i];
                moving[i].localPosition = Vector3.Lerp(from[i], to, u * u);
            }
            yield return null;
        }
        SnapEmpty();
    }

    /// <summary>
    /// チップの山を動かす。count が 0 以上なら、その枚数を出して from → to へ滑らせる。
    /// count が負なら、いま出ている山を to へ滑らせて消す。
    /// </summary>
    private IEnumerator ChipRoutine(Transform pivot, GameObject[] chips, int count, Vector3 from, Vector3 to, Vector3 offset, bool stay)
    {
        if (pivot == null) yield break;
        if (count >= 0)
        {
            for (int i = 0; i < chips.Length; i++) if (chips[i] != null) chips[i].SetActive(i < count);
            pivot.gameObject.SetActive(count > 0);
            if (count == 0) yield break;
        }
        else if (!pivot.gameObject.activeSelf) yield break;

        from += offset; to += offset;
        GameAudio.PlayAt("casino/chips_slide", transform.position, 1f, 0.05f, 12f);
        float t = 0f;
        while (t < chipSeconds)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / chipSeconds);
            pivot.localPosition = Vector3.Lerp(from, to, 1f - (1f - u) * (1f - u));
            yield return null;
        }
        pivot.localPosition = to;
        if (!stay) pivot.gameObject.SetActive(false);
    }

    // ================================================================ 置く・数える

    private Vector3 LocalOf(Transform anchor) => anchor != null ? anchor.localPosition : Vector3.zero;

    /// <summary>賭け金の山が出てくる所：置く所より、遊ぶ人の側へ少し手前。</summary>
    private Vector3 PlayerSide(Transform anchor) => LocalOf(anchor) + new Vector3(0f, 0f, 0.22f);

    private Vector3 HandPosition(Transform hand, int index, Transform parent)
    {
        Vector3 world = hand.TransformPoint(cardStep * index);
        return parent.InverseTransformPoint(world);
    }

    private void PlaceCard(CardSlot slot, Transform hand, int index, char code)
    {
        if (slot == null || slot.pivot == null || hand == null) return;
        SetFace(slot, code);
        slot.pivot.localPosition = HandPosition(hand, index, slot.pivot.parent);
        slot.pivot.localRotation = code == Hidden ? hand.localRotation * Quaternion.Euler(0f, 0f, 180f) : hand.localRotation;
        slot.pivot.gameObject.SetActive(true);
    }

    private void ShowStack(Transform pivot, GameObject[] chips, int count, Transform anchor, Vector3 offset = default)
    {
        if (pivot == null) return;
        for (int i = 0; i < chips.Length; i++) if (chips[i] != null) chips[i].SetActive(i < count);
        pivot.localPosition = LocalOf(anchor) + offset;
        pivot.gameObject.SetActive(count > 0);
    }

    private void SnapEmpty()
    {
        foreach (CardSlot s in playerCards) if (s != null && s.pivot != null) s.pivot.gameObject.SetActive(false);
        foreach (CardSlot s in dealerCards) if (s != null && s.pivot != null) s.pivot.gameObject.SetActive(false);
        if (pivotBet != null) pivotBet.gameObject.SetActive(false);
        if (pivotDouble != null) pivotDouble.gameObject.SetActive(false);
        if (pivotPayout != null) pivotPayout.gameObject.SetActive(false);
        landedPlayer = landedDealer = "";
        SetScoresVisible(false, false);
    }

    /// <summary>表の面の絵を、その札に合わせる（マテリアルは共通のまま、絵の中の場所だけを替える）。</summary>
    private void SetFace(CardSlot slot, char code)
    {
        if (slot.face == null) return;
        int column = atlasColumns - 1, row = 0;                     // 伏せ札の間は、表も裏面の絵にしておく
        if (TryDecode(code, out int rank, out int suit)) { column = rank - 1; row = suit; }
        float v = 1f / atlasRows;
        block ??= new MaterialPropertyBlock();
        slot.face.GetPropertyBlock(block);
        block.SetVector("_BaseMap_ST", new Vector4(cellU, v, column * cellU, 1f - (row + 1) * v));
        slot.face.SetPropertyBlock(block);
    }

    private static string ReplaceAt(string s, int index, char c)
    {
        if (index < 0 || index >= s.Length) return s;
        return s.Substring(0, index) + c + s.Substring(index + 1);
    }

    /// <summary>点数の札を、台の上に置き終わった札だけで数え直す（配っている途中や、めくる前に点が出ないように）。</summary>
    private void RefreshScores()
    {
        SetScore(playerScoreText, landedPlayer);
        SetScore(dealerScoreText, landedDealer);
        SetScoresVisible(landedPlayer.Length > 0, landedDealer.Length > 0);
    }

    private void SetScore(TMP_Text text, string codes)
    {
        if (text == null) return;
        int total = HandValue(codes, out bool hidden);
        bool blackjack = !hidden && codes.Length == 2 && total == 21;
        text.text = hidden ? total + "+?" : blackjack ? "BJ" : total.ToString();
        text.color = total > 21 ? bustColor : blackjack ? blackjackColor : scoreColor;
    }

    private void SetScoresVisible(bool player, bool dealer)
    {
        if (playerScorePlate != null) playerScorePlate.SetActive(player);
        if (dealerScorePlate != null) dealerScorePlate.SetActive(dealer);
    }
}
