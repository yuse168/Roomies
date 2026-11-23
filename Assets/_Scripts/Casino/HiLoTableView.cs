using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ハイ＆ローの台の上の実物：カードとチップ（見た目と音だけ。勝ち負けは HiLoTable が決める）。
/// 考え方はブラックジャック（BlackjackTableView）と同じ。トランプの絵とマテリアルも同じ物を使う。
///
/// カードとチップはすべてプレハブに置いてあり、ここでは出し入れして動かすだけ（実行時に物を作らない）。
/// 配置ツール：Roomies/Casino/2 Build Gimmick Prefabs（RoomiesCasinoBuilder.HiLoCards.cs）。
///
/// 流れ：
///   はじめる … 賭け金のチップが置かれ、1 枚目が山から滑ってきて表向きに置かれる（これが「いまのカード」）
///   上か下か … 次のカードが、山から裏のまま滑ってきて、となりに置かれ、ひと呼吸おいてめくれる（これが新しい「いまのカード」）
///   外れ     … 賭け金のチップが、親のチップ入れへ下がる
///   受け取る … 払い戻しのチップが、チップ入れから滑ってくる
///   終わり   … 少し見せてから、カードを片付ける
/// </summary>
public class HiLoTableView : MonoBehaviour
{
    private const char FirstCard = 'A';      // BlackjackTableView.Encode と同じ符号：'A' + (数字-1)*4 + スート

    [Serializable]
    public class CardSlot
    {
        [Tooltip("カードの軸（Pivot_Card_〇）。これだけを動かす")]
        public Transform pivot;
        [Tooltip("表の面（絵を切り替える）")]
        public Renderer face;
    }

    [Header("カード（プレハブに置いてある枚数まで。1 枚目から順に、となりへ並べる）")]
    [SerializeField] private CardSlot[] cards = new CardSlot[0];

    [Header("位置（Anchor_〇）")]
    [Tooltip("カードが出てくる所（山の上）")]
    [SerializeField] private Transform anchorDeck;
    [Tooltip("片付ける所")]
    [SerializeField] private Transform anchorDiscard;
    [Tooltip("1 枚目を置く所。2 枚目からは cardStep ずつずらす")]
    [SerializeField] private Transform anchorRow;
    [Tooltip("並べる所（Anchor_Row）から見た、1 枚ごとのずらし")]
    [SerializeField] private Vector3 cardStep = new Vector3(0.175f, 0.0012f, 0f);

    [Header("カードの絵（1 枚の絵に 11 列 × 5 段。左上から ♠A〜K・♥・♦・♣、53 番目が裏面）")]
    [SerializeField, Min(1)] private int atlasColumns = 11;
    [Tooltip("マスの幅・高さが、絵の横幅・縦幅に占める割合（372 / 4096、409 / 2048）")]
    [SerializeField] private float cellU = 372f / 4096f;
    [SerializeField] private float cellV = 409f / 2048f;

    [Header("動き（秒）")]
    [SerializeField, Min(0.05f)] private float dealSeconds = 0.32f;
    [Tooltip("裏のまま置いてから、めくるまでの間")]
    [SerializeField, Min(0f)] private float holdSeconds = 0.18f;
    [SerializeField, Min(0.05f)] private float flipSeconds = 0.3f;
    [Tooltip("終わってから、カードを片付けはじめるまでの間（結果を見せる時間）")]
    [SerializeField, Min(0f)] private float clearDelaySeconds = 0.8f;
    [SerializeField, Min(0.05f)] private float clearSeconds = 0.35f;
    [SerializeField, Min(0f)] private float dealArc = 0.06f;
    [Tooltip("めくるときに浮かせる高さ。高いと、立ったカードが奥の「手持ちの額」の札に重なって見える")]
    [SerializeField, Min(0f)] private float flipLift = 0.015f;

    [Header("チップ（賭け金の山・払い戻しの山。下から順に入れる）")]
    [SerializeField] private Transform pivotBet;
    [SerializeField] private GameObject[] betChips = new GameObject[0];
    [SerializeField] private Transform pivotPayout;
    [SerializeField] private GameObject[] payoutChips = new GameObject[0];
    [Tooltip("チップが出入りする所（親のチップ入れ）")]
    [SerializeField] private Transform anchorTray;
    [Tooltip("賭け金の山を置く所／払い戻しの山を置く所")]
    [SerializeField] private Transform anchorBet;
    [SerializeField] private Transform anchorPayout;
    [SerializeField, Min(0.05f)] private float chipSeconds = 0.45f;

    private readonly Queue<IEnumerator> queue = new();
    private Coroutine worker;
    private string shown = "";        // 届いている並び（これから動かす分も含む）
    private string landed = "";       // 台の上で表になった並び
    private MaterialPropertyBlock block;
    private int betTier;

    /// <summary>まだ動かしている途中か（確認の道具用）。</summary>
    public bool IsBusy => worker != null;

    /// <summary>台の上で表になっているカードの並び（確認の道具用）。</summary>
    public string Landed => landed;

    /// <summary>いま台の上に出ているカードの枚数（確認の道具用）。</summary>
    public int VisibleCards
    {
        get
        {
            int n = 0;
            foreach (CardSlot s in cards) if (s != null && s.pivot != null && s.pivot.gameObject.activeSelf) n++;
            return n;
        }
    }

    /// <summary>賭け金の山・払い戻しの山が出ているか（確認の道具用）。</summary>
    public bool BetShown => pivotBet != null && pivotBet.gameObject.activeSelf;
    public bool PayoutShown => pivotPayout != null && pivotPayout.gameObject.activeSelf;

    private static bool TryDecode(char code, out int rank, out int suit)
    {
        int i = code - FirstCard;
        rank = i / 4 + 1; suit = i % 4;
        return i >= 0 && i < 52;
    }

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        SnapEmpty();
    }

    private void OnDisable()
    {
        // 見えない間は動かさない。戻ったときに、いまの並びへそろえ直す（HiLoTable が Snap を呼ぶ）
        queue.Clear();
        worker = null;
    }

    // ================================================================ 外から呼ぶ

    /// <summary>動きなしで、その並びにそろえる（あとから入った人・表示が戻ったとき）。</summary>
    public void Snap(string codes, int betIndex)
    {
        queue.Clear();
        if (worker != null) { StopCoroutine(worker); worker = null; }
        SnapEmpty();
        shown = codes ?? "";
        landed = shown;
        betTier = betIndex;
        for (int i = 0; i < shown.Length; i++) PlaceCard(Slot(i), i, shown[i]);
        if (shown.Length > 0) ShowStack(pivotBet, betChips, betIndex + 1, anchorBet);
    }

    /// <summary>はじまった：賭け金のチップを置き、1 枚目を表向きに配る。</summary>
    public void Begin(int betIndex, char first)
    {
        betTier = betIndex;
        shown = first.ToString();
        if (!isActiveAndEnabled) return;
        Enqueue(ChipRoutine(pivotBet, betChips, betIndex + 1, PlayerSide(anchorBet), LocalOf(anchorBet), true));
        Enqueue(DealRoutine(Slot(0), 0, first, true, null));
    }

    /// <summary>
    /// 次のカード：山から裏のまま滑ってきて、ひと呼吸おいてめくれる。
    /// onShown は、表が見えた瞬間に呼ぶ（手持ちの額の表示を、その瞬間に合わせるため）。
    /// 外れなら、めくったあとで賭け金のチップが下がる。
    /// </summary>
    public void Reveal(char code, bool won, Action onShown)
    {
        int index = shown.Length;
        shown += code;
        if (!isActiveAndEnabled) { onShown?.Invoke(); return; }
        Enqueue(DealRoutine(Slot(index), index, code, false, () =>
        {
            GameAudio.PlayAt(won ? "casino/win" : "casino/lose", transform.position);
            onShown?.Invoke();
        }));
        if (!won) Enqueue(ChipRoutine(pivotBet, betChips, -1, pivotBet != null ? pivotBet.localPosition : Vector3.zero, LocalOf(anchorTray), false));
    }

    /// <summary>受け取った：払い戻しのチップが、チップ入れから滑ってくる（steps：当てた回数。多いほど山が高い）。</summary>
    public void Collect(int steps)
    {
        if (!isActiveAndEnabled) return;
        GameAudio.PlayAt("casino/cash", transform.position);
        Enqueue(ChipRoutine(pivotPayout, payoutChips, Mathf.Min(payoutChips.Length, betTier + 1 + Mathf.Max(0, steps)), LocalOf(anchorTray), LocalOf(anchorPayout), true));
    }

    /// <summary>終わった：少し見せてから、台の上を片付ける。</summary>
    public void Clear()
    {
        bool anything = shown.Length > 0;
        shown = "";
        if (!isActiveAndEnabled) return;
        if (anything || VisibleCards > 0) Enqueue(ClearRoutine());
    }

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

    /// <summary>プレハブに置いてある枚数を超えたら、最後の 1 枚を使い回す（上に重ねて置く）。</summary>
    private CardSlot Slot(int index) => cards.Length == 0 ? null : cards[Mathf.Min(index, cards.Length - 1)];

    private IEnumerator DealRoutine(CardSlot slot, int index, char code, bool faceUpOnTheWay, Action onShown)
    {
        if (slot == null || slot.pivot == null || anchorRow == null) { landed += code; onShown?.Invoke(); yield break; }
        SetFace(slot, code);
        Transform parent = slot.pivot.parent;
        Vector3 from = anchorDeck != null ? parent.InverseTransformPoint(anchorDeck.position) : slot.pivot.localPosition;
        Vector3 to = RowPosition(index, parent);
        Quaternion faceUp = anchorRow.localRotation, faceDown = faceUp * Quaternion.Euler(0f, 0f, 180f);

        slot.pivot.localPosition = from;
        slot.pivot.localRotation = faceDown;
        slot.pivot.gameObject.SetActive(true);
        GameAudio.PlayAt("casino/card", transform.position, 0.9f, 0.08f, 12f);

        bool shownNow = false;
        void Shown() { if (shownNow) return; shownNow = true; landed += code; onShown?.Invoke(); }

        // 山から滑ってくる。1 枚目は、滑ってくる途中の後ろ半分でめくる。2 枚目からは、裏のまま置く
        float t = 0f;
        while (t < dealSeconds)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dealSeconds), eased = 1f - (1f - u) * (1f - u);
            Vector3 p = Vector3.Lerp(from, to, eased);
            p.y += Mathf.Sin(u * Mathf.PI) * dealArc;
            slot.pivot.localPosition = p;
            if (faceUpOnTheWay)
            {
                float turn = Mathf.InverseLerp(0.45f, 1f, u);
                slot.pivot.localRotation = Quaternion.Slerp(faceDown, faceUp, turn);
                if (turn >= 0.5f) Shown();
            }
            yield return null;
        }
        slot.pivot.localPosition = to;
        if (faceUpOnTheWay) { slot.pivot.localRotation = faceUp; Shown(); yield break; }

        // ひと呼吸おいて、その場でめくる
        if (holdSeconds > 0f) yield return new WaitForSeconds(holdSeconds);
        GameAudio.PlayAt("casino/card", transform.position, 1f, 0.05f, 12f);
        t = 0f;
        while (t < flipSeconds)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / flipSeconds);
            slot.pivot.localRotation = Quaternion.Slerp(faceDown, faceUp, u);
            slot.pivot.localPosition = to + Vector3.up * (Mathf.Sin(u * Mathf.PI) * flipLift);
            if (u >= 0.5f) Shown();      // 半分まで回ると、表が見えはじめる
            yield return null;
        }
        slot.pivot.localRotation = faceUp;
        slot.pivot.localPosition = to;
        Shown();
    }

    private IEnumerator ClearRoutine()
    {
        if (clearDelaySeconds > 0f) yield return new WaitForSeconds(clearDelaySeconds);
        var moving = new List<Transform>();
        foreach (CardSlot s in cards) if (s != null && s.pivot != null && s.pivot.gameObject.activeSelf) moving.Add(s.pivot);
        if (moving.Count > 0) GameAudio.PlayAt("casino/card", transform.position, 0.8f, 0.05f, 12f);

        var from = new Vector3[moving.Count];
        for (int i = 0; i < moving.Count; i++) from[i] = moving[i].localPosition;
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
    private IEnumerator ChipRoutine(Transform pivot, GameObject[] chips, int count, Vector3 from, Vector3 to, bool stay)
    {
        if (pivot == null) yield break;
        if (count >= 0)
        {
            for (int i = 0; i < chips.Length; i++) if (chips[i] != null) chips[i].SetActive(i < count);
            pivot.gameObject.SetActive(count > 0);
            if (count == 0) yield break;
        }
        else if (!pivot.gameObject.activeSelf) yield break;

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

    // ================================================================ 置く

    private Vector3 LocalOf(Transform anchor) => anchor != null ? anchor.localPosition : Vector3.zero;

    /// <summary>賭け金の山が出てくる所：置く所より、遊ぶ人の側へ少し手前。</summary>
    private Vector3 PlayerSide(Transform anchor) => LocalOf(anchor) + new Vector3(0f, 0f, 0.16f);

    private Vector3 RowPosition(int index, Transform parent)
    {
        int column = Mathf.Min(index, Mathf.Max(0, cards.Length - 1));
        // 枚数を超えて重ねる分は、少しだけ上に置く
        Vector3 local = cardStep * column + Vector3.up * (cardStep.y * (index - column));
        return parent.InverseTransformPoint(anchorRow.TransformPoint(local));
    }

    private void PlaceCard(CardSlot slot, int index, char code)
    {
        if (slot == null || slot.pivot == null || anchorRow == null) return;
        SetFace(slot, code);
        slot.pivot.localPosition = RowPosition(index, slot.pivot.parent);
        slot.pivot.localRotation = anchorRow.localRotation;
        slot.pivot.gameObject.SetActive(true);
    }

    private void ShowStack(Transform pivot, GameObject[] chips, int count, Transform anchor)
    {
        if (pivot == null) return;
        for (int i = 0; i < chips.Length; i++) if (chips[i] != null) chips[i].SetActive(i < count);
        pivot.localPosition = LocalOf(anchor);
        pivot.gameObject.SetActive(count > 0);
    }

    private void SnapEmpty()
    {
        foreach (CardSlot s in cards) if (s != null && s.pivot != null) s.pivot.gameObject.SetActive(false);
        if (pivotBet != null) pivotBet.gameObject.SetActive(false);
        if (pivotPayout != null) pivotPayout.gameObject.SetActive(false);
        landed = "";
    }

    /// <summary>表の面の絵を、その札に合わせる（マテリアルは共通のまま、絵の中の場所だけを替える）。</summary>
    private void SetFace(CardSlot slot, char code)
    {
        if (slot.face == null) return;
        int index = BlackjackTableView.BackIndex;
        if (TryDecode(code, out int rank, out int suit)) index = suit * 13 + rank - 1;
        block ??= new MaterialPropertyBlock();
        slot.face.GetPropertyBlock(block);
        block.SetVector("_BaseMap_ST", BlackjackTableView.CellST(index, atlasColumns, cellU, cellV));
        slot.face.SetPropertyBlock(block);
    }
}
