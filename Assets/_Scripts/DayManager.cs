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

    private void Start()
    {
        UpdateDayUI(currentDay.Value);

        currentDay.OnValueChanged += OnDayChanged;
    }

    private void OnDestroy()
    {
        currentDay.OnValueChanged -= OnDayChanged;
    }

    private void Update()
    {
        if (!IsHost) return;

        // テスト用
        // Nキーで次の日へ
        if (Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame)
        {
            NextDayServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void NextDayServerRpc()
    {
        // まだ3日以内なら普通に進む
        if (currentDay.Value < maxDay)
        {
            currentDay.Value++;

            Debug.Log("DAY " + currentDay.Value);
        }
        else
        {
            // 3日終了時
            Debug.Log("3日終了！");

            ChargeRent();

            // 次のサイクルへ戻す
            currentDay.Value = 1;

            Debug.Log("新しい3日サイクル開始");
        }
    }

    // 家賃徴収
    private void ChargeRent()
    {
        PlayerMoney[] players = FindObjectsByType<PlayerMoney>(FindObjectsSortMode.None);

        foreach (PlayerMoney player in players)
        {
            // 支払える場合
            if (player.CanPay(rentAmount))
            {
                player.PayRent(rentAmount);

                Debug.Log("家賃支払い成功");
            }
            else
            {
                Debug.Log("家賃が払えない！");
            }
        }
    }

    private void OnDayChanged(int oldDay, int newDay)
    {
        UpdateDayUI(newDay);
    }

    private void UpdateDayUI(int day)
    {
        if (dayText == null) return;

        dayText.text = "DAY " + day;
    }
}