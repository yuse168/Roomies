using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class SlotMachine : NetworkBehaviour
{
    [System.Serializable]
    public class SlotSymbol
    {
        public string symbol = "C";
        public int reward = 10;

        [Range(0, 100)]
        public int weight = 50;

        public bool isFeverSymbol = false;
    }

    [Header("賭け金")]
    [SerializeField] private int[] betAmounts = { 10, 50, 100 };
    private int currentBetIndex = 0;

    private int CurrentBet => betAmounts[currentBetIndex];
    private float BetMultiplier => (float)CurrentBet / betAmounts[0];

    [Header("スロットの目")]
    [SerializeField]
    private SlotSymbol[] slotSymbols =
    {
        new SlotSymbol { symbol = "C", reward = 10, weight = 50 },
        new SlotSymbol { symbol = "L", reward = 15, weight = 30 },
        new SlotSymbol { symbol = "S", reward = 30, weight = 15 },
        new SlotSymbol { symbol = "D", reward = 50, weight = 5 },
        new SlotSymbol { symbol = "7", reward = 100, weight = 2, isFeverSymbol = true }
    };

    [Header("ハズレ確率")]
    [SerializeField, Range(0, 100)]
    private int missPercent = 50;

    [Header("フィーバー設定")]
    [SerializeField] private int feverFreeSpins = 3;
    [SerializeField] private int feverMultiplier = 2;

    [Header("Server検証")]
    [SerializeField, Min(0.1f)] private float serverInteractDistance = 4f;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI reelText1;
    [SerializeField] private TextMeshProUGUI reelText2;
    [SerializeField] private TextMeshProUGUI reelText3;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private TextMeshProUGUI feverText;
    [SerializeField] private TextMeshProUGUI betText;
    [SerializeField] private Sprite[] symbolSprites = new Sprite[5];

    [Header("演出")]
    [SerializeField] private float spinDuration = 0.8f;
    [SerializeField] private float spinInterval = 0.05f;
    [SerializeField] private float reelStopDelay = 0.28f;
    [SerializeField] private float stopPulseScale = 1.2f;
    [SerializeField] private float stopPulseDuration = 0.15f;

    private static readonly string[] SymbolFaces =
    {
        "<color=#FF4D6D>CHERRY</color>",
        "<color=#FFD166>STAR</color>",
        "<color=#73E2A7>BAR</color>",
        "<color=#70B7FF>GEM</color>",
        "<color=#FF3B30>7</color>"
    };

    private static readonly string[] SymbolLabels =
    {
        "CHERRY",
        "STAR",
        "BAR",
        "GEM",
        "7"
    };

    private NetworkVariable<int> feverSpinCount =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private NetworkVariable<bool> isSpinning =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private Coroutine spinCoroutine;
    private readonly Dictionary<TextMeshProUGUI, ReelImages> reelImages = new();

    private sealed class ReelImages
    {
        public Image previous;
        public Image current;
        public Image next;
    }

    private void Start()
    {
        feverSpinCount.OnValueChanged += OnFeverChanged;
        PreparePresentation();
        SetReels(0, 1, Mathf.Min(4, slotSymbols.Length - 1));
        UpdateFeverUI();
        UpdateBetUI();
    }

    public string CurrentBetLabel => CurrentBet + " R";

    public void ChangeBet(int direction)
    {
        if (isSpinning.Value) return;
        if (betAmounts == null || betAmounts.Length == 0) return;

        int nextIndex = (currentBetIndex + direction) % betAmounts.Length;
        if (nextIndex < 0) nextIndex += betAmounts.Length;
        SetBetIndex(nextIndex);
    }

    private void SetBetIndex(int index)
    {
        if (index >= 0 && index < betAmounts.Length)
        {
            currentBetIndex = index;
            UpdateBetUI();
        }
    }

    private void UpdateBetUI()
    {
        if (betText == null) return;

        betText.text =
            "<size=60%><color=#A8B0C0>BET</color></size>  " +
            "<color=#FFD34E><b>" + CurrentBet + " R</b></color>\n" +
            "<size=40%><color=#7E8CA5>&lt;  MOUSE WHEEL  &gt;</color></size>";
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        feverSpinCount.OnValueChanged -= OnFeverChanged;
    }

    public void Interact(PlayerEarning playerEarning)
    {
        if (isSpinning.Value) return;
        if (playerEarning == null || !playerEarning.IsSpawned) return;
        if (betAmounts == null || betAmounts.Length == 0) return;

        PlaySlotServerRpc(playerEarning.NetworkObjectId, currentBetIndex);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PlaySlotServerRpc(
        ulong playerNetworkObjectId,
        int betIndex,
        RpcParams rpcParams = default)
    {
        if (isSpinning.Value) return;
        if (betAmounts == null || betAmounts.Length == 0) return;

        betIndex = Mathf.Clamp(betIndex, 0, betAmounts.Length - 1);
        int cost = Mathf.Max(1, betAmounts[betIndex]);
        float multiplier = (float)cost / Mathf.Max(1, betAmounts[0]);

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(playerNetworkObjectId, out NetworkObject requestingPlayer) ||
            requestingPlayer.OwnerClientId != rpcParams.Receive.SenderClientId)
        {
            Debug.LogWarning("[Slot] 他プレイヤー名義のBET要求を拒否しました。");
            return;
        }

        if (Vector3.Distance(requestingPlayer.transform.position, transform.position) >
            serverInteractDistance)
        {
            Debug.LogWarning("[Slot] 遠すぎるBET要求を拒否しました。");
            return;
        }

        PlayerEarning requestingEarning =
            requestingPlayer.GetComponent<PlayerEarning>();

        if (SharedMoneyManager.Instance == null)
        {
            ShowResultClientRpc("口座なし");
            return;
        }

        bool isFeverSpin = feverSpinCount.Value > 0;

        if (!isFeverSpin)
        {
            if (!SharedMoneyManager.Instance.TrySpend(
                    cost,
                    SharedMoneyReason.SlotBet,
                    $"client={rpcParams.Receive.SenderClientId}"))
            {
                ShowResultClientRpc("お金不足");
                return;
            }

            if (requestingEarning != null) requestingEarning.SpendEarning(cost);
        }
        else
        {
            feverSpinCount.Value--;
        }

        isSpinning.Value = true;

        bool isMiss = Random.Range(0, 100) < missPercent;

        int r1;
        int r2;
        int r3;

        int reward = 0;
        bool addFever = false;
        string resultMessage;

        if (isMiss)
        {
            GetMissResult(out r1, out r2, out r3);
            resultMessage = isFeverSpin ? "FEVER MISS" : "MISS";
        }
        else
        {
            int winIndex = GetRandomSymbolIndexByWeight();

            r1 = winIndex;
            r2 = winIndex;
            r3 = winIndex;

            reward = Mathf.RoundToInt(slotSymbols[winIndex].reward * multiplier);

            if (isFeverSpin)
            {
                reward *= feverMultiplier;
            }

            if (slotSymbols[winIndex].isFeverSymbol)
            {
                addFever = true;
                resultMessage = "FEVER +" + reward;
            }
            else if (isFeverSpin)
            {
                resultMessage = "FEVER WIN +" + reward;
            }
            else
            {
                resultMessage = "WIN +" + reward;
            }
        }

        SpinClientRpc(r1, r2, r3, resultMessage);

        StartCoroutine(ApplyRewardAfterSpin(reward, addFever, playerNetworkObjectId));
    }

    private float GetSpinAnimationDuration()
    {
        return spinDuration + reelStopDelay * 2f + stopPulseDuration;
    }

    private IEnumerator ApplyRewardAfterSpin(int reward, bool addFever, ulong playerNetworkObjectId)
    {
        yield return new WaitForSeconds(GetSpinAnimationDuration());

        if (reward > 0)
        {
            // 共同口座へ
            bool credited = SharedMoneyManager.Instance != null &&
                            SharedMoneyManager.Instance.TryAdd(
                                reward,
                                SharedMoneyReason.SlotReward,
                                $"playerObject={playerNetworkObjectId}");

            // 個人の収支にも反映（賭け金を個人から引いているので当たりも個人へ加算）
            if (credited &&
                NetworkManager.Singleton.SpawnManager.SpawnedObjects
                .TryGetValue(playerNetworkObjectId, out NetworkObject playerObj))
            {
                PlayerEarning earning = playerObj.GetComponent<PlayerEarning>();
                if (earning != null)
                {
                    earning.AddEarning(reward);
                }
            }
        }

        if (addFever)
        {
            feverSpinCount.Value += feverFreeSpins;
        }

        isSpinning.Value = false;
    }

    private int GetRandomSymbolIndexByWeight()
    {
        int totalWeight = 0;

        for (int i = 0; i < slotSymbols.Length; i++)
        {
            totalWeight += Mathf.Max(0, slotSymbols[i].weight);
        }

        if (totalWeight <= 0)
        {
            return 0;
        }

        int randomValue = Random.Range(0, totalWeight);
        int currentWeight = 0;

        for (int i = 0; i < slotSymbols.Length; i++)
        {
            currentWeight += Mathf.Max(0, slotSymbols[i].weight);

            if (randomValue < currentWeight)
            {
                return i;
            }
        }

        return 0;
    }

    private void GetMissResult(out int r1, out int r2, out int r3)
    {
        r1 = Random.Range(0, slotSymbols.Length);
        r2 = Random.Range(0, slotSymbols.Length);
        r3 = Random.Range(0, slotSymbols.Length);

        while (r1 == r2 && r2 == r3)
        {
            r3 = Random.Range(0, slotSymbols.Length);
        }
    }

    [ClientRpc]
    private void SpinClientRpc(int r1, int r2, int r3, string resultMessage)
    {
        if (spinCoroutine != null)
        {
            StopCoroutine(spinCoroutine);
        }

        spinCoroutine = StartCoroutine(
            SpinRoutine(r1, r2, r3, resultMessage)
        );
    }

    private IEnumerator SpinRoutine(int r1, int r2, int r3, string resultMessage)
    {
        if (resultText != null)
        {
            resultText.text = "<color=#FFD34E>GOOD LUCK!</color>";
            resultText.color = Color.white;
        }

        float elapsed = 0f;
        float[] stopTimes =
        {
            spinDuration,
            spinDuration + reelStopDelay,
            spinDuration + reelStopDelay * 2f
        };
        float[] nextTicks = { 0f, 0f, 0f };
        int[] shown =
        {
            Random.Range(0, slotSymbols.Length),
            Random.Range(0, slotSymbols.Length),
            Random.Range(0, slotSymbols.Length)
        };
        int[] targets = { r1, r2, r3 };
        TextMeshProUGUI[] reels = { reelText1, reelText2, reelText3 };
        bool[] stopped = { false, false, false };

        while (elapsed < stopTimes[2])
        {
            elapsed += Time.deltaTime;

            for (int i = 0; i < reels.Length; i++)
            {
                if (stopped[i]) continue;

                if (elapsed >= stopTimes[i])
                {
                    stopped[i] = true;
                    shown[i] = targets[i];
                    SetReel(reels[i], shown[i]);
                    StartCoroutine(PulseReel(reels[i]));

                    if (i == 1 && targets[0] == targets[1] && targets[2] != targets[1] && resultText != null)
                    {
                        resultText.text = "<color=#FF8A3D>ONE MORE...</color>";
                    }
                    continue;
                }

                if (elapsed >= nextTicks[i])
                {
                    shown[i] = (shown[i] + 1) % slotSymbols.Length;
                    SetReel(reels[i], shown[i]);

                    float progress = Mathf.Clamp01(elapsed / stopTimes[i]);
                    float slowDown = Mathf.InverseLerp(0.58f, 1f, progress);
                    float interval = Mathf.Lerp(spinInterval, spinInterval * 4.5f, slowDown * slowDown);
                    nextTicks[i] = elapsed + interval;
                }
            }

            yield return null;
        }

        SetReels(r1, r2, r3);

        if (resultText != null)
        {
            resultText.text = resultMessage;

            if (resultMessage.Contains("FEVER WIN"))
            {
                resultText.color = new Color(1f, 0.8f, 0.2f);
            }
            else if (resultMessage.Contains("WIN"))
            {
                resultText.color = Color.green;
            }
            else if (resultMessage.Contains("FEVER MISS"))
            {
                resultText.color = new Color(1f, 0.5f, 0.3f);
            }
            else
            {
                resultText.color = new Color(0.7f, 0.7f, 0.7f);
            }
        }

        UpdateFeverUI();

        spinCoroutine = null;
    }

    private IEnumerator PulseReel(TextMeshProUGUI reel)
    {
        if (reel == null) yield break;

        Vector3 originalScale = reel.transform.localScale;
        Vector3 bigScale = originalScale * stopPulseScale;
        float half = stopPulseDuration / 2f;

        reel.transform.localScale = bigScale;
        yield return new WaitForSeconds(half);
        reel.transform.localScale = originalScale;
    }

    private void SetReels(int r1, int r2, int r3)
    {
        SetReel(reelText1, r1);
        SetReel(reelText2, r2);
        SetReel(reelText3, r3);
    }

    private void SetReel(TextMeshProUGUI reel, int symbolIndex)
    {
        if (reel == null || slotSymbols == null || slotSymbols.Length == 0) return;

        symbolIndex = Mathf.Clamp(symbolIndex, 0, slotSymbols.Length - 1);
        int previous = (symbolIndex - 1 + slotSymbols.Length) % slotSymbols.Length;
        int next = (symbolIndex + 1) % slotSymbols.Length;

        if (reelImages.TryGetValue(reel, out ReelImages images) && HasSpriteSet())
        {
            images.previous.sprite = symbolSprites[previous];
            images.current.sprite = symbolSprites[symbolIndex];
            images.next.sprite = symbolSprites[next];
            reel.text = "";
            reel.enabled = false;
            return;
        }

        reel.enabled = true;
        reel.text =
            "<size=48%><color=#596274>" + GetSymbolLabel(previous) + "</color></size>\n" +
            "<size=100%><b>" + GetSymbolFace(symbolIndex) + "</b></size>\n" +
            "<size=48%><color=#596274>" + GetSymbolLabel(next) + "</color></size>";
    }

    private string GetSymbolFace(int index)
    {
        if (index >= 0 && index < SymbolFaces.Length) return SymbolFaces[index];
        return slotSymbols[index].symbol;
    }

    private string GetSymbolLabel(int index)
    {
        if (index >= 0 && index < SymbolLabels.Length) return SymbolLabels[index];
        return slotSymbols[index].symbol;
    }

    private void PreparePresentation()
    {
        PrepareReel(reelText1, new Color(1f, 0.28f, 0.38f, 0.24f));
        PrepareReel(reelText2, new Color(1f, 0.76f, 0.18f, 0.24f));
        PrepareReel(reelText3, new Color(0.25f, 0.62f, 1f, 0.24f));

        if (reelText2 != null)
        {
            Transform parent = reelText2.transform.parent;
            Transform existing = parent.Find("WinLine");
            if (existing == null)
            {
                GameObject lineObject = new GameObject("WinLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform line = lineObject.GetComponent<RectTransform>();
                line.SetParent(parent, false);
                line.SetSiblingIndex(0);
                line.anchoredPosition = Vector2.zero;
                line.sizeDelta = new Vector2(470f, 4f);
                lineObject.GetComponent<Image>().color = new Color(1f, 0.75f, 0.12f, 0.75f);
            }
        }

        if (resultText != null)
        {
            resultText.fontStyle = FontStyles.Bold;
            resultText.text = "<color=#A8B0C0>READY</color>";
        }
    }

    private void PrepareReel(TextMeshProUGUI reel, Color glowColor)
    {
        if (reel == null) return;

        RectTransform rect = reel.rectTransform;
        rect.sizeDelta = new Vector2(118f, 160f);
        reel.fontSize = 46f;
        reel.alignment = TextAlignmentOptions.Center;
        reel.textWrappingMode = TextWrappingModes.NoWrap;
        reel.raycastTarget = false;
        reel.lineSpacing = -28f;

        ReelImages images = new ReelImages
        {
            previous = GetOrCreateSymbolImage(reel.transform, "PreviousEmoji", new Vector2(0f, 48f), 42f, 0.32f),
            current = GetOrCreateSymbolImage(reel.transform, "CurrentEmoji", Vector2.zero, 70f, 1f),
            next = GetOrCreateSymbolImage(reel.transform, "NextEmoji", new Vector2(0f, -48f), 42f, 0.32f)
        };
        reelImages[reel] = images;
        reel.enabled = !HasSpriteSet();

        Transform parent = reel.transform.parent;
        string glowName = reel.gameObject.name + "Glow";
        Transform existing = parent.Find(glowName);
        if (existing != null) return;

        GameObject glowObject = new GameObject(glowName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform glow = glowObject.GetComponent<RectTransform>();
        glow.SetParent(parent, false);
        glow.anchorMin = rect.anchorMin;
        glow.anchorMax = rect.anchorMax;
        glow.pivot = rect.pivot;
        glow.anchoredPosition = rect.anchoredPosition;
        glow.sizeDelta = rect.sizeDelta + new Vector2(16f, 8f);
        glow.SetSiblingIndex(reel.transform.GetSiblingIndex());
        glowObject.GetComponent<Image>().color = glowColor;
    }

    private Image GetOrCreateSymbolImage(Transform parent, string objectName, Vector2 position, float size, float alpha)
    {
        Transform existing = parent.Find(objectName);
        GameObject imageObject = existing != null
            ? existing.gameObject
            : new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        if (existing == null) rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(size, size);

        Image image = imageObject.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = new Color(1f, 1f, 1f, alpha);
        return image;
    }

    private bool HasSpriteSet()
    {
        if (symbolSprites == null || symbolSprites.Length < slotSymbols.Length) return false;

        for (int i = 0; i < slotSymbols.Length; i++)
        {
            if (symbolSprites[i] == null) return false;
        }

        return true;
    }

    private void OnFeverChanged(int oldValue, int newValue)
    {
        UpdateFeverUI();
    }

    private Coroutine feverPulseCoroutine;

    private void UpdateFeverUI()
    {
        if (feverText == null) return;

        if (feverSpinCount.Value > 0)
        {
            feverText.text =
                "FEVER x" +
                feverMultiplier +
                " 残り " +
                feverSpinCount.Value;

            feverText.color = new Color(1f, 0.85f, 0.3f);

            if (feverPulseCoroutine == null)
            {
                feverPulseCoroutine = StartCoroutine(FeverPulse());
            }
        }
        else
        {
            if (feverPulseCoroutine != null)
            {
                StopCoroutine(feverPulseCoroutine);
                feverPulseCoroutine = null;
            }

            feverText.text = "";
        }
    }

    private IEnumerator FeverPulse()
    {
        float time = 0f;

        while (true)
        {
            time += Time.deltaTime * 3f;
            float alpha = Mathf.PingPong(time, 1f);
            feverText.color = new Color(1f, 0.85f, 0.3f, Mathf.Lerp(0.6f, 1f, alpha));

            yield return null;
        }
    }

    [ClientRpc]
    private void ShowResultClientRpc(string message)
    {
        if (resultText != null)
        {
            resultText.text = message;
        }
    }
}
