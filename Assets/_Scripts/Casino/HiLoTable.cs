using System.Collections;
using TMPro;
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
/// プレハブ：Assets/_Prefabs/Casino/HiLoTable.prefab（ルート：NetworkObject・このスクリプト・台の当たり判定／Visual：台／Anchor_Card・Anchor_Board：文字）
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

    [Header("文字（置いてある物に書き込むだけ）")]
    [SerializeField] private TextMeshPro cardText;
    [SerializeField] private TextMeshPro potText;
    [SerializeField] private TextMeshPro hintText;

    private readonly NetworkVariable<int> phase = new((int)Phase.Idle);
    private readonly NetworkVariable<ulong> ownerClientId = new(ulong.MaxValue);
    private readonly NetworkVariable<int> card = new(0);        // いまのカードの数字（0 = なし）
    private readonly NetworkVariable<int> suit = new(0);
    private readonly NetworkVariable<int> pot = new(0);         // 手持ち（受け取れる額）
    private readonly NetworkVariable<int> step = new(0);        // 当てた回数

    private ulong ownerPlayerObjectId = ulong.MaxValue;   // サーバーだけ
    private int activeBet;                                 // サーバーだけ
    private double deadline;                               // サーバーだけ
    private int currentBetIndex;

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
        RefreshTexts();
    }

    public override void OnNetworkDespawn()
    {
        phase.OnValueChanged -= OnChanged; card.OnValueChanged -= OnChanged; pot.OnValueChanged -= OnChanged;
        step.OnValueChanged -= OnChanged;
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
        suit.Value = Random.Range(0, 4);
        card.Value = next;
        if (won)
        {
            pot.Value = Mathf.FloorToInt(pot.Value * multiplier);
            step.Value = step.Value + 1;
        }
        else pot.Value = 0;

        ResultRpc(won ? 1 : 2);
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
        ResultRpc(3);
        ServerReset();
    }

    private void ServerReset()
    {
        ownerClientId.Value = ulong.MaxValue;
        ownerPlayerObjectId = ulong.MaxValue;
        activeBet = 0;
        pot.Value = 0;
        step.Value = 0;
        phase.Value = (int)Phase.Idle;
    }

    private void Update()
    {
        // 自分の番で放っておかれたら、その時点の手持ちを受け取って終わる
        if (IsSpawned && IsServer && phase.Value == (int)Phase.Guess && ServerNow > deadline) ServerCollect();
    }

    // ================================================================ 見た目と音（各クライアント）

    private void OnChanged(int oldValue, int newValue) => RefreshTexts();

    /// <summary>結果の音（1 = 当たり、2 = 外れ、3 = 受け取った）。</summary>
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void ResultRpc(int kind)
    {
        if (kind == 1) GameAudio.PlayAt("blackjack/win", transform.position);
        else if (kind == 2) GameAudio.PlayAt("blackjack/lose", transform.position);
        else GameAudio.PlayAt("money/gain", transform.position);
        RefreshTexts();
    }

    private void RefreshTexts()
    {
        bool playing = phase.Value != (int)Phase.Idle;
        if (cardText != null)
        {
            cardText.text = card.Value > 0 ? HiLoRules.RankName(card.Value) + HiLoRules.SuitName(suit.Value) : "？";
            cardText.color = card.Value > 0 && HiLoRules.IsRedSuit(suit.Value) ? new Color(.78f, .16f, .22f) : new Color(.1f, .1f, .14f);
        }
        if (potText != null)
            potText.text = playing ? $"手持ち {pot.Value:N0}R（{step.Value}/{maxSteps}）" : $"賭け金 {CurrentBet:N0}R";
        if (hintText != null)
            hintText.text = playing ? "上か下か　同じ数字は外れ" : "HIGH & LOW";
    }
}
