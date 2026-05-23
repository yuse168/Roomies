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

        if (!ulong.TryParse(lobbyIdInput.text, out ulong lobbyId))
        {
            Debug.LogError($"[MainMenuManager] Invalid Lobby ID: {lobbyIdInput.text}");
            return;
        }

        SteamLobby.Instance.JoinLobby(new CSteamID(lobbyId));
    }

    public void Quit()
    {
        Application.Quit();
    }
}
