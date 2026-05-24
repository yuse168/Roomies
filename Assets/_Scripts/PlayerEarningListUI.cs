using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerEarningListUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI earningListText;

    private void Start()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        bool isTabPressed = Keyboard.current.tabKey.isPressed;

        if (panel != null)
        {
            panel.SetActive(isTabPressed);
        }

        if (isTabPressed)
        {
            UpdateEarningList();
        }
    }

    private void UpdateEarningList()
    {
        if (earningListText == null) return;

        PlayerEarning[] players = FindObjectsByType<PlayerEarning>(FindObjectsSortMode.None);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("個人の稼ぎ");

        foreach (PlayerEarning player in players)
        {
            string playerName = "Player";

            PlayerNameDisplay nameDisplay = player.GetComponent<PlayerNameDisplay>();
            if (nameDisplay != null)
            {
                playerName = nameDisplay.GetPlayerName();
            }

            sb.AppendLine(playerName + "　¥" + player.GetEarning().ToString("N0"));
        }

        earningListText.text = sb.ToString();
    }
}