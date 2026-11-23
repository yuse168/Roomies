using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

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
    [Tooltip("距離判定は筐体のColliderを基準にする。インポート由来の親原点は使用しない。")]
    [SerializeField] private Collider interactionCollider;

    [Header("画面の文字（プレハブの Anchor_Screen の中。配置ツールが割り当てる）")]
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private TextMeshProUGUI feverText;
    [SerializeField] private TextMeshProUGUI betText;

    [Header("画面とランプの演出（見た目と音だけ。当たり外れには関係しない）")]
    [SerializeField] private SlotScreen screen;

    [Header("演出の長さ（秒）。サーバーも同じ長さだけ、配当と次の回を待つ")]
    [Tooltip("回りはじめてから、左のリールが止まるまで")]
    [SerializeField] private float spinDuration = 0.8f;
    [Tooltip("左 → 中、中 → 右の間")]
    [SerializeField] private float reelStopDelay = 0.28f;
    [Tooltip("リーチ（左と中がそろったとき）：右のリールが止まるまでに足す秒数")]
    [SerializeField, Min(0f)] private float reachSeconds = 1.8f;
    [Tooltip("右のリールが止まってから、結果を出すまで")]
    [SerializeField] private float stopPulseDuration = 0.15f;

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

    // フィーバーが始まった回の賭け金（サーバーだけが持つ）。無料の回はこの賭け金で配当を決める。
    // 無料の回のあいだに賭け金を上げても、配当は増えない。
    private int feverBetCost;

    private Coroutine spinCoroutine;

    private void Start()
    {
        feverSpinCount.OnValueChanged += OnFeverChanged;
        if (resultText != null) resultText.text = "<color=#ADCAC7>READY TO PLAY</color>";
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
            "<size=65%><color=#ABC7C8>BET</color></size>  " +
            "<color=#F5CB76><b>" + CurrentBet + " R</b></color>";
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

        PlayerEarning requestingEarning =
            requestingPlayer.GetComponent<PlayerEarning>();

        if (SharedMoneyManager.Instance == null)
        {
            ShowResultClientRpc("口座なし");
            return;
        }

        bool isFeverSpin = feverSpinCount.Value > 0;

        // 無料の回は、フィーバーが始まった回の賭け金で配当を決める（手元の賭け金の切り替えは見ない）
        if (isFeverSpin && feverBetCost > 0)
        {
            cost = feverBetCost;
            multiplier = (float)cost / Mathf.Max(1, betAmounts[0]);
        }

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

        int tier = isMiss ? 0 : TierOf(r1);
        SpinClientRpc(r1, r2, r3, resultMessage, reward, tier);

        StartCoroutine(ApplyRewardAfterSpin(reward, addFever, playerNetworkObjectId, cost, isFeverSpin ? 0 : cost,
            GetSpinAnimationDuration(IsReach(r1, r2)), screen != null ? screen.CelebrateSeconds((SlotScreen.Tier)tier) : 0f));
    }

    /// <summary>
    /// 賭け金に対して戻る割合の期待値（1 = 100%）。当たりの重み・配当・フィーバーから計算する。
    /// 数値を直したときに、テストとインスペクターの右クリック（Log Expected Return）で確かめるためのもの。
    /// </summary>
    public float ExpectedReturn()
    {
        if (betAmounts == null || betAmounts.Length == 0 || slotSymbols == null) return 0f;

        float totalWeight = 0f, rewardSum = 0f, feverWeight = 0f;
        foreach (SlotSymbol s in slotSymbols)
        {
            float w = Mathf.Max(0, s.weight);
            totalWeight += w;
            rewardSum += w * s.reward;
            if (s.isFeverSymbol) feverWeight += w;
        }
        if (totalWeight <= 0f) return 0f;

        float hit = 1f - missPercent / 100f;
        float basePay = hit * rewardSum / totalWeight;                 // 1回ぶん（いちばん低い賭け金あたり）
        float feverChance = hit * feverWeight / totalWeight;
        float chain = 1f - feverFreeSpins * feverChance;               // 無料の回でまた 7 が出るぶん
        float freeSpin = chain > 0.001f ? feverMultiplier * basePay / chain : 0f;
        return (basePay + feverChance * feverFreeSpins * freeSpin) / Mathf.Max(1, betAmounts[0]);
    }

    [ContextMenu("Log Expected Return")]
    private void LogExpectedReturn() =>
        Debug.Log($"[Slot] {name} 戻る割合の期待値 {ExpectedReturn() * 100f:F1}%", this);

    /// <summary>Server validation shared with the editor regression check.</summary>
    public bool IsWithinInteractionRange(Vector3 playerPosition)
    {
        if (!float.IsFinite(playerPosition.x) || !float.IsFinite(playerPosition.y) || !float.IsFinite(playerPosition.z))
            return false;
        if (interactionCollider == null)
        {
            foreach (var candidate in GetComponentsInChildren<Collider>())
                if (candidate.GetComponent<Renderer>() != null && !candidate.isTrigger)
                { interactionCollider = candidate; break; }
        }
        if (interactionCollider == null || !interactionCollider.enabled || !interactionCollider.gameObject.activeInHierarchy)
            return false;
        Vector3 closest = interactionCollider.ClosestPoint(playerPosition);
        return (closest - playerPosition).sqrMagnitude <= serverInteractDistance * serverInteractDistance;
    }

    /// <summary>当たりの演出の段（1 小・2 中・3 大・4 = 7 ぞろい）。配当の低い絵から順に並んでいる前提で、並びの位置で決める。</summary>
    private int TierOf(int symbolIndex)
    {
        if (slotSymbols[symbolIndex].isFeverSymbol) return (int)SlotScreen.Tier.Jackpot;
        float k = slotSymbols.Length > 1 ? (float)symbolIndex / (slotSymbols.Length - 1) : 0f;
        return k < 0.4f ? (int)SlotScreen.Tier.Small : k < 0.7f ? (int)SlotScreen.Tier.Mid : (int)SlotScreen.Tier.Big;
    }

    /// <summary>
    /// リーチにするか：左と中がそろい、その絵が「中」以上の当たりの絵のとき。
    /// 小さい当たりの絵（配当の低い 2 つ）は、そろっても普通の速さで止める（毎回じらすと遊ぶテンポが落ちるため）。
    /// </summary>
    private bool IsReach(int r1, int r2)
    {
        return r1 == r2 && TierOf(r1) >= (int)SlotScreen.Tier.Mid;
    }

    /// <summary>回りはじめてから結果が出るまでの秒数（リーチのときは長い）。サーバーと各自の画面が同じ式を使う。</summary>
    private float GetSpinAnimationDuration(bool reach)
    {
        return spinDuration + reelStopDelay * 2f + (reach ? reachSeconds : 0f) + stopPulseDuration;
    }

    private IEnumerator ApplyRewardAfterSpin(int reward, bool addFever, ulong playerNetworkObjectId, int cost, int paid, float spinSeconds, float celebrateSeconds)
    {
        yield return new WaitForSeconds(spinSeconds);

        if (reward > 0)
        {
            // 共同口座へ
            bool credited = SharedMoneyManager.Instance != null &&
                            SharedMoneyManager.Instance.TryAdd(
                                reward,
                                SharedMoneyReason.SlotReward,
                                $"playerObject={playerNetworkObjectId}",
                                paid);   // 払った賭け金（無料の回は 0）。おねがいは差だけを数える

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
            // フィーバー中にまた 7 が出たときは、最初の賭け金のまま回数だけ足す
            if (feverSpinCount.Value <= 0) feverBetCost = cost;
            feverSpinCount.Value += feverFreeSpins;
        }

        // 当たりの演出が終わるまで、次の回を待つ
        if (celebrateSeconds > 0f) yield return new WaitForSeconds(celebrateSeconds);

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
    private void SpinClientRpc(int r1, int r2, int r3, string resultMessage, int reward, int tier)
    {
        if (spinCoroutine != null)
        {
            StopCoroutine(spinCoroutine);
        }

        spinCoroutine = StartCoroutine(
            SpinRoutine(r1, r2, r3, resultMessage, reward, tier)
        );
    }

    /// <summary>各自の画面：リールと演出は SlotScreen に任せ、ここでは下の文字（結果・フィーバー）だけを合わせる。</summary>
    private IEnumerator SpinRoutine(int r1, int r2, int r3, string resultMessage, int reward, int tier)
    {
        bool reach = IsReach(r1, r2);
        if (resultText != null)
        {
            resultText.text = "<color=#FFD34E>GOOD LUCK!</color>";
            resultText.color = Color.white;
        }

        float[] stopTimes =
        {
            spinDuration,
            spinDuration + reelStopDelay,
            spinDuration + reelStopDelay * 2f + (reach ? reachSeconds : 0f)
        };
        if (screen != null) screen.Play(r1, r2, r3, stopTimes, reach, (SlotScreen.Tier)tier, reward);

        if (reach)
        {
            yield return new WaitForSeconds(stopTimes[1]);
            if (resultText != null) resultText.text = "<color=#FF8A3D>REACH!</color>";
            yield return new WaitForSeconds(stopTimes[2] - stopTimes[1]);
        }
        else yield return new WaitForSeconds(stopTimes[2]);

        if (resultText != null)
        {
            resultText.text = resultMessage;

            if (resultMessage.Contains("FEVER WIN") || resultMessage.StartsWith("FEVER +"))
            {
                resultText.color = new Color(1f, 0.8f, 0.2f);
            }
            else if (resultMessage.Contains("WIN"))
            {
                resultText.color = new Color(0.55f, 1f, 0.6f);
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

    private void OnFeverChanged(int oldValue, int newValue)
    {
        UpdateFeverUI();
    }

    private Coroutine feverPulseCoroutine;

    private void UpdateFeverUI()
    {
        if (screen != null) screen.SetFever(feverSpinCount.Value > 0);
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
