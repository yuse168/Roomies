using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class DayManager : NetworkBehaviour
{
    [Header("Day設定")]
    [SerializeField] private int maxDay = 3;

    [Header("家賃設定")]
    [SerializeField] private int rentAmount = 500;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI dayText;

    private NetworkVariable<int> currentDay = new NetworkVariable<int>(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // 0 = 朝, 1 = 夜
    private NetworkVariable<int> currentTime = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<bool> isGameOver = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Start()
    {
        currentDay.OnValueChanged += OnDayChanged;
        currentTime.OnValueChanged += OnTimeChanged;
        isGameOver.OnValueChanged += OnGameOverChanged;

        UpdateDayUI();
    }

    private void OnDestroy()
    {
        currentDay.OnValueChanged -= OnDayChanged;
        currentTime.OnValueChanged -= OnTimeChanged;
        isGameOver.OnValueChanged -= OnGameOverChanged;
    }

    private void Update()
    {
        if (!IsHost) return;
        if (isGameOver.Value) return;

        if (Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame)
        {
            NextTimeServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void NextTimeServerRpc()
    {
        if (isGameOver.Value) return;

        if (currentTime.Value == 0)
        {
            currentTime.Value = 1;
            return;
        }

        if (currentDay.Value < maxDay)
        {
            currentDay.Value++;
            currentTime.Value = 0;
        }
        else
        {
            bool rentPaid = ChargeRent();

            if (!rentPaid)
            {
                GameOver();
                return;
            }

            currentDay.Value = 1;
            currentTime.Value = 0;
        }
    }

    private bool ChargeRent()
    {
        if (!IsServer) return false;

        if (SharedMoneyManager.Instance == null)
        {
            Debug.Log("SharedMoneyManagerがありません");
            return false;
        }

        if (SharedMoneyManager.Instance.CanPay(rentAmount))
        {
            SharedMoneyManager.Instance.PayRent(rentAmount);
            Debug.Log("家賃支払い成功");
            return true;
        }

        Debug.Log("共同口座のお金が足りない！");
        return false;
    }

    private void GameOver()
    {
        if (!IsServer) return;

        isGameOver.Value = true;
        Debug.Log("GAME OVER");
    }

    private void OnDayChanged(int oldDay, int newDay)
    {
        UpdateDayUI();
    }

    private void OnTimeChanged(int oldTime, int newTime)
    {
        UpdateDayUI();
    }

    private void OnGameOverChanged(bool oldValue, bool newValue)
    {
        UpdateDayUI();
    }

    private void UpdateDayUI()
    {
        if (dayText == null) return;

        if (isGameOver.Value)
        {
            dayText.text = "GAME OVER";
            return;
        }

        string timeText = currentTime.Value == 0 ? "朝" : "夜";
        dayText.text = "DAY " + currentDay.Value + " " + timeText;
    }
}