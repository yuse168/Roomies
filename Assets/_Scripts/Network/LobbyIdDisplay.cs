using TMPro;
using UnityEngine;

/// <summary>
/// 現在のロビーコードをTMP_Textに表示する軽量コンポーネント。
/// LobbyUIManagerが存在しない場面でも単体で使用可能。
/// </summary>
public class LobbyIdDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text lobbyIdText;

    private void Awake()
    {
        if (lobbyIdText == null)
            lobbyIdText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        if (SteamLobby.Instance != null)
        {
            SteamLobby.Instance.OnLobbyReady  += Refresh;
            SteamLobby.Instance.OnLobbyJoined += Refresh;
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (SteamLobby.Instance != null)
        {
            SteamLobby.Instance.OnLobbyReady  -= Refresh;
            SteamLobby.Instance.OnLobbyJoined -= Refresh;
        }
    }

    private void Refresh()
    {
        if (lobbyIdText == null) return;
        string code = SteamLobby.Instance?.LobbyCode;
        lobbyIdText.text = string.IsNullOrEmpty(code) ? "Room Code: -" : $"Room Code: {code}";
    }
}
