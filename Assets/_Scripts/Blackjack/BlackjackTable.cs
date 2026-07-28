using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// シーンへPrefabを配置するだけで動作する、Server権限のブラックジャック台。
/// 見た目とワールドスペースUIは仮素材としてランタイム生成する。
/// </summary>
public class BlackjackTable : NetworkBehaviour
{
    private enum RoundPhase
    {
        Idle,
        PlayerTurn,
        Result
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

    [Header("表示")]
    [SerializeField] private Vector3 displayLocalPosition = new Vector3(0f, 1.35f, 0.68f);
    [SerializeField] private Vector2 displaySize = new Vector2(700f, 430f);
    [SerializeField, Min(0.0001f)] private float displayScale = 0.0025f;
    [SerializeField, Min(0.1f)] private float resultDisplaySeconds = 2.5f;

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

    private readonly NetworkVariable<FixedString512Bytes> playerHandDisplay = new(
        new FixedString512Bytes("-"),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<FixedString512Bytes> dealerHandDisplay = new(
        new FixedString512Bytes("-"),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<FixedString128Bytes> resultDisplay = new(
        new FixedString128Bytes("Eでゲーム開始"),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> resultKind = new(
        (int)ResultKind.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly List<Card> playerCards = new();
    private readonly List<Card> dealerCards = new();

    private int currentBetIndex;
    private TextMeshProUGUI displayText;
    private Image displayPanel;
    private Image resultBanner;
    private TextMeshProUGUI resultBannerText;
    private RectTransform resultBannerRect;
    private Coroutine resetCoroutine;
    private Coroutine resultPulseCoroutine;

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
        BuildDisplayIfNeeded();
        RefreshDisplay();
    }

    public override void OnNetworkSpawn()
    {
        phase.OnValueChanged += OnStateChanged;
        roundOwnerClientId.OnValueChanged += OnStateChanged;
        activeBet.OnValueChanged += OnStateChanged;
        playerHandDisplay.OnValueChanged += OnTextChanged;
        dealerHandDisplay.OnValueChanged += OnTextChanged;
        resultDisplay.OnValueChanged += OnTextChanged;
        resultKind.OnValueChanged += OnResultKindChanged;
        RefreshDisplay();
    }

    public override void OnNetworkDespawn()
    {
        phase.OnValueChanged -= OnStateChanged;
        roundOwnerClientId.OnValueChanged -= OnStateChanged;
        activeBet.OnValueChanged -= OnStateChanged;
        playerHandDisplay.OnValueChanged -= OnTextChanged;
        dealerHandDisplay.OnValueChanged -= OnTextChanged;
        resultDisplay.OnValueChanged -= OnTextChanged;
        resultKind.OnValueChanged -= OnResultKindChanged;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (resetCoroutine != null) StopCoroutine(resetCoroutine);
        if (resultPulseCoroutine != null) StopCoroutine(resultPulseCoroutine);
    }

    public string GetInteractionLabel(ulong clientId)
    {
        RoundPhase currentPhase = (RoundPhase)phase.Value;

        if (currentPhase == RoundPhase.Idle)
            return "DEAL   <color=#FFA31F>WHEEL</color> BET " + CurrentBetLabel;

        if (currentPhase == RoundPhase.PlayerTurn && roundOwnerClientId.Value == clientId)
            return "HIT   <color=#FFA31F>R</color> STAND";

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
        phase.Value = (int)RoundPhase.PlayerTurn;
        UpdateHandsForClients(false);
        resultDisplay.Value = new FixedString128Bytes("EでHIT　RでSTAND");

        int playerTotal = GetHandValue(playerCards);
        int dealerTotal = GetHandValue(dealerCards);

        if (playerTotal == 21 || dealerTotal == 21)
        {
            if (playerTotal == 21 && dealerTotal == 21)
                FinishRound("引き分け！", wager, ResultKind.Push);
            else if (playerTotal == 21)
                FinishRound(
                    "ブラックジャック！ 大勝利！",
                    CalculateReturn(wager, blackjackReturnMultiplier),
                    ResultKind.Blackjack);
            else
                FinishRound("ディーラーのブラックジャック…", 0, ResultKind.Lose);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void HitServerRpc(RpcParams rpcParams = default)
    {
        if (!IsValidRoundOwner(rpcParams.Receive.SenderClientId)) return;

        playerCards.Add(DrawCard());
        UpdateHandsForClients(false);

        int total = GetHandValue(playerCards);
        if (total > 21)
        {
            FinishRound("バースト！ 負け…", 0, ResultKind.Lose);
        }
        else if (total == 21)
        {
            PlayDealerTurn();
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

    private void PlayDealerTurn()
    {
        while (GetHandValue(dealerCards) < 17)
            dealerCards.Add(DrawCard());

        int playerTotal = GetHandValue(playerCards);
        int dealerTotal = GetHandValue(dealerCards);

        if (dealerTotal > 21)
            FinishRound(
                "ディーラーがバースト！ 勝ち！",
                CalculateReturn(activeBet.Value, normalWinReturnMultiplier),
                ResultKind.Win);
        else if (playerTotal > dealerTotal)
            FinishRound(
                "勝ち！",
                CalculateReturn(activeBet.Value, normalWinReturnMultiplier),
                ResultKind.Win);
        else if (playerTotal == dealerTotal)
            FinishRound("引き分け！", activeBet.Value, ResultKind.Push);
        else
            FinishRound("負け…", 0, ResultKind.Lose);
    }

    private void FinishRound(string message, int returnAmount, ResultKind kind)
    {
        UpdateHandsForClients(true);

        if (returnAmount > 0)
        {
            bool credited = SharedMoneyManager.Instance != null &&
                            SharedMoneyManager.Instance.TryAdd(
                                returnAmount,
                                SharedMoneyReason.BlackjackPayout,
                                $"client={roundOwnerClientId.Value}");

            if (credited &&
                TryGetRoundPlayer(out PlayerEarning playerEarning))
                playerEarning.AddEarning(returnAmount);
        }

        int net = returnAmount - activeBet.Value;
        string netText = net > 0 ? "  +" + net + "R" : net < 0 ? "  " + net + "R" : "";
        resultDisplay.Value = new FixedString128Bytes(message + netText);
        resultKind.Value = (int)kind;
        phase.Value = (int)RoundPhase.Result;

        if (resetCoroutine != null) StopCoroutine(resetCoroutine);
        resetCoroutine = StartCoroutine(ResetRoundAfterDelay());
    }

    private IEnumerator ResetRoundAfterDelay()
    {
        yield return new WaitForSeconds(resultDisplaySeconds);

        playerCards.Clear();
        dealerCards.Clear();
        playerHandDisplay.Value = new FixedString512Bytes("-");
        dealerHandDisplay.Value = new FixedString512Bytes("-");
        resultDisplay.Value = new FixedString128Bytes("Eでゲーム開始");
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
        int playerTotal = GetHandValue(playerCards);
        playerHandDisplay.Value = new FixedString512Bytes(
            FormatHand(playerCards, false) + "  = " + playerTotal);

        if (revealDealer)
        {
            dealerHandDisplay.Value = new FixedString512Bytes(
                FormatHand(dealerCards, false) + "  = " + GetHandValue(dealerCards));
        }
        else
        {
            dealerHandDisplay.Value = new FixedString512Bytes(
                FormatHand(dealerCards, true) + "  = ?");
        }
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

    private static string FormatHand(List<Card> cards, bool hideSecondCard)
    {
        if (cards.Count == 0) return "-";

        string text = "";
        for (int i = 0; i < cards.Count; i++)
        {
            if (i > 0) text += "  ";
            text += hideSecondCard && i == 1 ? "??" : FormatCard(cards[i]);
        }

        return text;
    }

    private static string FormatCard(Card card)
    {
        string rank = card.rank switch
        {
            1 => "A",
            11 => "J",
            12 => "Q",
            13 => "K",
            _ => card.rank.ToString()
        };

        string suit = card.suit switch
        {
            0 => "♠",
            1 => "♥",
            2 => "♦",
            _ => "♣"
        };

        bool isRed = card.suit == 1 || card.suit == 2;
        string color = isRed ? "#D93452" : "#102027";
        return "<mark=#FFF7E8><color=" + color + ">[" + rank + suit + "]</color></mark>";
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

    private void OnTextChanged(FixedString512Bytes oldValue, FixedString512Bytes newValue)
    {
        RefreshDisplay();
    }

    private void OnResultKindChanged(int oldValue, int newValue)
    {
        RefreshDisplay();

        if (newValue != (int)ResultKind.None && isActiveAndEnabled)
        {
            if (resultPulseCoroutine != null) StopCoroutine(resultPulseCoroutine);
            resultPulseCoroutine = StartCoroutine(PulseResultBanner());
        }
    }

    private void BuildDisplayIfNeeded()
    {
        Transform existing = transform.Find("RuntimeDisplay");
        if (existing != null)
        {
            Transform panel = existing.Find("Panel");
            Transform text = panel != null ? panel.Find("BlackjackText") : null;
            Transform banner = panel != null ? panel.Find("ResultBanner") : null;

            displayPanel = panel != null ? panel.GetComponent<Image>() : null;
            displayText = text != null ? text.GetComponent<TextMeshProUGUI>() : null;
            resultBanner = banner != null ? banner.GetComponent<Image>() : null;
            resultBannerRect = banner != null ? banner.GetComponent<RectTransform>() : null;
            resultBannerText = banner != null
                ? banner.GetComponentInChildren<TextMeshProUGUI>(true)
                : null;

            if (displayText != null && resultBannerText != null) return;
        }

        GameObject canvasObject = new GameObject(
            "RuntimeDisplay",
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
    }

    private void RefreshDisplay()
    {
        if (displayText == null || resultBannerText == null || resultBanner == null) return;

        string betLine = phase.Value == (int)RoundPhase.Idle
            ? "BET  " + CurrentBetLabel
            : "BET  " + activeBet.Value + " R";

        displayText.text =
            "<size=115%><b><color=#FFD34E>BLACKJACK</color></b></size>\n" +
            "<size=72%><color=#FF8C99>▼ DEALER ▼</color></size>\n" +
            "<size=115%><b>" + dealerHandDisplay.Value + "</b></size>\n" +
            "<color=#526970>━━━━━━━━━━━━</color>\n" +
            "<size=72%><color=#74E0B5>▲ PLAYER ▲</color></size>\n" +
            "<size=115%><b>" + playerHandDisplay.Value + "</b></size>\n" +
            "<size=78%><color=#FFD34E>" + betLine + "</color></size>";

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
    }

    private IEnumerator PulseResultBanner()
    {
        if (resultBannerRect == null) yield break;

        float duration = 0.46f;
        float elapsed = 0f;
        resultBannerRect.localScale = Vector3.one * 0.72f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = t < 0.55f
                ? Mathf.Lerp(0.72f, 1.18f, t / 0.55f)
                : Mathf.Lerp(1.18f, 1f, (t - 0.55f) / 0.45f);
            resultBannerRect.localScale = Vector3.one * scale;
            yield return null;
        }

        resultBannerRect.localScale = Vector3.one;
        resultPulseCoroutine = null;
    }
}
