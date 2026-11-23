using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// シーンへPrefabを配置するだけで動作する、Server権限のブラックジャック台。
/// カード・チップ・点数の札は台の上の実物（BlackjackTableView が動かす）。上のモニター（Anchor_Display）は賭け金と結果だけの補助。
/// 親の番は、カードをめくって配る動きのぶんだけサーバーが待ってから、結果とお金を確定する。
/// </summary>
public class BlackjackTable : NetworkBehaviour
{
    private enum RoundPhase
    {
        Idle,
        PlayerTurn,
        Result,
        DealerTurn      // 親がめくって引いている間（だれも操作できない）。順番を変えないよう、最後に足した
    }

    private enum ResultKind
    {
        None,
        Win,
        Lose,
        Push,
        Blackjack,
        Error
    }

    [Header("賭け金")]
    [SerializeField] private int[] betAmounts = { 10, 50, 100 };

    [Header("配当")]
    [SerializeField, Min(1f)] private float normalWinReturnMultiplier = 2f;
    [SerializeField, Min(1f)] private float blackjackReturnMultiplier = 2.5f;

    [Header("ダブルダウン（上の階の台）")]
    [Tooltip("最初の 2 枚のときだけ、賭け金を倍にして 1 枚だけ引ける。1 階の台は切っておく")]
    [SerializeField] private bool allowDoubleDown;

    [Header("Server検証")]
    [SerializeField, Min(0.1f)] private float serverInteractDistance = 4f;

    [Header("表示")]
    [SerializeField] private Vector3 displayLocalPosition = new Vector3(0f, 1.35f, 0.68f);
    [SerializeField] private Vector2 displaySize = new Vector2(700f, 430f);
    [SerializeField, Min(0.0001f)] private float displayScale = 0.0025f;
    [SerializeField, Min(0.1f)] private float resultDisplaySeconds = 3.5f;

    [Header("台の上のカードとチップ（見た目）")]
    [SerializeField] private BlackjackTableView view;
    [Tooltip("カード 1 枚を配る・めくる動きの長さ（秒）。サーバーが、動きが終わるまで結果を待つのに使う。BlackjackTableView の値と合わせる")]
    [SerializeField, Min(0.05f)] private float cardSeconds = 0.34f;
    [Tooltip("親の札がそろってから、結果を出すまでの間（秒）")]
    [SerializeField, Min(0f)] private float settleSeconds = 0.35f;

    private readonly NetworkVariable<int> phase = new(
        (int)RoundPhase.Idle,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<ulong> roundPlayerObjectId = new(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<ulong> roundOwnerClientId = new(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> activeBet = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // 台の上のカードの並び（1 枚 1 文字。BlackjackTableView.Encode。親の伏せ札は '#'）。全員に同じ物が届く
    private readonly NetworkVariable<FixedString32Bytes> playerCardsNet = new(
        new FixedString32Bytes(),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<FixedString32Bytes> dealerCardsNet = new(
        new FixedString32Bytes(),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<FixedString128Bytes> resultDisplay = new(
        new FixedString128Bytes("Eでゲーム開始"),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> resultNetAmount = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> resultKind = new(
        (int)ResultKind.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // いまダブルダウンできるか（最初の 2 枚で、まだ何もしていないとき。サーバーが決める）
    private readonly NetworkVariable<bool> canDouble = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly List<Card> playerCards = new();
    private readonly List<Card> dealerCards = new();

    private int currentBetIndex;

    [Header("画面（プレハブの Anchor_Display の中。配置ツール Roomies/Casino/2 Build Gimmick Prefabs が置いて割り当てる）")]
    [SerializeField] private TextMeshProUGUI displayText;
    [SerializeField] private Image displayPanel;
    [SerializeField] private Image resultBanner;
    [SerializeField] private TextMeshProUGUI resultBannerText;
    [SerializeField] private RectTransform resultBannerRect;
    [SerializeField] private CanvasGroup resultOverlayGroup;
    [SerializeField] private Image resultOverlayBackground;
    [SerializeField] private RectTransform resultOverlayRect;
    [SerializeField] private TextMeshProUGUI resultHeadlineText;
    [SerializeField] private TextMeshProUGUI resultMessageText;
    [SerializeField] private TextMeshProUGUI resultAmountText;
    [SerializeField] private TextMeshProUGUI resultOutcomeText;

    private Coroutine resetCoroutine;
    private Coroutine resultAnimationCoroutine;
    private Coroutine finishCoroutine;

    private struct Card
    {
        public int rank;
        public int suit;
    }

    public string CurrentBetLabel
    {
        get
        {
            if (betAmounts == null || betAmounts.Length == 0) return "0 R";
            return betAmounts[Mathf.Clamp(currentBetIndex, 0, betAmounts.Length - 1)] + " R";
        }
    }

    public bool IsIdle => phase.Value == (int)RoundPhase.Idle;

    private void Awake()
    {
        // 画面はプレハブに置いてある（実行時には作らない）。割り当てが無いときは、配置ツールを実行し直す
        if (displayText == null)
            Debug.LogWarning("[Blackjack] 画面（Anchor_Display）が割り当てられていません。Roomies/Casino/2 Build Gimmick Prefabs を実行してください。", this);
        RefreshDisplay();
    }

    public override void OnNetworkSpawn()
    {
        phase.OnValueChanged += OnStateChanged;
        roundOwnerClientId.OnValueChanged += OnStateChanged;
        activeBet.OnValueChanged += OnStateChanged;
        activeBet.OnValueChanged += OnBetChanged;
        playerCardsNet.OnValueChanged += OnCardsChanged;
        dealerCardsNet.OnValueChanged += OnCardsChanged;
        resultDisplay.OnValueChanged += OnTextChanged;
        resultNetAmount.OnValueChanged += OnStateChanged;
        resultKind.OnValueChanged += OnResultKindChanged;
        RefreshDisplay();
        SnapView();
    }

    private void OnEnable()
    {
        // 階の出し入れで表示が戻ったとき：台の上を、いまの並びにそろえ直す
        if (IsSpawned) SnapView();
    }

    private void SnapView()
    {
        if (view == null) return;
        int bet = activeBet.Value;
        bool doubled = false;
        int index = BetIndexOf(bet);
        if (index < 0 && bet % 2 == 0) { index = BetIndexOf(bet / 2); doubled = index >= 0; }
        view.Snap(playerCardsNet.Value.ToString(), dealerCardsNet.Value.ToString(), Mathf.Max(0, index), doubled);
    }

    private int BetIndexOf(int amount)
    {
        if (betAmounts == null) return -1;
        for (int i = 0; i < betAmounts.Length; i++) if (betAmounts[i] == amount) return i;
        return -1;
    }

    public override void OnNetworkDespawn()
    {
        phase.OnValueChanged -= OnStateChanged;
        roundOwnerClientId.OnValueChanged -= OnStateChanged;
        activeBet.OnValueChanged -= OnStateChanged;
        activeBet.OnValueChanged -= OnBetChanged;
        playerCardsNet.OnValueChanged -= OnCardsChanged;
        dealerCardsNet.OnValueChanged -= OnCardsChanged;
        resultDisplay.OnValueChanged -= OnTextChanged;
        resultNetAmount.OnValueChanged -= OnStateChanged;
        resultKind.OnValueChanged -= OnResultKindChanged;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (resetCoroutine != null) StopCoroutine(resetCoroutine);
        if (resultAnimationCoroutine != null) StopCoroutine(resultAnimationCoroutine);
        if (finishCoroutine != null) StopCoroutine(finishCoroutine);
    }

    public string GetInteractionLabel(ulong clientId)
    {
        RoundPhase currentPhase = (RoundPhase)phase.Value;

        if (currentPhase == RoundPhase.Idle)
            return "DEAL   <color=#FFA31F>WHEEL</color> BET " + CurrentBetLabel;

        if (currentPhase == RoundPhase.PlayerTurn && roundOwnerClientId.Value == clientId)
            return "HIT   <color=#FFA31F>R</color> STAND" +
                   (canDouble.Value
                       ? "   <color=#FFA31F>" + GameSettings.GetKeyLabel(GameAction.Carry) + "</color> DOUBLE"
                       : "");

        return "ゲーム中…";
    }

    public bool CanLocalPlayerStand(ulong clientId)
    {
        return phase.Value == (int)RoundPhase.PlayerTurn &&
               roundOwnerClientId.Value == clientId;
    }

    public void ChangeBet(int direction)
    {
        if (!IsIdle || betAmounts == null || betAmounts.Length == 0) return;

        currentBetIndex = (currentBetIndex + direction) % betAmounts.Length;
        if (currentBetIndex < 0) currentBetIndex += betAmounts.Length;
        RefreshDisplay();
    }

    public void Interact(PlayerEarning playerEarning)
    {
        if (playerEarning == null || !playerEarning.IsSpawned) return;

        if (phase.Value == (int)RoundPhase.Idle)
        {
            StartRoundServerRpc(playerEarning.NetworkObjectId, currentBetIndex);
            return;
        }

        if (phase.Value == (int)RoundPhase.PlayerTurn &&
            roundOwnerClientId.Value == playerEarning.OwnerClientId)
        {
            HitServerRpc();
        }
    }

    public void Stand()
    {
        if (!IsSpawned) return;
        StandServerRpc();
    }

    /// <summary>自分の番で、いまダブルダウンできるか（操作の案内とキーの受け付けに使う）。</summary>
    public bool CanLocalPlayerDouble(ulong clientId)
    {
        return canDouble.Value &&
               phase.Value == (int)RoundPhase.PlayerTurn &&
               roundOwnerClientId.Value == clientId;
    }

    public void DoubleDown()
    {
        if (!IsSpawned) return;
        DoubleServerRpc();
    }

    /// <summary>勝ったとき・ブラックジャックのときに戻る倍率（確認の道具用）。</summary>
    public float BlackjackReturnMultiplier => blackjackReturnMultiplier;
    public bool AllowsDoubleDown => allowDoubleDown;
    public int[] BetAmounts => betAmounts;

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void StartRoundServerRpc(
        ulong playerObjectId,
        int requestedBetIndex,
        RpcParams rpcParams = default)
    {
        if (phase.Value != (int)RoundPhase.Idle) return;
        if (betAmounts == null || betAmounts.Length == 0) return;

        if (!TryGetRequestingPlayer(
                playerObjectId,
                rpcParams.Receive.SenderClientId,
                out PlayerEarning playerEarning))
            return;

        if (Vector3.Distance(playerEarning.transform.position, transform.position) >
            serverInteractDistance)
        {
            Debug.LogWarning("[Blackjack] 遠すぎるゲーム開始要求を拒否しました。");
            return;
        }

        requestedBetIndex = Mathf.Clamp(requestedBetIndex, 0, betAmounts.Length - 1);
        int wager = Mathf.Max(1, betAmounts[requestedBetIndex]);

        if (SharedMoneyManager.Instance == null)
        {
            SetTemporaryMessage("共有口座がありません");
            return;
        }

        if (!SharedMoneyManager.Instance.TrySpend(
                wager,
                SharedMoneyReason.BlackjackBet,
                $"client={rpcParams.Receive.SenderClientId}"))
        {
            SetTemporaryMessage("お金が足りません！");
            return;
        }

        playerEarning.SpendEarning(wager);

        roundPlayerObjectId.Value = playerObjectId;
        roundOwnerClientId.Value = rpcParams.Receive.SenderClientId;
        activeBet.Value = wager;

        playerCards.Clear();
        dealerCards.Clear();
        playerCards.Add(DrawCard());
        dealerCards.Add(DrawCard());
        playerCards.Add(DrawCard());
        dealerCards.Add(DrawCard());

        resultKind.Value = (int)ResultKind.None;
        canDouble.Value = allowDoubleDown;
        phase.Value = (int)RoundPhase.PlayerTurn;
        UpdateHandsForClients(false);
        resultDisplay.Value = new FixedString128Bytes(
            allowDoubleDown ? "HIT / STAND / DOUBLE（賭け金を倍に）" : "EでHIT　RでSTAND");

        int playerTotal = GetHandValue(playerCards);
        int dealerTotal = GetHandValue(dealerCards);

        if (playerTotal == 21 || dealerTotal == 21)
        {
            // 4 枚が配られるのを待ってから、伏せ札をめくって結果を出す
            float deal = cardSeconds * 4f;
            if (playerTotal == 21 && dealerTotal == 21)
                FinishAfter(deal, 0, "引き分け！", wager, ResultKind.Push);
            else if (playerTotal == 21)
                FinishAfter(deal, 0,
                    "ブラックジャック！ 大勝利！",
                    CalculateReturn(wager, blackjackReturnMultiplier),
                    ResultKind.Blackjack);
            else
                FinishAfter(deal, 0, "ディーラーのブラックジャック…", 0, ResultKind.Lose);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void HitServerRpc(RpcParams rpcParams = default)
    {
        if (!IsValidRoundOwner(rpcParams.Receive.SenderClientId)) return;

        canDouble.Value = false;
        playerCards.Add(DrawCard());
        UpdateHandsForClients(false);

        int total = GetHandValue(playerCards);
        if (total > 21)
        {
            FinishAfter(cardSeconds, 0, "バースト！ 負け…", 0, ResultKind.Lose);
        }
        else if (total == 21)
        {
            PlayDealerTurn(cardSeconds);
        }
        else
        {
            resultDisplay.Value = new FixedString128Bytes("もう1枚？　それともSTAND？");
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void StandServerRpc(RpcParams rpcParams = default)
    {
        if (!IsValidRoundOwner(rpcParams.Receive.SenderClientId)) return;
        PlayDealerTurn();
    }

    /// <summary>
    /// ダブルダウン：最初の 2 枚のときだけ。賭け金と同じ額をもう一度払い、1 枚だけ引いて親の番へ進む。
    /// 払えないときは何も変えない（そのままヒットかスタンドを選べる）。
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void DoubleServerRpc(RpcParams rpcParams = default)
    {
        if (!IsValidRoundOwner(rpcParams.Receive.SenderClientId)) return;
        if (!allowDoubleDown || !canDouble.Value || playerCards.Count != 2) return;

        int extra = activeBet.Value;
        if (SharedMoneyManager.Instance == null ||
            !SharedMoneyManager.Instance.TrySpend(
                extra,
                SharedMoneyReason.BlackjackBet,
                $"double client={rpcParams.Receive.SenderClientId}"))
        {
            resultDisplay.Value = new FixedString128Bytes("お金が足りません（HIT か STAND）");
            return;
        }

        if (TryGetRoundPlayer(out PlayerEarning playerEarning)) playerEarning.SpendEarning(extra);

        canDouble.Value = false;
        activeBet.Value = extra * 2;
        playerCards.Add(DrawCard());
        UpdateHandsForClients(false);

        if (GetHandValue(playerCards) > 21)
            FinishAfter(cardSeconds, 0, "バースト！ 負け…", 0, ResultKind.Lose);
        else
            PlayDealerTurn(cardSeconds);
    }

    /// <summary>親の番。waitBefore：その前に配った札（ヒット・ダブルダウンの 1 枚）が置かれるのを待つ秒数。</summary>
    private void PlayDealerTurn(float waitBefore = 0f)
    {
        canDouble.Value = false;
        int before = dealerCards.Count;
        while (GetHandValue(dealerCards) < 17)
            dealerCards.Add(DrawCard());
        int drawn = dealerCards.Count - before;

        int playerTotal = GetHandValue(playerCards);
        int dealerTotal = GetHandValue(dealerCards);

        if (dealerTotal > 21)
            FinishAfter(waitBefore, drawn,
                "ディーラーがバースト！ 勝ち！",
                CalculateReturn(activeBet.Value, normalWinReturnMultiplier),
                ResultKind.Win);
        else if (playerTotal > dealerTotal)
            FinishAfter(waitBefore, drawn,
                "勝ち！",
                CalculateReturn(activeBet.Value, normalWinReturnMultiplier),
                ResultKind.Win);
        else if (playerTotal == dealerTotal)
            FinishAfter(waitBefore, drawn, "引き分け！", activeBet.Value, ResultKind.Push);
        else
            FinishAfter(waitBefore, drawn, "負け…", 0, ResultKind.Lose);
    }

    /// <summary>
    /// 結果を、台の上の動きが終わるのを待ってから確定する（勝ち負けと額は、呼んだ時点で決まっている）。
    /// waitBefore 秒（配っている札が置かれるまで）→ 伏せ札をめくり、親が引いた dealerDrawn 枚を配る → 結果とお金。
    /// その間は DealerTurn で、だれも操作できない。
    /// </summary>
    private void FinishAfter(float waitBefore, int dealerDrawn, string message, int returnAmount, ResultKind kind)
    {
        canDouble.Value = false;
        phase.Value = (int)RoundPhase.DealerTurn;
        resultDisplay.Value = new FixedString128Bytes("…");
        if (finishCoroutine != null) StopCoroutine(finishCoroutine);
        finishCoroutine = StartCoroutine(FinishRoutine(waitBefore, dealerDrawn, message, returnAmount, kind));
    }

    private IEnumerator FinishRoutine(float waitBefore, int dealerDrawn, string message, int returnAmount, ResultKind kind)
    {
        if (waitBefore > 0f) yield return new WaitForSeconds(waitBefore);
        UpdateHandsForClients(true);
        yield return new WaitForSeconds(cardSeconds * (1 + dealerDrawn) + settleSeconds);
        finishCoroutine = null;
        FinishRound(message, returnAmount, kind);
    }

    private void FinishRound(string message, int returnAmount, ResultKind kind)
    {
        canDouble.Value = false;
        UpdateHandsForClients(true);

        if (returnAmount > 0)
        {
            bool credited = SharedMoneyManager.Instance != null &&
                            SharedMoneyManager.Instance.TryAdd(
                                returnAmount,
                                SharedMoneyReason.BlackjackPayout,
                                $"client={roundOwnerClientId.Value}",
                                activeBet.Value);   // おねがいは、賭け金を引いた差だけを数える（引き分けは 0）

            if (credited &&
                TryGetRoundPlayer(out PlayerEarning playerEarning))
                playerEarning.AddEarning(returnAmount);
        }

        int net = returnAmount - activeBet.Value;
        resultDisplay.Value = new FixedString128Bytes(message);
        resultNetAmount.Value = net;
        phase.Value = (int)RoundPhase.Result;
        resultKind.Value = (int)kind;

        if (resetCoroutine != null) StopCoroutine(resetCoroutine);
        resetCoroutine = StartCoroutine(ResetRoundAfterDelay());
    }

    private IEnumerator ResetRoundAfterDelay()
    {
        yield return new WaitForSeconds(resultDisplaySeconds);

        playerCards.Clear();
        dealerCards.Clear();
        playerCardsNet.Value = new FixedString32Bytes();
        dealerCardsNet.Value = new FixedString32Bytes();
        resultDisplay.Value = new FixedString128Bytes("Eでゲーム開始");
        resultNetAmount.Value = 0;
        resultKind.Value = (int)ResultKind.None;
        activeBet.Value = 0;
        roundPlayerObjectId.Value = ulong.MaxValue;
        roundOwnerClientId.Value = ulong.MaxValue;
        phase.Value = (int)RoundPhase.Idle;
        resetCoroutine = null;
    }

    private void SetTemporaryMessage(string message)
    {
        resultDisplay.Value = new FixedString128Bytes(message);
        resultNetAmount.Value = 0;
        resultKind.Value = (int)ResultKind.Error;
        if (resetCoroutine != null) StopCoroutine(resetCoroutine);
        resetCoroutine = StartCoroutine(RestoreIdleMessage());
    }

    private IEnumerator RestoreIdleMessage()
    {
        yield return new WaitForSeconds(1.8f);
        if (phase.Value == (int)RoundPhase.Idle)
        {
            resultDisplay.Value = new FixedString128Bytes("Eでゲーム開始");
            resultKind.Value = (int)ResultKind.None;
        }
        resetCoroutine = null;
    }

    private bool IsValidRoundOwner(ulong senderClientId)
    {
        return phase.Value == (int)RoundPhase.PlayerTurn &&
               roundOwnerClientId.Value == senderClientId;
    }

    private bool TryGetRequestingPlayer(
        ulong playerObjectId,
        ulong senderClientId,
        out PlayerEarning playerEarning)
    {
        playerEarning = null;
        if (NetworkManager.Singleton == null) return false;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                playerObjectId,
                out NetworkObject playerObject))
            return false;

        if (playerObject.OwnerClientId != senderClientId) return false;

        playerEarning = playerObject.GetComponent<PlayerEarning>();
        return playerEarning != null;
    }

    private bool TryGetRoundPlayer(out PlayerEarning playerEarning)
    {
        playerEarning = null;
        if (NetworkManager.Singleton == null) return false;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                roundPlayerObjectId.Value,
                out NetworkObject playerObject))
            return false;

        playerEarning = playerObject.GetComponent<PlayerEarning>();
        return playerEarning != null;
    }

    private void UpdateHandsForClients(bool revealDealer)
    {
        playerCardsNet.Value = new FixedString32Bytes(EncodeHand(playerCards, false));
        dealerCardsNet.Value = new FixedString32Bytes(EncodeHand(dealerCards, !revealDealer));
    }

    /// <summary>カードの並びを文字にする（1 枚 1 文字）。hideSecond のときは 2 枚目を伏せ札にする。</summary>
    private static string EncodeHand(List<Card> cards, bool hideSecond)
    {
        var text = new System.Text.StringBuilder(cards.Count);
        for (int i = 0; i < cards.Count && i < 28; i++)
            text.Append(hideSecond && i == 1 ? BlackjackTableView.Hidden : BlackjackTableView.Encode(cards[i].rank, cards[i].suit));
        return text.ToString();
    }

    private static Card DrawCard()
    {
        return new Card
        {
            rank = Random.Range(1, 14),
            suit = Random.Range(0, 4)
        };
    }

    private static int GetHandValue(List<Card> cards)
    {
        int total = 0;
        int aceCount = 0;

        foreach (Card card in cards)
        {
            if (card.rank == 1)
            {
                total += 11;
                aceCount++;
            }
            else
            {
                total += Mathf.Min(card.rank, 10);
            }
        }

        while (total > 21 && aceCount > 0)
        {
            total -= 10;
            aceCount--;
        }

        return total;
    }

    private static int CalculateReturn(int wager, float multiplier)
    {
        return Mathf.Max(0, Mathf.RoundToInt(wager * multiplier));
    }

    private void OnStateChanged(int oldValue, int newValue)
    {
        RefreshDisplay();
    }

    private void OnStateChanged(ulong oldValue, ulong newValue)
    {
        RefreshDisplay();
    }

    private void OnTextChanged(FixedString128Bytes oldValue, FixedString128Bytes newValue)
    {
        RefreshDisplay();
    }

    private void OnCardsChanged(FixedString32Bytes oldValue, FixedString32Bytes newValue)
    {
        if (view != null) view.SetHands(playerCardsNet.Value.ToString(), dealerCardsNet.Value.ToString());
    }

    /// <summary>賭け金が決まった・倍になった：台の上にチップを置く。</summary>
    private void OnBetChanged(int oldValue, int newValue)
    {
        if (view == null || newValue <= 0) return;
        if (oldValue == 0) view.PlaceBet(Mathf.Max(0, BetIndexOf(newValue)));
        else if (newValue == oldValue * 2) view.PlaceDouble();
    }

    private void OnResultKindChanged(int oldValue, int newValue)
    {
        RefreshDisplay();

        // 結果ごとに音を変える（筐体の位置から鳴らすので、近くの人にだけ聞こえる）
        switch ((ResultKind)newValue)
        {
            case ResultKind.Blackjack:
                GameAudio.PlayAt("blackjack/blackjack", transform.position, 1.1f);
                break;
            case ResultKind.Win:
                GameAudio.PlayAt("blackjack/win", transform.position);
                break;
            case ResultKind.Lose:
                GameAudio.PlayAt("blackjack/lose", transform.position);
                break;
            case ResultKind.Push:
                GameAudio.PlayAt("blackjack/push", transform.position);
                break;
        }

        if (view != null && newValue != (int)ResultKind.None && newValue != (int)ResultKind.Error)
            view.ShowResult(
                newValue == (int)ResultKind.Win || newValue == (int)ResultKind.Blackjack,
                newValue == (int)ResultKind.Lose,
                newValue == (int)ResultKind.Blackjack);

        if (newValue != (int)ResultKind.None && isActiveAndEnabled)
        {
            if (resultAnimationCoroutine != null) StopCoroutine(resultAnimationCoroutine);
            resultAnimationCoroutine = StartCoroutine(AnimateResultPresentation((ResultKind)newValue));
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 画面（Anchor_Display）を組み立てて、参照を割り当てる。配置ツールがプレハブに対して呼ぶ。実行中は呼ばない。
    /// 位置・大きさは上の displayLocalPosition / displaySize / displayScale。置いたあとは、プレハブの中で直接直してよい。
    /// </summary>
    public void EditorBuildUI()
    {
        foreach (string old in new[] { "RuntimeDisplay", "Anchor_Display" })
        {
            Transform existing = transform.Find(old);
            if (existing != null) DestroyImmediate(existing.gameObject);
        }

        GameObject canvasObject = new GameObject(
            "Anchor_Display",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = displayLocalPosition;
        canvasObject.transform.localRotation = Quaternion.Euler(15f, 180f, 0f);
        canvasObject.transform.localScale = Vector3.one * displayScale;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = displaySize;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 5;

        GameObject panelObject = new GameObject(
            "Panel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        panelObject.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        displayPanel = panelObject.GetComponent<Image>();
        displayPanel.color = new Color(0.025f, 0.08f, 0.07f, 0.96f);

        GameObject textObject = new GameObject(
            "BlackjackText",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panelObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(30f, 105f);
        textRect.offsetMax = new Vector2(-30f, -18f);

        displayText = textObject.GetComponent<TextMeshProUGUI>();
        displayText.alignment = TextAlignmentOptions.Center;
        displayText.fontSize = 42f;
        displayText.enableAutoSizing = true;
        displayText.fontSizeMin = 20f;
        displayText.fontSizeMax = 42f;
        displayText.color = Color.white;
        displayText.richText = true;
        displayText.raycastTarget = false;

        GameObject bannerObject = new GameObject(
            "ResultBanner",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        bannerObject.transform.SetParent(panelObject.transform, false);
        resultBannerRect = bannerObject.GetComponent<RectTransform>();
        resultBannerRect.anchorMin = new Vector2(0.04f, 0f);
        resultBannerRect.anchorMax = new Vector2(0.96f, 0f);
        resultBannerRect.pivot = new Vector2(0.5f, 0f);
        resultBannerRect.anchoredPosition = new Vector2(0f, 18f);
        resultBannerRect.sizeDelta = new Vector2(0f, 80f);
        resultBanner = bannerObject.GetComponent<Image>();

        GameObject bannerTextObject = new GameObject(
            "ResultText",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        bannerTextObject.transform.SetParent(bannerObject.transform, false);
        RectTransform bannerTextRect = bannerTextObject.GetComponent<RectTransform>();
        bannerTextRect.anchorMin = Vector2.zero;
        bannerTextRect.anchorMax = Vector2.one;
        bannerTextRect.offsetMin = new Vector2(16f, 6f);
        bannerTextRect.offsetMax = new Vector2(-16f, -6f);

        resultBannerText = bannerTextObject.GetComponent<TextMeshProUGUI>();
        resultBannerText.alignment = TextAlignmentOptions.Center;
        resultBannerText.fontSize = 40f;
        resultBannerText.enableAutoSizing = true;
        resultBannerText.fontSizeMin = 20f;
        resultBannerText.fontSizeMax = 44f;
        resultBannerText.fontStyle = FontStyles.Bold;
        resultBannerText.color = Color.white;
        resultBannerText.raycastTarget = false;

        BuildResultOverlay(panelObject.transform);
        // 再生しなくても、シーンビューで待機中の画面が見えるようにしておく。
        // ここでは英数字だけにする：日本語を入れると、予備のフォント用の子（TMP_SubMeshUI）ができてプレハブに残り、開くたびにエラーが出る
        displayText.text = "<size=115%><b><color=#FFD34E>BLACKJACK</color></b></size>\n<size=85%><color=#FFD34E>BET</color></size>";
        resultBanner.color = new Color(0.09f, 0.18f, 0.24f, 0.96f);
        resultBannerText.text = "PRESS E";
        foreach (TMP_SubMeshUI sub in canvasObject.GetComponentsInChildren<TMP_SubMeshUI>(true))
            DestroyImmediate(sub.gameObject);
    }

    private void BuildResultOverlay(Transform parent)
    {
        GameObject overlayObject = new GameObject(
            "ResultOverlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup),
            typeof(Outline));
        overlayObject.transform.SetParent(parent, false);

        resultOverlayRect = overlayObject.GetComponent<RectTransform>();
        resultOverlayRect.anchorMin = Vector2.zero;
        resultOverlayRect.anchorMax = Vector2.one;
        resultOverlayRect.offsetMin = new Vector2(14f, 14f);
        resultOverlayRect.offsetMax = new Vector2(-14f, -14f);

        resultOverlayBackground = overlayObject.GetComponent<Image>();
        // 角丸の板は、保存できる絵（アセット）を使う。UITheme.PanelSprite は実行中に作る絵なので、プレハブには残せない
        resultOverlayBackground.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Art/UI/RoomiesPanel.png");
        resultOverlayBackground.type = Image.Type.Sliced;
        resultOverlayBackground.raycastTarget = false;

        Outline outline = overlayObject.GetComponent<Outline>();
        outline.effectColor = new Color(1f, 0.83f, 0.31f, 0.9f);
        outline.effectDistance = new Vector2(5f, -5f);
        outline.useGraphicAlpha = true;

        resultOverlayGroup = overlayObject.GetComponent<CanvasGroup>();
        resultOverlayGroup.alpha = 0f;
        resultOverlayGroup.interactable = false;
        resultOverlayGroup.blocksRaycasts = false;

        resultOutcomeText = CreateOverlayText(
            overlayObject.transform,
            "Outcome",
            new Vector2(0.08f, 0.78f),
            new Vector2(0.92f, 0.94f),
            28f,
            38f);
        resultOutcomeText.characterSpacing = 12f;

        resultHeadlineText = CreateOverlayText(
            overlayObject.transform,
            "Headline",
            new Vector2(0.05f, 0.48f),
            new Vector2(0.95f, 0.82f),
            54f,
            92f);
        resultHeadlineText.fontStyle = FontStyles.Bold;

        resultMessageText = CreateOverlayText(
            overlayObject.transform,
            "Message",
            new Vector2(0.08f, 0.31f),
            new Vector2(0.92f, 0.50f),
            25f,
            38f);
        resultMessageText.fontStyle = FontStyles.Bold;

        resultAmountText = CreateOverlayText(
            overlayObject.transform,
            "Amount",
            new Vector2(0.08f, 0.06f),
            new Vector2(0.92f, 0.34f),
            44f,
            72f);
        resultAmountText.fontStyle = FontStyles.Bold;
    }

    private static TextMeshProUGUI CreateOverlayText(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float minFontSize,
        float maxFontSize)
    {
        GameObject textObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI),
            typeof(Outline));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = minFontSize;
        text.fontSizeMax = maxFontSize;
        text.richText = true;
        text.raycastTarget = false;

        Outline outline = textObject.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
        outline.effectDistance = new Vector2(3f, -3f);
        outline.useGraphicAlpha = true;
        return text;
    }
#endif

    private void RefreshDisplay()
    {
        if (displayText == null || resultBannerText == null || resultBanner == null) return;

        string betLine = phase.Value == (int)RoundPhase.Idle
            ? "BET  " + CurrentBetLabel
            : "BET  " + activeBet.Value + " R";

        // モニターは補助：題字と賭け金だけ（カードと点数は台の上）。結果は下の帯と、結果の演出に出る
        displayText.text =
            "<size=120%><b><color=#FFD34E>BLACKJACK</color></b></size>\n" +
            "<size=85%><color=#FFD34E>" + betLine + "</color></size>";

        ResultKind kind = (ResultKind)resultKind.Value;
        Color bannerColor;
        Color panelColor;

        switch (kind)
        {
            case ResultKind.Win:
                bannerColor = new Color(0.04f, 0.66f, 0.34f, 0.98f);
                panelColor = new Color(0.02f, 0.18f, 0.10f, 0.98f);
                break;
            case ResultKind.Blackjack:
                bannerColor = new Color(0.95f, 0.60f, 0.03f, 0.98f);
                panelColor = new Color(0.20f, 0.12f, 0.02f, 0.98f);
                break;
            case ResultKind.Lose:
                bannerColor = new Color(0.78f, 0.08f, 0.14f, 0.98f);
                panelColor = new Color(0.20f, 0.025f, 0.035f, 0.98f);
                break;
            case ResultKind.Push:
                bannerColor = new Color(0.88f, 0.63f, 0.05f, 0.98f);
                panelColor = new Color(0.16f, 0.12f, 0.025f, 0.98f);
                break;
            case ResultKind.Error:
                bannerColor = new Color(0.92f, 0.28f, 0.06f, 0.98f);
                panelColor = new Color(0.18f, 0.06f, 0.02f, 0.98f);
                break;
            default:
                bannerColor = phase.Value == (int)RoundPhase.PlayerTurn
                    ? new Color(0.07f, 0.40f, 0.70f, 0.98f)
                    : new Color(0.20f, 0.26f, 0.25f, 0.98f);
                panelColor = new Color(0.025f, 0.08f, 0.07f, 0.96f);
                break;
        }

        resultBanner.color = bannerColor;
        resultBannerText.text = resultDisplay.Value.ToString();
        if (displayPanel != null) displayPanel.color = panelColor;
        RefreshResultOverlay(kind);
    }

    private void RefreshResultOverlay(ResultKind kind)
    {
        if (resultOverlayGroup == null ||
            resultOverlayBackground == null ||
            resultHeadlineText == null ||
            resultMessageText == null ||
            resultAmountText == null ||
            resultOutcomeText == null)
            return;

        if (kind == ResultKind.None)
        {
            resultOverlayGroup.alpha = 0f;
            if (resultOverlayRect != null)
            {
                resultOverlayRect.localScale = Vector3.one;
                resultOverlayRect.anchoredPosition = Vector2.zero;
            }
            return;
        }

        string headline;
        string outcome;
        Color background;
        Color accent;

        switch (kind)
        {
            case ResultKind.Win:
                headline = "勝 利！";
                outcome = "YOU WIN";
                background = new Color(0.025f, 0.25f, 0.14f, 0.99f);
                accent = new Color(0.47f, 1f, 0.51f, 1f);
                break;
            case ResultKind.Blackjack:
                headline = "BLACKJACK!";
                outcome = "♠  SPECIAL WIN  ♠"; // ★ はフォントに無く □ になる
                background = new Color(0.22f, 0.08f, 0.28f, 0.99f);
                accent = new Color(1f, 0.82f, 0.18f, 1f);
                break;
            case ResultKind.Lose:
                headline = "敗 北…";
                outcome = "YOU LOSE";
                background = new Color(0.28f, 0.025f, 0.055f, 0.99f);
                accent = new Color(1f, 0.32f, 0.38f, 1f);
                break;
            case ResultKind.Push:
                headline = "引き分け";
                outcome = "DRAW";
                background = new Color(0.24f, 0.16f, 0.035f, 0.99f);
                accent = new Color(1f, 0.78f, 0.25f, 1f);
                break;
            default:
                headline = "できません";
                outcome = "CHECK!";
                background = new Color(0.24f, 0.075f, 0.025f, 0.99f);
                accent = new Color(1f, 0.48f, 0.16f, 1f);
                break;
        }

        int net = resultNetAmount.Value;
        resultOverlayBackground.color = background;
        resultHeadlineText.color = accent;
        resultOutcomeText.color = new Color(accent.r, accent.g, accent.b, 0.92f);
        resultMessageText.color = Color.white;
        resultAmountText.color = accent;
        resultHeadlineText.text = headline;
        resultOutcomeText.text = outcome;
        resultMessageText.text = resultDisplay.Value.ToString();

        if (kind == ResultKind.Error)
            resultAmountText.text = "";
        else if (net > 0)
            resultAmountText.text = "+" + net + " R";
        else if (net < 0)
            resultAmountText.text = net + " R";
        else
            resultAmountText.text = "±0 R";
    }

    private IEnumerator AnimateResultPresentation(ResultKind kind)
    {
        if (resultOverlayGroup == null || resultOverlayRect == null) yield break;

        RefreshResultOverlay(kind);
        resultOverlayGroup.alpha = 0f;
        resultOverlayRect.localScale = Vector3.one * 0.62f;
        resultOverlayRect.anchoredPosition = Vector2.zero;

        const float entranceDuration = 0.42f;
        float elapsed = 0f;

        while (elapsed < entranceDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / entranceDuration);
            resultOverlayGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 2f));
            float scale = t < 0.68f
                ? Mathf.Lerp(0.62f, 1.12f, t / 0.68f)
                : Mathf.Lerp(1.12f, 1f, (t - 0.68f) / 0.32f);
            resultOverlayRect.localScale = Vector3.one * scale;
            yield return null;
        }

        resultOverlayGroup.alpha = 1f;
        resultOverlayRect.localScale = Vector3.one;

        const float emphasisDuration = 0.7f;
        elapsed = 0f;
        while (elapsed < emphasisDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / emphasisDuration);

            if (kind == ResultKind.Lose || kind == ResultKind.Error)
            {
                float shake = Mathf.Sin(t * Mathf.PI * 12f) * (1f - t) * 20f;
                resultOverlayRect.anchoredPosition = new Vector2(shake, 0f);
            }
            else
            {
                float pulse = 1f + Mathf.Sin(t * Mathf.PI * 4f) * (1f - t) * 0.045f;
                resultOverlayRect.localScale = Vector3.one * pulse;
            }

            yield return null;
        }

        resultOverlayRect.anchoredPosition = Vector2.zero;
        resultOverlayRect.localScale = Vector3.one;
        resultAnimationCoroutine = null;
    }
}
