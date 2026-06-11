using System;
using System.Collections;
using Netcode.Transports;
using Steamworks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkSessionManager : MonoBehaviour
{
    private const float RelayTimeoutSeconds = 10f;
    private const float ConnectionTimeoutSeconds = 30f;
    private const float ClientSceneSyncTimeoutSeconds = 15f;

    // Steam側の初回接続タイムアウト。NGO側のConnectionTimeoutSecondsより短くして、
    // 失敗時はSteamのEndReason/EndDebugが先にログへ出るようにする。
    private const int SteamInitialConnectTimeoutMs = 25000;
    private const float SteamP2PActivityCheckSeconds = 6f;

    private static NetworkSessionManager instance;

    private Coroutine connectionTimeoutCoroutine;
    private Coroutine clientSceneSyncGuardCoroutine;
    private NetworkManager sessionNetworkManager;
    private Callback<SteamNetConnectionStatusChangedCallback_t>
        steamConnectionStatusChanged;
    private double lastSteamP2PEventRealtime = -1d;
    private int maxMembers = 4;
    private string gameSceneName = "GameRoom";
    private string menuSceneName = "MainMenuSteam";

    public NetworkSessionState State { get; private set; } =
        NetworkSessionState.Idle;

    public bool IsBusy =>
        State == NetworkSessionState.CreatingLobby ||
        State == NetworkSessionState.StartingHost ||
        State == NetworkSessionState.Connecting ||
        State == NetworkSessionState.Disconnecting ||
        State == NetworkSessionState.Reconnecting ||
        (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening);

    public event Action<NetworkSessionState> StateChanged;

    public static NetworkSessionManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<NetworkSessionManager>();
            }

            if (instance == null)
            {
                var go = new GameObject(nameof(NetworkSessionManager));
                instance = go.AddComponent<NetworkSessionManager>();
                DontDestroyOnLoad(go);
            }

            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded -= OnUnitySceneLoaded;
        SceneManager.sceneLoaded += OnUnitySceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnUnitySceneLoaded;
        UnsubscribeNetworkCallbacks();
    }

    public void SetState(NetworkSessionState nextState)
    {
        if (State == nextState)
        {
            return;
        }

        State = nextState;
        StateChanged?.Invoke(State);
        Debug.Log("[NetworkSessionManager] State=" + State);
    }

    public void MarkLobbyReady()
    {
        SetState(NetworkSessionState.InLobby);
    }

    public bool StartSteamHost(
        int maxMembers,
        string gameSceneName,
        string menuSceneName,
        Action onHostStarted
    )
    {
        if (State == NetworkSessionState.StartingHost ||
            State == NetworkSessionState.Connecting)
        {
            Debug.LogWarning("[NetworkSessionManager] セッション開始中のためHost開始を無視します。");
            return false;
        }

        this.maxMembers = maxMembers;
        this.gameSceneName = gameSceneName;
        this.menuSceneName = menuSceneName;

        StartCoroutine(StartSteamHostRoutine(onHostStarted));
        return true;
    }

    public bool StartSteamClient(
        ulong hostSteamId,
        string gameSceneName,
        string menuSceneName
    )
    {
        if (State == NetworkSessionState.StartingHost ||
            State == NetworkSessionState.Connecting)
        {
            Debug.LogWarning("[NetworkSessionManager] セッション開始中のためClient開始を無視します。");
            return false;
        }

        this.gameSceneName = gameSceneName;
        this.menuSceneName = menuSceneName;

        StartCoroutine(StartSteamClientRoutine(hostSteamId));
        return true;
    }

    public void ShutdownToMenu()
    {
        StartCoroutine(ShutdownToMenuRoutine());
    }

    private IEnumerator StartSteamHostRoutine(Action onHostStarted)
    {
        SetState(NetworkSessionState.StartingHost);

        yield return WaitForRelayNetwork("ホスト側");
        EnsureSteamConnectionDiagnostics();

        NetworkManager networkManager = NetworkManager.Singleton;
        if (!PrepareSteamNetworkManager(networkManager))
        {
            SetState(NetworkSessionState.InLobby);
            yield break;
        }

        ConfigureSteamTransportOptions(
            (SteamNetworkingSocketsTransport)
            networkManager.NetworkConfig.NetworkTransport
        );

        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.ConnectionApprovalCallback = ApprovalCheck;

        LogSteamHostSnapshot(networkManager, "StartHost直前");

        bool started = networkManager.StartHost();
        Debug.Log("[NetworkSessionManager] StartHost: " + started);
        LogSteamHostSnapshot(networkManager, "StartHost直後");

        if (!started)
        {
            SetState(NetworkSessionState.InLobby);
            yield break;
        }

        SetSessionNetworkManager(networkManager);
        SubscribeNetworkCallbacks();

        onHostStarted?.Invoke();

        SceneEventProgressStatus sceneLoadStatus =
            networkManager.SceneManager.LoadScene(
                gameSceneName,
                LoadSceneMode.Single
            );

        Debug.Log(
            "[NetworkSessionManager] Host LoadScene " +
            gameSceneName +
            ": " +
            sceneLoadStatus
        );

        if (sceneLoadStatus != SceneEventProgressStatus.Started)
        {
            Debug.LogError(
                "[NetworkSessionManager] Host LoadScene failed: " +
                sceneLoadStatus
            );
            SetState(NetworkSessionState.InLobby);
        }
    }

    private IEnumerator StartSteamClientRoutine(ulong hostSteamId)
    {
        SetState(NetworkSessionState.Connecting);

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            SetState(NetworkSessionState.InLobby);
            yield break;
        }

        if (networkManager.IsListening)
        {
            Debug.LogWarning("[NetworkSessionManager] NetworkManagerが既に起動中のためShutdownします。");
            networkManager.Shutdown();
            yield return new WaitForSeconds(0.2f);
        }

        yield return WaitForRelayNetwork("クライアント側");
        EnsureSteamConnectionDiagnostics();

        if (!PrepareSteamNetworkManager(networkManager))
        {
            SetState(NetworkSessionState.InLobby);
            yield break;
        }

        var transport =
            (SteamNetworkingSocketsTransport)
            networkManager.NetworkConfig.NetworkTransport;
        ConfigureSteamTransportOptions(transport);
        transport.ConnectToSteamID = hostSteamId;

        // NGOは接続要求時にNetworkConfigのハッシュを照合する。
        // ConnectionApprovalはハッシュ対象のため、ホスト側(true)と一致させないと
        // 接続直後にサーバーから切断される。
        networkManager.NetworkConfig.ConnectionApproval = true;

        LogSteamClientTarget(networkManager, transport, hostSteamId);

        SubscribeNetworkCallbacks();

        bool started = networkManager.StartClient();
        Debug.Log("[NetworkSessionManager] StartClient: " + started);

        if (!started)
        {
            UnsubscribeNetworkCallbacks();
            SetState(NetworkSessionState.InLobby);
            yield break;
        }

        SetSessionNetworkManager(networkManager);
        StartCoroutine(WarnIfNoSteamP2PActivity(SteamP2PActivityCheckSeconds));
        connectionTimeoutCoroutine =
            StartCoroutine(ConnectionTimeout(ConnectionTimeoutSeconds));
    }

    /// <summary>
    /// Transportに渡すSteamNetworkingのオプションを正常な内容で上書きする。
    /// Inspector上で無効な要素(m_eValue=0)が混入していると、Steam APIは
    /// ConnectP2P / CreateListenSocketP2P を黙って失敗させるため、
    /// コールバックが一切発生しないままタイムアウトする。
    /// </summary>
    private void ConfigureSteamTransportOptions(
        SteamNetworkingSocketsTransport transport
    )
    {
        if (transport == null)
        {
            return;
        }

        if (transport.options != null)
        {
            foreach (SteamNetworkingConfigValue_t option in transport.options)
            {
                if (option.m_eValue ==
                    ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_Invalid)
                {
                    Debug.LogWarning(
                        "[NetworkSessionManager] Transportのoptionsに無効な設定" +
                        "(k_ESteamNetworkingConfig_Invalid)が含まれていたため上書きします。" +
                        "シーン/プレハブのSteamNetworkingSocketsTransportのoptionsを確認してください。"
                    );
                    break;
                }
            }
        }

        var initialTimeout = new SteamNetworkingConfigValue_t
        {
            m_eValue =
                ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutInitial,
            m_eDataType =
                ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
            m_val = new SteamNetworkingConfigValue_t.OptionValue
            {
                m_int32 = SteamInitialConnectTimeoutMs
            }
        };

        transport.options = new[] { initialTimeout };
    }

    /// <summary>
    /// StartClient後、Steam P2Pの状態変化が一定時間まったく観測できない場合に警告する。
    /// この状態はConnectP2Pが接続自体を作成できていないことを意味する
    /// (不正なoptions・Steam未初期化・リレー未準備など)。
    /// </summary>
    private IEnumerator WarnIfNoSteamP2PActivity(float seconds)
    {
        double startTime = Time.realtimeSinceStartupAsDouble;
        yield return new WaitForSeconds(seconds);

        if (State != NetworkSessionState.Connecting)
        {
            yield break;
        }

        if (lastSteamP2PEventRealtime >= startTime)
        {
            yield break;
        }

        Debug.LogError(
            "[NetworkSessionManager] StartClientから" + seconds +
            "秒間、Steam P2Pの状態変化が一度も発生していません。" +
            "ConnectP2Pが接続を作成できていない可能性が高いです。" +
            "Transportのoptions設定とSteamの初期化状態を確認してください。"
        );
    }

    private IEnumerator WaitForRelayNetwork(string label)
    {
        float elapsed = 0f;

        while (elapsed < RelayTimeoutSeconds)
        {
            SteamRelayNetworkStatus_t status;
            ESteamNetworkingAvailability availability =
                SteamNetworkingUtils.GetRelayNetworkStatus(out status);

            if (availability ==
                ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Current)
            {
                Debug.Log("[NetworkSessionManager] " + label + "Steamリレー準備完了");
                yield break;
            }

            if (availability ==
                ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Failed)
            {
                Debug.LogWarning(
                    "[NetworkSessionManager] " + label +
                    "Steamリレー初期化失敗。接続処理は継続します。"
                );
                yield break;
            }

            Debug.Log(
                "[NetworkSessionManager] " + label +
                "Steamリレー待機中... " + availability
            );

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private bool PrepareSteamNetworkManager(NetworkManager networkManager)
    {
        if (networkManager == null)
        {
            Debug.LogError("[NetworkSessionManager] NetworkManagerがありません。");
            return false;
        }

        if (!(networkManager.NetworkConfig.NetworkTransport is SteamNetworkingSocketsTransport))
        {
            string transportName = networkManager.NetworkConfig.NetworkTransport != null
                ? networkManager.NetworkConfig.NetworkTransport.GetType().Name
                : "(none)";

            Debug.LogError(
                "[NetworkSessionManager] Steam接続にはSteamNetworkingSocketsTransportが必要です。現在のTransport=" +
                transportName
            );
            return false;
        }

        return true;
    }

    private void LogSteamHostSnapshot(NetworkManager networkManager, string label)
    {
        ulong localSteamId = SteamManager.Initialized
            ? SteamUser.GetSteamID().m_SteamID
            : 0;
        ulong lobbyOwnerSteamId = GetCurrentLobbyOwnerSteamId();

        Debug.Log(
            "[NetworkSessionManager] Host状態 " + label +
            " LocalSteamID=" + localSteamId +
            " LobbyOwnerSteamID=" + lobbyOwnerSteamId +
            " IsHost=" + (networkManager != null && networkManager.IsHost) +
            " IsServer=" + (networkManager != null && networkManager.IsServer) +
            " IsListening=" + (networkManager != null && networkManager.IsListening)
        );
    }

    private void LogSteamClientTarget(
        NetworkManager networkManager,
        SteamNetworkingSocketsTransport transport,
        ulong hostSteamId
    )
    {
        ulong localSteamId = SteamManager.Initialized
            ? SteamUser.GetSteamID().m_SteamID
            : 0;
        ulong lobbyOwnerSteamId = GetCurrentLobbyOwnerSteamId();
        ulong transportTargetSteamId = transport != null
            ? transport.ConnectToSteamID
            : 0;

        Debug.Log(
            "[NetworkSessionManager] StartClient直前 " +
            "LocalSteamID=" + localSteamId +
            " HostSteamID=" + hostSteamId +
            " LobbyOwnerSteamID=" + lobbyOwnerSteamId +
            " Transport.ConnectToSteamID(TargetSteamId)=" +
            transportTargetSteamId +
            " IsListening=" + (networkManager != null && networkManager.IsListening)
        );

        if (localSteamId != 0 && localSteamId == hostSteamId)
        {
            Debug.LogError(
                "[NetworkSessionManager] 接続先HostSteamIDが自分自身です。LobbyData/Ownerの取得を確認してください。"
            );
        }

        if (lobbyOwnerSteamId != 0 && lobbyOwnerSteamId != hostSteamId)
        {
            Debug.LogWarning(
                "[NetworkSessionManager] HostSteamIDとLobbyOwnerSteamIDが一致しません。HostAddressが最新か確認してください。"
            );
        }

        if (transportTargetSteamId != hostSteamId)
        {
            Debug.LogError(
                "[NetworkSessionManager] Transport.ConnectToSteamIDがHostSteamIDと一致していません。"
            );
        }
    }

    private void EnsureSteamConnectionDiagnostics()
    {
        if (!SteamManager.Initialized || steamConnectionStatusChanged != null)
        {
            return;
        }

        steamConnectionStatusChanged =
            Callback<SteamNetConnectionStatusChangedCallback_t>.Create(
                OnSteamConnectionStatusChanged
            );

        Debug.Log("[NetworkSessionManager] Steam P2P接続診断ログを開始しました。");
    }

    private void OnSteamConnectionStatusChanged(
        SteamNetConnectionStatusChangedCallback_t callback
    )
    {
        lastSteamP2PEventRealtime = Time.realtimeSinceStartupAsDouble;

        ulong localSteamId = SteamUser.GetSteamID().m_SteamID;
        ulong remoteSteamId =
            callback.m_info.m_identityRemote.GetSteamID64();

        Debug.Log(
            "[NetworkSessionManager] Steam P2P状態変更 " +
            "LocalSteamID=" + localSteamId +
            " RemoteSteamID=" + remoteSteamId +
            " OldState=" + callback.m_eOldState +
            " NewState=" + callback.m_info.m_eState +
            " EndReason=" + callback.m_info.m_eEndReason +
            " EndDebug=" + callback.m_info.m_szEndDebug +
            " Description=" + callback.m_info.m_szConnectionDescription
        );
    }

    private ulong GetCurrentLobbyOwnerSteamId()
    {
        SteamLobby lobby = SteamLobby.Instance;
        if (lobby == null || lobby.LobbyID == 0 || !SteamManager.Initialized)
        {
            return 0;
        }

        return SteamMatchmaking.GetLobbyOwner(new CSteamID(lobby.LobbyID))
            .m_SteamID;
    }

    private void SetSessionNetworkManager(NetworkManager networkManager)
    {
        if (networkManager == null)
        {
            return;
        }

        sessionNetworkManager = networkManager;
        DontDestroyOnLoad(networkManager.gameObject);
        RemoveSceneNetworkManagerDuplicates();
    }

    private void SubscribeNetworkCallbacks()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            return;
        }

        networkManager.OnClientConnectedCallback -= OnClientConnected;
        networkManager.OnClientConnectedCallback += OnClientConnected;

        networkManager.OnClientDisconnectCallback -= OnClientDisconnect;
        networkManager.OnClientDisconnectCallback += OnClientDisconnect;

        SubscribeNetworkSceneCallbacks(networkManager);
    }

    private void SubscribeNetworkSceneCallbacks(NetworkManager networkManager)
    {
        if (networkManager == null || networkManager.SceneManager == null)
        {
            return;
        }

        networkManager.SceneManager.OnSceneEvent -= OnNetworkSceneEvent;
        networkManager.SceneManager.OnSceneEvent += OnNetworkSceneEvent;
    }

    private void UnsubscribeNetworkCallbacks()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            return;
        }

        networkManager.OnClientConnectedCallback -= OnClientConnected;
        networkManager.OnClientDisconnectCallback -= OnClientDisconnect;
        if (networkManager.SceneManager != null)
        {
            networkManager.SceneManager.OnSceneEvent -= OnNetworkSceneEvent;
        }
    }

    private void OnNetworkSceneEvent(SceneEvent sceneEvent)
    {
        Debug.Log(
            "[NetworkSessionManager] SceneEvent: " +
            sceneEvent.SceneEventType +
            " Scene=" +
            sceneEvent.SceneName +
            " ClientID=" +
            sceneEvent.ClientId +
            " ActiveScene=" +
            SceneManager.GetActiveScene().name
        );

        if (sceneEvent.SceneName == gameSceneName &&
            SceneManager.GetActiveScene().name == gameSceneName)
        {
            SetState(NetworkSessionState.InGame);
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            return;
        }

        Debug.Log("[NetworkSessionManager] クライアント接続 ClientID=" + clientId);
        SubscribeNetworkSceneCallbacks(networkManager);
        Debug.Log(
            "[NetworkSessionManager] OnClientConnected状態 ClientID=" +
            clientId +
            " LocalClientID=" + networkManager.LocalClientId +
            " IsHost=" + networkManager.IsHost +
            " IsServer=" + networkManager.IsServer +
            " IsClient=" + networkManager.IsClient +
            " IsListening=" + networkManager.IsListening +
            " ConnectedClients=" + networkManager.ConnectedClients.Count
        );

        if (clientId != networkManager.LocalClientId)
        {
            return;
        }

        if (connectionTimeoutCoroutine != null)
        {
            StopCoroutine(connectionTimeoutCoroutine);
            connectionTimeoutCoroutine = null;
        }

        if (clientSceneSyncGuardCoroutine != null)
        {
            StopCoroutine(clientSceneSyncGuardCoroutine);
        }

        clientSceneSyncGuardCoroutine =
            StartCoroutine(ClientSceneSyncGuard(ClientSceneSyncTimeoutSeconds));
    }

    private void OnClientDisconnect(ulong clientId)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        string reason = networkManager != null
            ? networkManager.DisconnectReason
            : string.Empty;

        Debug.LogWarning(
            "[NetworkSessionManager] クライアント切断 ClientID=" +
            clientId +
            " 理由=" +
            (string.IsNullOrEmpty(reason) ? "(なし)" : reason) +
            " IsHost=" + (networkManager != null && networkManager.IsHost) +
            " IsServer=" + (networkManager != null && networkManager.IsServer) +
            " IsClient=" + (networkManager != null && networkManager.IsClient) +
            " IsListening=" + (networkManager != null && networkManager.IsListening)
        );

        if (networkManager != null &&
            networkManager.IsServer &&
            clientId != networkManager.LocalClientId)
        {
            return;
        }

        StartCoroutine(ShutdownToMenuRoutine());
    }

    private IEnumerator ClientSceneSyncGuard(float seconds)
    {
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            NetworkManager networkManager = NetworkManager.Singleton;

            if (SceneManager.GetActiveScene().name == gameSceneName)
            {
                SetState(NetworkSessionState.InGame);
                clientSceneSyncGuardCoroutine = null;
                yield break;
            }

            if (networkManager == null ||
                !networkManager.IsClient ||
                !networkManager.IsConnectedClient)
            {
                clientSceneSyncGuardCoroutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsClient &&
            NetworkManager.Singleton.IsConnectedClient &&
            SceneManager.GetActiveScene().name != gameSceneName)
        {
            Debug.LogWarning(
                "[NetworkSessionManager] シーン同期が完了しないため、クライアント側でGameRoomへ復旧遷移します。"
            );
            SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            SetState(NetworkSessionState.InGame);
        }

        clientSceneSyncGuardCoroutine = null;
    }

    private IEnumerator ConnectionTimeout(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsClient &&
            !NetworkManager.Singleton.IsConnectedClient)
        {
            Debug.LogError(
                "[NetworkSessionManager] 接続タイムアウト (" +
                seconds +
                "秒) — ホストに到達できませんでした。"
            );

            yield return ShutdownToMenuRoutine();
        }
    }

    private IEnumerator ShutdownToMenuRoutine()
    {
        SetState(NetworkSessionState.Disconnecting);
        UnsubscribeNetworkCallbacks();

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsListening)
        {
            networkManager.Shutdown();
            yield return new WaitForSeconds(0.2f);
        }

        sessionNetworkManager = null;
        SetState(NetworkSessionState.Idle);

        if (SceneManager.GetActiveScene().name != menuSceneName)
        {
            SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
        }
    }

    private void ApprovalCheck(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response
    )
    {
        int currentCount =
            NetworkManager.Singleton != null
                ? NetworkManager.Singleton.ConnectedClients.Count
                : 0;

        Debug.Log(
            "[NetworkSessionManager] ApprovalCheck: 現在の接続数=" +
            currentCount +
            " / 最大=" +
            maxMembers
        );

        response.Pending = true;

        if (currentCount >= maxMembers)
        {
            Debug.LogWarning("[NetworkSessionManager] ApprovalCheck: 満員のため拒否");
            response.Approved = false;
            response.Pending = false;
            return;
        }

        response.Approved = true;
        response.CreatePlayerObject = true;
        response.PlayerPrefabHash = null;
        response.Position = null;
        response.Rotation = null;
        response.Pending = false;
    }

    private void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RemoveSceneNetworkManagerDuplicates();
    }

    private void RemoveSceneNetworkManagerDuplicates()
    {
        if (sessionNetworkManager == null ||
            !sessionNetworkManager.IsListening)
        {
            return;
        }

        NetworkManager[] networkManagers =
            FindObjectsByType<NetworkManager>(FindObjectsInactive.Exclude);

        foreach (NetworkManager networkManager in networkManagers)
        {
            if (networkManager == null ||
                networkManager == sessionNetworkManager)
            {
                continue;
            }

            Debug.LogWarning(
                "[NetworkSessionManager] セッション中の重複NetworkManagerを破棄します: " +
                networkManager.gameObject.scene.name +
                "/" +
                networkManager.name
            );
            Destroy(networkManager.gameObject);
        }
    }
}
