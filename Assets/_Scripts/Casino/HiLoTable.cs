using System.Collections;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>ハイ＆ローの決まり（通信もシーンも使わない計算だけ。テストから直接呼べる）。</summary>
public static class HiLoRules
{
    public const int Ranks = 13;   // A(1)〜K(13)。A がいちばん低い

    /// <summary>いまのカードに対して、その向き（上か下か）で勝ちになる数字の数。同じ数字は負け。</summary>
    public static int WinningRanks(int card, bool high) => high ? Ranks - card : card - 1;

    /// <summary>
    /// 当たったときに賭け金（手持ちの額）へ掛ける倍率。当たりにくい向きほど大きい。
    /// どの向きを選んでも、1 回ごとの戻る割合は returnRate になる（当たる確率 × 倍率 = returnRate）。
    /// </summary>
    public static float Multiplier(int card, bool high, float returnRate)
    {
        int k = WinningRanks(card, high);
        return k <= 0 ? 0f : returnRate * Ranks / k;
    }

    public static string RankName(int rank)
    {
        switch (rank)
        {
            case 1: return "A";
            case 11: return "J";
            case 12: return "Q";
            case 13: return "K";
            default: return rank.ToString();
        }
    }

    public static string SuitName(int suit) => suit == 0 ? "♠" : suit == 1 ? "♥" : suit == 2 ? "♦" : "♣";
    public static bool IsRedSuit(int suit) => suit == 1 || suit == 2;
}

/// <summary>
/// ハイ＆ロー（2 階）。次のカードが、いまのカードより上か下かを当てる。1 台につき 1 人ずつ。
///
/// 操作：E ではじめる（賭け金を払う）→ E = HIGH（上）、R = LOW（下）。当たるたびに手持ちが増え、F（持つキー）で受け取る。
/// 最大 maxSteps 回まで続けられる（最後は自動で受け取る）。外れたら手持ちは 0。同じ数字は外れ。
/// しばらく何もしないと、その時点の手持ちを自動で受け取る（台がふさがったままにならないように）。
///
/// プレハブ：Assets/_Prefabs/Casino/HiLoTable.prefab（ルート：NetworkObject・このスクリプト・HiLoTableView・台の当たり判定／Visual：台／
/// Pivot_Card_〇・Pivot_Chips_〇：台の上のカードとチップ／Anchor_Odds・Anchor_Board：文字）
/// カードは台の上の実物（HiLoTableView が動かす）。カードの並びは、サーバーが決めて全員に同じ物を配る。
/// 立て札（Anchor_Odds）は、上と下の倍率を出すだけの補助。
/// お金と勝ち負けはサーバーだけが決める。
/// </summary>
public class HiLoTable : CasinoGame
{
    private enum Phase { Idle, Guess, Reveal }

    [Header("賭け金")]
    [SerializeField] private int[] betAmounts = { 100, 500, 1000 };

    [Header("決まり")]
    [Tooltip("1 回ごとの戻る割合（当たる確率 × 倍率）。0.995 なら、1 回で受け取ると 99.5%、5 回続けると約 97.5%")]
    [SerializeField, Range(0.8f, 1.05f)] private float stepReturnRate = 0.995f;
    [Tooltip("続けられる回数の上限（ここまで当て続けたら自動で受け取る）")]
    [SerializeField, Min(1)] private int maxSteps = 5;

    [Header("時間（秒）")]
    [SerializeField, Min(0.2f)] private float revealSeconds = 1.3f;
    [Tooltip("自分の番でこの秒数なにもしないと、その時点の手持ちを受け取って終わる")]
    [SerializeField, Min(5f)] private float idleTimeoutSeconds = 25f;

    [Header("台の上の実物（カードとチップ）")]
    [SerializeField] private HiLoTableView view;

    [Header("文字（置いてある物に書き込むだけ）")]
    [Tooltip("立て札：上と下の倍率")]
    [SerializeField] private TextMeshPro oddsText;
    [SerializeField] private TextMeshPro potText;
    [SerializeField] private TextMeshPro hintText;
    [SerializeField] private Color upColor = new Color(1f, 0.56f, 0.8f);
    [SerializeField] private Color downColor = new Color(0.45f, 0.86f, 0.95f);

    private readonly NetworkVariable<int> phase = new((int)Phase.Idle);
    private readonly NetworkVariable<ulong> ownerClientId = new(ulong.MaxValue);
    private readonly NetworkVariable<int> card = new(0);        // いまのカードの数字（0 = なし）
    private readonly NetworkVariable<int> suit = new(0);
    private readonly NetworkVariable<int> pot = new(0);         // 手持ち（受け取れる額）
    private readonly NetworkVariable<int> step = new(0);        // 当てた回数
    // 台の上のカードの並び（1 枚目から順に。符号は BlackjackTableView.Encode）。全員に同じ物が届く。あとから入った人は、これで台の上をそろえる
    private readonly NetworkVariable<FixedString32Bytes> cardsNet = new(new FixedString32Bytes());
    private readonly NetworkVariable<int> betIndexNet = new(0);

    private ulong ownerPlayerObjectId = ulong.MaxValue;   // サーバーだけ
    private int activeBet;                                 // サーバーだけ
    private double deadline;                               // サーバーだけ
    private int currentBetIndex;

    // 台の上でカードがめくれるまで、文字（手持ちの額・倍率）は前の値のままにしておく（めくる前に結果が読めてしまわないように）
    private bool holdTexts;
    private int shownCard, shownPot, shownStep;

    public HiLoTableView View => view;
    public string CardsOnTable => cardsNet.Value.ToString();

    public int[] BetAmounts => betAmounts;
    public float StepReturnRate => stepReturnRate;
    public int MaxSteps => maxSteps;
    public bool IsIdle => phase.Value == (int)Phase.Idle;
    public bool IsGuessing(ulong clientId) => phase.Value == (int)Phase.Guess && ownerClientId.Value == clientId;
    public int Card => card.Value;
    public int Pot => pot.Value;
    public int Step => step.Value;
    public int CurrentBet => betAmounts[Mathf.Clamp(currentBetIndex, 0, betAmounts.Length - 1)];

    public override void OnNetworkSpawn()
    {
        phase.OnValueChanged += OnChanged; card.OnValueChanged += OnChanged; pot.OnValueChanged += OnChanged;
        step.OnValueChanged += OnChanged;
        phase.OnValueChanged += OnPhaseChanged;
        RefreshTexts();
        SnapView();
    }

    private void OnEnable()
    {
        // 階の出し入れで表示が戻ったとき：台の上を、いまの並びにそろえ直す
        if (IsSpawned) { holdTexts = false; RefreshTexts(); SnapView(); }
    }

    private void SnapView()
    {
        if (view != null) view.Snap(cardsNet.Value.ToString(), betIndexNet.Value);
    }

    public override void OnNetworkDespawn()
    {
        phase.OnValueChanged -= OnChanged; card.OnValueChanged -= OnChanged; pot.OnValueChanged -= OnChanged;
        step.OnValueChanged -= OnChanged;
        phase.OnValueChanged -= OnPhaseChanged;
    }

    // ================================================================ 操作（各クライアント）

    public override string GetLabel(Collider lookedAt, ulong clientId)
    {
        switch ((Phase)phase.Value)
        {
            case Phase.Idle:
                return $"{Key(GameAction.Interact)} ハイ＆ローをはじめる {CurrentBet:N0}R   {WheelTag} 額";
            case Phase.Guess when ownerClientId.Value == clientId:
                float up = HiLoRules.Multiplier(card.Value, true, stepReturnRate), down = HiLoRules.Multiplier(card.Value, false, stepReturnRate);
                string label = (up > 0 ? $"{Key(GameAction.Interact)} 上 ×{up:F2}   " : "") + (down > 0 ? $"{Key(GameAction.Rotate)} 下 ×{down:F2}" : "");
                if (step.Value > 0) label += $"   {Key(GameAction.Carry)} 受け取る {pot.Value:N0}R";
                return label;
            case Phase.Reveal when ownerClientId.Value == clientId:
                return "…";
            default:
                return "ほかの人が遊んでいる";
        }
    }

    public override void ChangeBet(int direction)
    {
        if (!IsIdle || betAmounts == null || betAmounts.Length == 0) return;
        currentBetIndex = (currentBetIndex + direction) % betAmounts.Length;
        if (currentBetIndex < 0) currentBetIndex += betAmounts.Length;
        RefreshTexts();
    }

    public override void Interact(Collider lookedAt, PlayerEarning earning)
    {
        if (!IsSpawned || earning == null || !earning.IsSpawned) return;
        if (IsIdle) StartServerRpc(earning.NetworkObjectId, currentBetIndex);
        else if (IsGuessing(earning.OwnerClientId)) GuessServerRpc(true);
    }

    public override void Secondary(Collider lookedAt, PlayerEarning earning)
    {
        if (IsSpawned && earning != null && IsGuessing(earning.OwnerClientId)) GuessServerRpc(false);
    }

    public override void Tertiary(Collider lookedAt, PlayerEarning earning)
    {
        if (IsSpawned && earning != null && IsGuessing(earning.OwnerClientId) && step.Value > 0) CollectServerRpc();
    }

    // ================================================================ サーバー

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void StartServerRpc(ulong playerObjectId, int betIndex, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (phase.Value != (int)Phase.Idle || betAmounts == null || betAmounts.Length == 0) return;
        if (!ServerValidatePlayer(playerObjectId, sender, out PlayerEarning earning)) return;

        int bet = Mathf.Max(1, betAmounts[Mathf.Clamp(betIndex, 0, betAmounts.Length - 1)]);
        SharedMoneyManager money = SharedMoneyManager.Instance;
        if (money == null || !money.TrySpend(bet, SharedMoneyReason.CasinoBet, $"hilo client={sender}"))
        {
            Tell(sender, "ハイ＆ロー", "共同口座のお金が足りない。");
            return;
        }
        earning.SpendEarning(bet);

        ownerClientId.Value = sender;
        ownerPlayerObjectId = playerObjectId;
        activeBet = bet;
        pot.Value = bet;
        step.Value = 0;
        // 最初のカードは、上にも下にも賭けられる数字（2〜Q）にする
        card.Value = Random.Range(2, HiLoRules.Ranks);
        suit.Value = Random.Range(0, 4);
        int index = Mathf.Clamp(betIndex, 0, betAmounts.Length - 1);
        char first = BlackjackTableView.Encode(card.Value, suit.Value);
        betIndexNet.Value = index;
        cardsNet.Value = new FixedString32Bytes(first.ToString());
        StartedRpc((byte)first, index);
        deadline = ServerNow + idleTimeoutSeconds;
        phase.Value = (int)Phase.Guess;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void GuessServerRpc(bool high, RpcParams rpcParams = default)
    {
        if (!IsGuessing(rpcParams.Receive.SenderClientId)) return;
        float multiplier = HiLoRules.Multiplier(card.Value, high, stepReturnRate);
        if (multiplier <= 0f) return;   // その向きには出ない（A の下・K の上）

        int next = Random.Range(1, HiLoRules.Ranks + 1);
        bool won = high ? next > card.Value : next < card.Value;
        int nextSuit = Random.Range(0, 4);
        // 先に「次のカード」を全員へ知らせる（台の上でめくれるまで、文字は前の値のままにするため。値を変えるのは、そのあと）
        char code = BlackjackTableView.Encode(next, nextSuit);
        RevealRpc((byte)code, won);
        AppendCard(code);
        suit.Value = nextSuit;
        card.Value = next;
        if (won)
        {
            pot.Value = Mathf.FloorToInt(pot.Value * multiplier);
            step.Value = step.Value + 1;
        }
        else pot.Value = 0;

        phase.Value = (int)Phase.Reveal;
        StartCoroutine(ServerAfterReveal(won));
    }

    private IEnumerator ServerAfterReveal(bool won)
    {
        yield return new WaitForSeconds(revealSeconds);
        if (!won)
        {
            Tell(ownerClientId.Value, "ハイ＆ロー", $"外れ…… ¥{activeBet:N0} の負け。");
            ServerReset();
        }
        else if (step.Value >= maxSteps) ServerCollect();
        else
        {
            deadline = ServerNow + idleTimeoutSeconds;
            phase.Value = (int)Phase.Guess;
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void CollectServerRpc(RpcParams rpcParams = default)
    {
        if (!IsGuessing(rpcParams.Receive.SenderClientId) || step.Value <= 0) return;
        ServerCollect();
    }

    /// <summary>手持ちを共同口座へ入れて終わる。</summary>
    private void ServerCollect()
    {
        int amount = pot.Value;
        ulong client = ownerClientId.Value;
        SharedMoneyManager money = SharedMoneyManager.Instance;
        if (amount > 0 && money != null && money.TryAdd(amount, SharedMoneyReason.CasinoPayout, $"hilo client={client}", activeBet))
        {
            PlayerEarning earning = ServerFindEarning(ownerPlayerObjectId);
            if (earning != null) earning.AddEarning(amount);
        }
        int net = amount - activeBet;
        if (net > 0) Tell(client, "ハイ＆ロー", $"{step.Value} 回当てて ¥{net:N0} の勝ち！", NightEventManager.StyleFun);
        else Tell(client, "ハイ＆ロー", "賭け金が戻った。");
        CollectedRpc(step.Value);
        ServerReset();
    }

    private void ServerReset()
    {
        ownerClientId.Value = ulong.MaxValue;
        ownerPlayerObjectId = ulong.MaxValue;
        activeBet = 0;
        pot.Value = 0;
        step.Value = 0;
        cardsNet.Value = new FixedString32Bytes();
        phase.Value = (int)Phase.Idle;
    }

    /// <summary>台の上の並びに 1 枚足す（入りきらなくなったら、足さない。台の上の動きは RevealRpc で届く）。</summary>
    private void AppendCard(char code)
    {
        FixedString32Bytes value = cardsNet.Value;
        if (value.Length + 1 > value.Capacity) return;
        value.Append(code);
        cardsNet.Value = value;
    }

    private void Update()
    {
        // 自分の番で放っておかれたら、その時点の手持ちを受け取って終わる
        if (IsSpawned && IsServer && phase.Value == (int)Phase.Guess && ServerNow > deadline) ServerCollect();
    }

    // ================================================================ 見た目と音（各クライアント）

    private void OnChanged(int oldValue, int newValue) => RefreshTexts();

    private void OnPhaseChanged(int oldValue, int newValue)
    {
        // 終わった（外れ・受け取り・時間切れ）：少し見せてから、台の上を片付ける
        if (newValue == (int)Phase.Idle && oldValue != (int)Phase.Idle)
        {
            if (view != null) view.Clear();
        }
        // 次の番になったら、文字は必ずいまの値にする（めくる動きが届かなかった場合の備え）
        if (newValue == (int)Phase.Guess) { holdTexts = false; RefreshTexts(); }
    }

    /// <summary>はじまった：賭け金のチップを置き、1 枚目を配る。</summary>
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void StartedRpc(byte first, int betIndex)
    {
        holdTexts = false;
        if (view != null) view.Begin(betIndex, (char)first);
        else GameAudio.PlayAt("casino/card", transform.position);
        RefreshTexts();
    }

    /// <summary>次のカードが出た：台の上で、山から滑ってきてめくれる。めくれた瞬間に、文字と音を合わせる。</summary>
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void RevealRpc(byte code, bool won)
    {
        if (view == null)
        {
            GameAudio.PlayAt("casino/card", transform.position);
            GameAudio.PlayAt(won ? "casino/win" : "casino/lose", transform.position);
            return;
        }
        holdTexts = true;
        view.Reveal((char)code, won, () => { holdTexts = false; RefreshTexts(); });
    }

    /// <summary>受け取った：払い戻しのチップが滑ってくる。</summary>
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void CollectedRpc(int steps)
    {
        if (view != null) view.Collect(steps);
        else GameAudio.PlayAt("casino/cash", transform.position);
    }

    private void RefreshTexts()
    {
        bool playing = phase.Value != (int)Phase.Idle;
        if (!holdTexts) { shownCard = card.Value; shownPot = pot.Value; shownStep = step.Value; }
        if (oddsText != null)
        {
            if (playing && shownCard > 0)
            {
                float up = HiLoRules.Multiplier(shownCard, true, stepReturnRate), down = HiLoRules.Multiplier(shownCard, false, stepReturnRate);
                string upLine = up > 0 ? $"<color=#{ColorUtility.ToHtmlStringRGB(upColor)}>上 ×{up:F2}</color>" : "<color=#777777>上 ―</color>";
                string downLine = down > 0 ? $"<color=#{ColorUtility.ToHtmlStringRGB(downColor)}>下 ×{down:F2}</color>" : "<color=#777777>下 ―</color>";
                oddsText.text = upLine + "\n" + downLine;
            }
            else oddsText.text = "上か\n下か";
        }
        if (potText != null)
            potText.text = playing ? $"手持ち {shownPot:N0}R（{shownStep}/{maxSteps}）" : $"賭け金 {CurrentBet:N0}R";
        if (hintText != null)
            hintText.text = playing ? "上か下か　同じ数字は外れ" : "HIGH & LOW";
    }
}
