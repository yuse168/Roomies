#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Steamworks;
using Netcode.Transports;

public class NetworkDebugOverlay : MonoBehaviour
{
    private const string GameSceneName = "GameRoom";
#if ENABLE_LEGACY_INPUT_MANAGER
    private const KeyCode ToggleKey = KeyCode.F1;
#endif

    private static NetworkDebugOverlay instance;

    private readonly StringBuilder statusBuilder = new StringBuilder(512);
    private const float WindowMargin = 12f;
    private const float WindowWidth = 320f;
    private const float WindowHeight = 260f;
    private const int WindowId = 240731;

    private Rect windowRect = new Rect(0f, WindowMargin, WindowWidth, WindowHeight);
    private bool visible = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;

        var go = new GameObject(nameof(NetworkDebugOverlay));
        instance = go.AddComponent<NetworkDebugOverlay>();
        DontDestroyOnLoad(go);
    }

    private void Update()
    {
        if (IsTogglePressed())
        {
            visible = !visible;
        }
    }

    private void OnGUI()
    {
        if (!visible) return;

        windowRect.x = Screen.width - WindowWidth - WindowMargin;
        windowRect.y = WindowMargin;
        windowRect = GUI.Window(WindowId, windowRect, DrawWindow, "Network Debug");
    }

    private void DrawWindow(int windowId)
    {
        var networkManager = NetworkManager.Singleton;

        DrawStatus(networkManager);

        GUI.enabled = networkManager != null && !networkManager.IsListening;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Host")) StartHost(networkManager);
        if (GUILayout.Button("Client")) StartClient(networkManager);
        if (GUILayout.Button("Server")) StartServer(networkManager);
        GUILayout.EndHorizontal();

        GUI.enabled = networkManager != null && networkManager.IsListening;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Load GameRoom")) LoadGameRoom(networkManager);
        if (GUILayout.Button("Shutdown")) Shutdown(networkManager);
        GUILayout.EndHorizontal();

        GUI.enabled = true;
        if (GUILayout.Button("Copy Status"))
        {
            GUIUtility.systemCopyBuffer = statusBuilder.ToString();
        }

        GUILayout.Label("F1: toggle");
        GUI.DragWindow();
    }

    private void DrawStatus(NetworkManager networkManager)
    {
        statusBuilder.Clear();
        statusBuilder.AppendLine($"Scene: {SceneManager.GetActiveScene().name}");

        if (networkManager == null)
        {
            statusBuilder.AppendLine("NetworkManager: missing");
            GUILayout.Label(statusBuilder.ToString());
            return;
        }

        statusBuilder.AppendLine($"Listening: {networkManager.IsListening}");
        statusBuilder.AppendLine($"Connected: {networkManager.IsConnectedClient}");
        statusBuilder.AppendLine($"Mode: {GetMode(networkManager)}");
        statusBuilder.AppendLine($"LocalClientId: {networkManager.LocalClientId}");
        statusBuilder.AppendLine($"ConnectedClients: {networkManager.ConnectedClientsIds.Count}");
        AppendTransportStatus(networkManager);
        AppendSteamStatus();

        if (networkManager.LocalClient != null)
        {
            string playerState = networkManager.LocalClient.PlayerObject != null
                ? networkManager.LocalClient.PlayerObject.name
                : "none";
            statusBuilder.AppendLine($"LocalPlayer: {playerState}");
        }

        GUILayout.Label(statusBuilder.ToString());
    }

    private void AppendTransportStatus(NetworkManager networkManager)
    {
        if (networkManager.NetworkConfig.NetworkTransport is SteamNetworkingSocketsTransport steamTransport)
        {
            statusBuilder.AppendLine($"SteamTarget: {steamTransport.ConnectToSteamID}");
            return;
        }

        statusBuilder.AppendLine(
            $"Transport: {networkManager.NetworkConfig.NetworkTransport.GetType().Name}"
        );
    }

    private void AppendSteamStatus()
    {
        if (!SteamManager.Initialized)
        {
            statusBuilder.AppendLine("Steam: not initialized");
            return;
        }

        string personaName = SteamLobby.Instance != null
            ? SteamLobby.Instance.LocalPersonaName
            : SteamFriends.GetPersonaName();

        statusBuilder.AppendLine($"SteamName: {personaName}");
        statusBuilder.AppendLine($"SteamID: {SteamUser.GetSteamID()}");
    }

    private static string GetMode(NetworkManager networkManager)
    {
        if (networkManager.IsHost) return "Host";
        if (networkManager.IsServer) return "Server";
        if (networkManager.IsClient) return "Client";
        return "Offline";
    }

    private static bool IsTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current?.f1Key.wasPressedThisFrame == true)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(ToggleKey))
        {
            return true;
        }
#endif

        return false;
    }

    private static void StartHost(NetworkManager networkManager)
    {
        if (networkManager == null || networkManager.IsListening) return;

        bool started = networkManager.StartHost();
        Debug.Log($"[NetworkDebugOverlay] StartHost: {started}");
    }

    private static void StartClient(NetworkManager networkManager)
    {
        if (networkManager == null || networkManager.IsListening) return;

        bool started = networkManager.StartClient();
        Debug.Log($"[NetworkDebugOverlay] StartClient: {started}");
    }

    private static void StartServer(NetworkManager networkManager)
    {
        if (networkManager == null || networkManager.IsListening) return;

        bool started = networkManager.StartServer();
        Debug.Log($"[NetworkDebugOverlay] StartServer: {started}");
    }

    private static void LoadGameRoom(NetworkManager networkManager)
    {
        if (networkManager == null || !networkManager.IsServer) return;

        networkManager.SceneManager.LoadScene(GameSceneName, LoadSceneMode.Single);
        Debug.Log($"[NetworkDebugOverlay] LoadScene: {GameSceneName}");
    }

    private static void Shutdown(NetworkManager networkManager)
    {
        if (networkManager == null || !networkManager.IsListening) return;

        networkManager.Shutdown();
        Debug.Log("[NetworkDebugOverlay] Shutdown");
    }
}
#endif
