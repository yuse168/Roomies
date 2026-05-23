using TMPro;
using UnityEngine;

public class LobbyIdDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text lobbyIdText;

    private void Start()
    {
        if (lobbyIdText == null)
        {
            lobbyIdText = GetComponent<TMP_Text>();
        }

        if (lobbyIdText == null)
        {
            Debug.LogError("[LobbyIdDisplay] TMP_Text is not assigned.");
            return;
        }

        if (SteamLobby.Instance == null)
        {
            lobbyIdText.text = "Lobby ID: -";
            Debug.LogWarning("[LobbyIdDisplay] SteamLobby was not found.");
            return;
        }

        lobbyIdText.text = $"Lobby ID: {SteamLobby.Instance.LobbyID}";
    }
}
