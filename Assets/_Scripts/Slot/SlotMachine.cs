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

    [Header("通常料金")]
    [SerializeField] private int playCost = 10;

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

    [Header("演出")]
    [SerializeField] private float spinDuration = 0.8f;
    [SerializeField] private float spinInterval = 0.05f;

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
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        feverSpinCount.OnValueChanged -= OnFeverChanged;
    }

    public void Interact(PlayerEarning playerEarning)
    {
        if (isSpinning.Value) return;

        PlaySlotServerRpc(playerEarning.NetworkObjectId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PlaySlotServerRpc(ulong playerNetworkObjectId)
    {
        if (isSpinning.Value) return;

        if (SharedMoneyManager.Instance == null)
        {
            ShowResultClientRpc("口座なし");
            return;
        }

        bool isFeverSpin = feverSpinCount.Value > 0;

        if (!isFeverSpin)
        {
            if (!SharedMoneyManager.Instance.CanPay(playCost))
            {
                ShowResultClientRpc("お金不足");
                return;
            }

            SharedMoneyManager.Instance.SpendSharedMoney(playCost);

            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects
                .TryGetValue(playerNetworkObjectId, out NetworkObject playerObj))
            {
                PlayerEarning earning = playerObj.GetComponent<PlayerEarning>();

                if (earning != null)
                {
                    earning.SpendEarning(playCost);
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

            reward = slotSymbols[winIndex].reward;

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

    private IEnumerator ApplyRewardAfterSpin(int reward, bool addFever)
    {
        yield return new WaitForSeconds(spinDuration);

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
        }

        float timer = 0f;

        while (timer < spinDuration)
        {
            SetReels(
                Random.Range(0, slotSymbols.Length),
                Random.Range(0, slotSymbols.Length),
                Random.Range(0, slotSymbols.Length)
            );

            timer += spinInterval;

            yield return new WaitForSeconds(spinInterval);
        }

        SetReels(r1, r2, r3);

        if (resultText != null)
        {
            resultText.text = resultMessage;
        }

        UpdateFeverUI();

        spinCoroutine = null;
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
        }
        else
        {
            feverText.text = "";
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