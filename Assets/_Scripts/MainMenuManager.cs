using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using Steamworks;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [Header("Common")]
    public Camera menuCamera;

    [Header("Steam Lobby")]
    [SerializeField] private bool useSteamLobby;
    [SerializeField] private TMP_InputField lobbyIdInput;

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
            JoinSteamLobby();
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

    public void Quit()
    {
        Application.Quit();
    }
}
