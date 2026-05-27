using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using Steamworks;
using TMPro;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("Common")]
    public Camera menuCamera;

    [Header("Steam Lobby")]
    [SerializeField] private bool useSteamLobby;
    [SerializeField] private TMP_InputField lobbyIdInput;
    [SerializeField] private Button joinButton;

    private void OnEnable()
    {
        if (SteamLobby.Instance != null)
        {
            SteamLobby.Instance.BusyStateChanged += OnSteamLobbyBusyStateChanged;
            UpdateJoinButton();
        }
    }

    private void OnDisable()
    {
        if (SteamLobby.Instance != null)
        {
            SteamLobby.Instance.BusyStateChanged -= OnSteamLobbyBusyStateChanged;
        }
    }

    public void Host()
    {
        if (menuCamera != null)
        {
            Destroy(menuCamera.gameObject);
        }

        if (useSteamLobby)
        {
            SteamLobby.Instance.CreateLobby();
            return;
        }

        NetworkManager.Singleton.StartHost();

        NetworkManager.Singleton.SceneManager.LoadScene("GameRoom", LoadSceneMode.Single);
    }

    public void Join()
    {
        if (useSteamLobby)
        {
            if (SteamLobby.Instance != null && SteamLobby.Instance.IsBusy)
            {
                Debug.LogWarning("[MainMenuManager] Join ignored because lobby operation is already running.");
                return;
            }

            JoinSteamLobby();
            return;
        }

        if (NetworkManager.Singleton.IsListening)
        {
            Debug.LogWarning("[MainMenuManager] Join ignored because NetworkManager is already listening.");
            return;
        }

        NetworkManager.Singleton.StartClient();
    }

    private void JoinSteamLobby()
    {
        if (lobbyIdInput == null)
        {
            Debug.LogError("[MainMenuManager] Lobby ID Input is not assigned.");
            return;
        }

        string inputText = lobbyIdInput.text.Trim();

        if (string.IsNullOrWhiteSpace(inputText))
        {
            Debug.LogError("[MainMenuManager] Lobby ID/Code input is empty.");
            return;
        }

        //18桁のulong値（LobbyID）としてパース可能な場合は従来通りの直接接続を行う（デバッグ用）
        if (ulong.TryParse(inputText, out ulong lobbyId) && inputText.Length >= 10)
        {
            Debug.Log($"[MainMenuManager] Parsing input as raw LobbyID: {lobbyId}");
            SteamLobby.Instance.JoinLobby(new CSteamID(lobbyId));
        }
        else
        {
            //それ以外（5桁の英数字等）の場合は部屋コードによる検索接続を行う
            Debug.Log($"[MainMenuManager] Parsing input as Room Code: {inputText}");
            SteamLobby.Instance.JoinLobbyWithCode(inputText);
        }
    }

    private void OnSteamLobbyBusyStateChanged(bool isBusy)
    {
        UpdateJoinButton();
    }

    private void UpdateJoinButton()
    {
        if (joinButton != null && SteamLobby.Instance != null)
        {
            joinButton.interactable = !SteamLobby.Instance.IsBusy;
        }
    }

    public void Quit()
    {
        Application.Quit();
    }
}
