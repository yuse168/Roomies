using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

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

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI reelText1;
    [SerializeField] private TextMeshProUGUI reelText2;
    [SerializeField] private TextMeshProUGUI reelText3;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private TextMeshProUGUI feverText;
    [SerializeField] private TextMeshProUGUI betText;

    [Header("演出")]
    [SerializeField] private float spinDuration = 0.8f;
    [SerializeField] private float spinInterval = 0.05f;
    [SerializeField] private float stopPulseScale = 1.2f;
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

    private Coroutine spinCoroutine;

    private void Start()
    {
        feverSpinCount.OnValueChanged += OnFeverChanged;
        UpdateFeverUI();
        UpdateBetUI();
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (isSpinning.Value) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            SetBetIndex(0);
        }
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            SetBetIndex(1);
        }
        else if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            SetBetIndex(2);
        }
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

        string betStr = "";

        for (int i = 0; i < betAmounts.Length; i++)
        {
            if (i == currentBetIndex)
            {
                betStr += "<color=yellow>【¥" + betAmounts[i] + "】</color> ";
            }
            else
            {
                betStr += "¥" + betAmounts[i] + " ";
            }
        }

        betText.text = betStr.TrimEnd();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        feverSpinCount.OnValueChanged -= OnFeverChanged;
    }

    public void Interact(PlayerEarning playerEarning)
    {
        if (isSpinning.Value) return;

        PlaySlotServerRpc(playerEarning.NetworkObjectId, currentBetIndex);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PlaySlotServerRpc(ulong playerNetworkObjectId, int betIndex)
    {
        if (isSpinning.Value) return;

        betIndex = Mathf.Clamp(betIndex, 0, betAmounts.Length - 1);
        int cost = betAmounts[betIndex];
        float multiplier = (float)cost / betAmounts[0];

        if (SharedMoneyManager.Instance == null)
        {
            ShowResultClientRpc("口座なし");
            return;
        }

        bool isFeverSpin = feverSpinCount.Value > 0;

        if (!isFeverSpin)
        {
            if (!SharedMoneyManager.Instance.CanPay(cost))
            {
                ShowResultClientRpc("お金不足");
                return;
            }

            SharedMoneyManager.Instance.SpendSharedMoney(cost);

            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects
                .TryGetValue(playerNetworkObjectId, out NetworkObject playerObj))
            {
                PlayerEarning earning = playerObj.GetComponent<PlayerEarning>();

                if (earning != null)
                {
                    earning.SpendEarning(cost);
                }
            }
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

        StartCoroutine(ApplyRewardAfterSpin(reward, addFever));
    }

    private float GetSpinAnimationDuration()
    {
        float total = 0f;
        int steps = Mathf.CeilToInt(spinDuration / spinInterval);

        for (int i = 0; i < steps; i++)
        {
            float t = (float)i / steps;
            total += Mathf.Lerp(spinInterval, spinInterval * 5f, t * t);
        }

        return total;
    }

    private IEnumerator ApplyRewardAfterSpin(int reward, bool addFever)
    {
        yield return new WaitForSeconds(GetSpinAnimationDuration());

        if (reward > 0 && SharedMoneyManager.Instance != null)
        {
            SharedMoneyManager.Instance.AddSharedMoney(reward);
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
            resultText.text = "SPIN...";
            resultText.color = Color.white;
        }

        float elapsed = 0f;

        while (elapsed < spinDuration)
        {
            SetReels(
                Random.Range(0, slotSymbols.Length),
                Random.Range(0, slotSymbols.Length),
                Random.Range(0, slotSymbols.Length)
            );

            elapsed += spinInterval;

            float t = elapsed / spinDuration;
            float delay = Mathf.Lerp(spinInterval, spinInterval * 5f, t * t);

            yield return new WaitForSeconds(delay);
        }

        SetReels(r1, r2, r3);
        StartCoroutine(StopPulse());

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

    private IEnumerator StopPulse()
    {
        Vector3 originalScale1 = reelText1 != null ? reelText1.transform.localScale : Vector3.one;
        Vector3 originalScale2 = reelText2 != null ? reelText2.transform.localScale : Vector3.one;
        Vector3 originalScale3 = reelText3 != null ? reelText3.transform.localScale : Vector3.one;

        Vector3 bigScale = originalScale1 * stopPulseScale;

        float half = stopPulseDuration / 2f;

        if (reelText1 != null) reelText1.transform.localScale = bigScale;
        if (reelText2 != null) reelText2.transform.localScale = bigScale;
        if (reelText3 != null) reelText3.transform.localScale = bigScale;

        yield return new WaitForSeconds(half);

        if (reelText1 != null) reelText1.transform.localScale = originalScale1;
        if (reelText2 != null) reelText2.transform.localScale = originalScale2;
        if (reelText3 != null) reelText3.transform.localScale = originalScale3;
    }

    private void SetReels(int r1, int r2, int r3)
    {
        if (reelText1 != null) reelText1.text = slotSymbols[r1].symbol;
        if (reelText2 != null) reelText2.text = slotSymbols[r2].symbol;
        if (reelText3 != null) reelText3.text = slotSymbols[r3].symbol;
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