using System;
using System.Collections;
using System.Text;
using Netcode.Transports;
using Steamworks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SteamLobby : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "GameRoom";
    [SerializeField] private string menuSceneName = "MainMenuSteam";
    [SerializeField] private int maxMembers = 4;

    // ロビー作成コールバック
    private CallResult<LobbyCreated_t> m_crLobbyCreated;

    // ロビー入室コールバック
    private Callback<LobbyEnter_t> m_lobbyEnter;

    // 招待参加コールバック
    private Callback<GameLobbyJoinRequested_t> m_gameLobbyJoinRequested;

    // ロビー検索コールバック
    private CallResult<LobbyMatchList_t> m_crLobbyMatchList;

    // ロビーチャットコールバック
    private Callback<LobbyChatMsg_t> m_lobbyChatMessage;

    // ロビーデータ更新コールバック
    private Callback<LobbyDataUpdate_t> m_lobbyDataUpdate;

    // ロビーデータキー
    private const string s_HostAddressKey = "HostAddress";
    private const string s_LobbyCodeKey = "LobbyCode";
    private const string s_GameStartedKey = "game_started";
    private const int s_LobbyChatMessageMaxBytes = 4096;

    public ulong LobbyID { get; private set; }
    public string LobbyCode { get; private set; }
    public string LocalPersonaName { get; private set; }
    public bool IsBusy => operationInProgress ||
        (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening);
    public event Action<bool> BusyStateChanged;

    // シングルトン
    private static SteamLobby instance;
    private bool operationInProgress;
    private Coroutine connectionTimeoutCoroutine;
    private Coroutine clientSceneSyncGuardCoroutine;
    private bool clientStartRequested;
    private NetworkManager sessionNetworkManager;

    public static SteamLobby Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<SteamLobby>();
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
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamLobby] SteamManagerが初期化されていません");
            return;
        }

        m_crLobbyCreated =
            CallResult<LobbyCreated_t>.Create(OnCreateLobby);

        m_lobbyEnter =
            Callback<LobbyEnter_t>.Create(OnLobbyEntered);

        m_gameLobbyJoinRequested =
            Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);

        m_crLobbyMatchList =
            CallResult<LobbyMatchList_t>.Create(OnLobbyMatchList);

        m_lobbyChatMessage =
            Callback<LobbyChatMsg_t>.Create(OnLobbyChatMessage);

        m_lobbyDataUpdate =
            Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdated);

        // Steamリレーネットワークを起動（ファイアウォール環境での接続に必要）
        SteamNetworkingUtils.InitRelayNetworkAccess();

        LocalPersonaName = SteamFriends.GetPersonaName();

        Debug.Log(
            "[SteamLobby] Steam初期化成功: " +
            LocalPersonaName + " (" +
            SteamUser.GetSteamID() + ")"
        );
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded -= OnUnitySceneLoaded;
        SceneManager.sceneLoaded += OnUnitySceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnUnitySceneLoaded;
    }

    /// <summary>
    /// ロビー作成
    /// </summary>
    public void CreateLobby()
    {
        if (IsBusy)
        {
            Debug.LogWarning("[SteamLobby] ロビー操作中のため作成を無視します");
            return;
        }

        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamLobby] Steam未初期化");
            return;
        }

        SetOperationInProgress(true);

        SteamAPICall_t handle =
            SteamMatchmaking.CreateLobby(
                ELobbyType.k_ELobbyTypePublic,
                maxMembers
            );

        m_crLobbyCreated.Set(handle);

        Debug.Log("[SteamLobby] ロビー作成開始");
    }

    /// <summary>
    /// ロビー作成完了
    /// </summary>
    private void OnCreateLobby(
        LobbyCreated_t callback,
        bool ioFailure
    )
    {
        if (ioFailure ||
            callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError(
                "[SteamLobby] ロビー作成失敗"
            );

            SetOperationInProgress(false);
            return;
        }

        LobbyID = callback.m_ulSteamIDLobby;

        Debug.Log(
            "[SteamLobby] ロビー作成成功: " +
            LobbyID
        );

        // 部屋コード生成
        LobbyCode = GenerateRoomCode();

        // ホストSteamID保存
        SteamMatchmaking.SetLobbyData(
            new CSteamID(LobbyID),
            s_HostAddressKey,
            SteamUser.GetSteamID().ToString()
        );

        // 部屋コード保存
        SteamMatchmaking.SetLobbyData(
            new CSteamID(LobbyID),
            s_LobbyCodeKey,
            LobbyCode
        );

        SteamMatchmaking.SetLobbyData(
            new CSteamID(LobbyID),
            s_GameStartedKey,
            "false"
        );

        Debug.Log(
            "[SteamLobby] 部屋コード: " +
            LobbyCode
        );

        var networkManager = NetworkManager.Singleton;
        if (!PrepareSteamNetworkManager(networkManager))
        {
            SetOperationInProgress(false);
            return;
        }

        // 接続承認
        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.ConnectionApprovalCallback = ApprovalCheck;

        // ホスト開始をコルーチンに変更して、Steamリレーネットワークの準備完了を待つ
        StartCoroutine(StartHostRoutine());
    }

    private IEnumerator StartHostRoutine()
    {
        // Steamリレーの準備完了を最大10秒待つ
        float elapsed = 0f;
        const float relayTimeout = 10f;

        while (elapsed < relayTimeout)
        {
            SteamRelayNetworkStatus_t status;
            ESteamNetworkingAvailability avail =
                SteamNetworkingUtils.GetRelayNetworkStatus(out status);

            if (avail == ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Current)
            {
                Debug.Log("[SteamLobby] ホスト側Steamリレー準備完了");
                break;
            }

            if (avail == ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Failed)
            {
                Debug.LogWarning("[SteamLobby] ホスト側Steamリレー初期化失敗 — 直接バインドで試みます");
                break;
            }

            Debug.Log("[SteamLobby] ホスト側Steamリレー待機中... " + avail);
            elapsed += Time.deltaTime;
            yield return null;
        }

        var networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.isActiveAndEnabled)
        {
            SetOperationInProgress(false);
            yield break;
        }

        if (!PrepareSteamNetworkManager(networkManager))
        {
            SetOperationInProgress(false);
            yield break;
        }

        // ホスト開始
        bool started = networkManager.StartHost();

        Debug.Log(
            "[SteamLobby] StartHost: " +
            started
        );

        if (!started)
        {
            SetOperationInProgress(false);
            yield break;
        }

        SetSessionNetworkManager(networkManager);

        SteamMatchmaking.SetLobbyData(
            new CSteamID(LobbyID),
            s_GameStartedKey,
            "true"
        );

        Debug.Log("[SteamLobby] LobbyData game_started=true");

        networkManager.SceneManager.OnSceneEvent -= OnNetworkSceneEvent;
        networkManager.SceneManager.OnSceneEvent += OnNetworkSceneEvent;

        // シーン移動
        SceneEventProgressStatus sceneLoadStatus =
            networkManager.SceneManager.LoadScene(
                gameSceneName,
                LoadSceneMode.Single
            );

        Debug.Log(
            "[SteamLobby] Host LoadScene " +
            gameSceneName +
            ": " +
            sceneLoadStatus
        );

        if (sceneLoadStatus != SceneEventProgressStatus.Started)
        {
            Debug.LogError(
                "[SteamLobby] Host LoadScene failed: " +
                sceneLoadStatus
            );
            SetOperationInProgress(false);
        }
    }

    /// <summary>
    /// ロビー参加
    /// </summary>
    public void JoinLobby(CSteamID lobbyID)
    {
        JoinLobby(lobbyID, false);
    }

    private void JoinLobby(CSteamID lobbyID, bool fromSearch)
    {
        if (IsBusy && !fromSearch)
        {
            Debug.LogWarning("[SteamLobby] ロビー操作中のため参加を無視します");
            return;
        }

        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamLobby] Steam未初期化");
            return;
        }

        SetOperationInProgress(true);
        clientStartRequested = false;

        SteamMatchmaking.JoinLobby(lobbyID);

        Debug.Log(
            "[SteamLobby] ロビー参加開始: " +
            lobbyID.m_SteamID
        );
    }

    /// <summary>
    /// Steam招待参加
    /// </summary>
    private void OnGameLobbyJoinRequested(
        GameLobbyJoinRequested_t callback
    )
    {
        JoinLobby(callback.m_steamIDLobby);
    }

    /// <summary>
    /// ロビー入室完了
    /// </summary>
    private void OnLobbyEntered(
        LobbyEnter_t callback
    )
    {
        if ((EChatRoomEnterResponse)
            callback.m_EChatRoomEnterResponse
            != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            Debug.LogError(
                "[SteamLobby] ロビー入室失敗"
            );

            SetOperationInProgress(false);
            return;
        }

        LobbyID = callback.m_ulSteamIDLobby;
        var lobbyId = new CSteamID(LobbyID);

        string hostAddress =
            SteamMatchmaking.GetLobbyData(
                lobbyId,
                s_HostAddressKey
            );

        CSteamID ownerId = SteamMatchmaking.GetLobbyOwner(lobbyId);

        Debug.Log(
            "[SteamLobby] ロビー入室成功 Host: " +
            hostAddress +
            " Owner: " +
            ownerId
        );

        // 自分がホストなら終了
        if (ownerId == SteamUser.GetSteamID() ||
            hostAddress == SteamUser.GetSteamID().ToString())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(hostAddress))
        {
            Debug.LogWarning(
                "[SteamLobby] HostAddressが空です。LobbyOwnerを接続先として使います。"
            );
        }

        LobbyCode =
            SteamMatchmaking.GetLobbyData(
                lobbyId,
                s_LobbyCodeKey
            );

        Debug.Log(
            "[SteamLobby] 部屋コード取得: " +
            LobbyCode
        );

        SendLobbyChatMessage(
            "Join request from " + GetLocalPersonaName()
        );

        TryStartClientFromLobby("LobbyEnter");
    }

    private void OnLobbyDataUpdated(LobbyDataUpdate_t callback)
    {
        if (callback.m_ulSteamIDLobby != LobbyID ||
            callback.m_ulSteamIDMember != callback.m_ulSteamIDLobby)
        {
            return;
        }

        Debug.Log(
            "[SteamLobby] LobbyDataUpdate: LobbyID=" +
            callback.m_ulSteamIDLobby +
            " Success=" +
            callback.m_bSuccess
        );

        if (callback.m_bSuccess == 0)
        {
            Debug.LogWarning("[SteamLobby] LobbyDataUpdate failed.");
            SetOperationInProgress(false);
            return;
        }

        if (SteamMatchmaking.GetLobbyOwner(new CSteamID(LobbyID)) ==
            SteamUser.GetSteamID())
        {
            return;
        }

        TryStartClientFromLobby("LobbyDataUpdate");
    }

    private void TryStartClientFromLobby(string source)
    {
        if (clientStartRequested)
        {
            Debug.Log("[SteamLobby] StartClient already requested. Source=" + source);
            return;
        }

        if (LobbyID == 0)
        {
            Debug.LogWarning("[SteamLobby] LobbyIDが未設定です Source=" + source);
            return;
        }

        var lobbyId = new CSteamID(LobbyID);
        string gameStarted =
            SteamMatchmaking.GetLobbyData(lobbyId, s_GameStartedKey);

        if (gameStarted != "true")
        {
            Debug.Log(
                "[SteamLobby] ホスト準備待ち Source=" +
                source +
                " game_started=" +
                gameStarted
            );
            return;
        }

        string hostAddress =
            SteamMatchmaking.GetLobbyData(lobbyId, s_HostAddressKey);

        if (string.IsNullOrWhiteSpace(hostAddress))
        {
            CSteamID ownerId = SteamMatchmaking.GetLobbyOwner(lobbyId);
            hostAddress = ownerId.m_SteamID.ToString();

            Debug.LogWarning(
                "[SteamLobby] HostAddressが空のためLobbyOwnerを使用します: " +
                hostAddress
            );
        }

        if (!ulong.TryParse(hostAddress, out _))
        {
            Debug.LogError(
                "[SteamLobby] HostAddressがSteamIDとして不正です: " +
                hostAddress
            );
            SetOperationInProgress(false);
            return;
        }

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[SteamLobby] NetworkManagerがありません");
            SetOperationInProgress(false);
            return;
        }

        clientStartRequested = true;
        StartCoroutine(StartClientRoutine(hostAddress));
    }

    private IEnumerator StartClientRoutine(string hostAddress)
    {
        var networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            SetOperationInProgress(false);
            clientStartRequested = false;
            yield break;
        }

        if (networkManager.IsListening)
        {
            Debug.Log("[SteamLobby] IsListening Before: True");
            Debug.LogWarning("[SteamLobby] NetworkManager is already listening. Shutting down first...");
            networkManager.Shutdown();

            // Shutdown完了を待つために0.2秒待機
            yield return new WaitForSeconds(0.2f);
        }

        // Steamリレーの準備完了を最大10秒待つ
        float elapsed = 0f;
        const float relayTimeout = 10f;

        while (elapsed < relayTimeout)
        {
            SteamRelayNetworkStatus_t status;
            ESteamNetworkingAvailability avail =
                SteamNetworkingUtils.GetRelayNetworkStatus(out status);

            if (avail == ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Current)
            {
                Debug.Log("[SteamLobby] Steamリレー準備完了");
                break;
            }

            if (avail == ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Failed)
            {
                Debug.LogWarning("[SteamLobby] Steamリレー初期化失敗 — 直接接続で試みます");
                break;
            }

            Debug.Log("[SteamLobby] Steamリレー待機中... " + avail);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (networkManager == null || !networkManager.isActiveAndEnabled)
        {
            SetOperationInProgress(false);
            clientStartRequested = false;
            yield break;
        }

        if (!PrepareSteamNetworkManager(networkManager))
        {
            SetOperationInProgress(false);
            clientStartRequested = false;
            yield break;
        }

        var transport =
            (SteamNetworkingSocketsTransport)
            networkManager.NetworkConfig.NetworkTransport;

        transport.ConnectToSteamID = ulong.Parse(hostAddress);

        // 重複登録を避けるため、一度-=してから+=する
        networkManager.OnClientConnectedCallback -= OnClientConnected;
        networkManager.OnClientConnectedCallback += OnClientConnected;

        networkManager.OnClientDisconnectCallback -= OnClientDisconnect;
        networkManager.OnClientDisconnectCallback += OnClientDisconnect;

        networkManager.SceneManager.OnSceneEvent -= OnNetworkSceneEvent;
        networkManager.SceneManager.OnSceneEvent += OnNetworkSceneEvent;

        bool result = networkManager.StartClient();

        Debug.Log("[SteamLobby] StartClient: " + result);

        if (!result)
        {
            Debug.LogError("[SteamLobby] StartClientに失敗しました");
            SetOperationInProgress(false);
            clientStartRequested = false;

            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnect;
            networkManager.SceneManager.OnSceneEvent -= OnNetworkSceneEvent;
            yield break;
        }

        SetSessionNetworkManager(networkManager);

        connectionTimeoutCoroutine = StartCoroutine(ConnectionTimeout(30f));
    }

    private bool PrepareSteamNetworkManager(NetworkManager networkManager)
    {
        if (networkManager == null)
        {
            Debug.LogError("[SteamLobby] NetworkManagerがありません");
            return false;
        }

        if (!(networkManager.NetworkConfig.NetworkTransport is SteamNetworkingSocketsTransport))
        {
            string transportName = networkManager.NetworkConfig.NetworkTransport != null
                ? networkManager.NetworkConfig.NetworkTransport.GetType().Name
                : "(none)";

            Debug.LogError(
                "[SteamLobby] Steam接続にはSteamNetworkingSocketsTransportが必要です。現在のTransport=" +
                transportName
            );

            return false;
        }

        return true;
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
                "[SteamLobby] セッション中の重複NetworkManagerを破棄します: " +
                networkManager.gameObject.scene.name + "/" +
                networkManager.name
            );

            Destroy(networkManager.gameObject);
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log(
            "[SteamLobby] クライアント接続成功 ClientID: " +
            clientId
        );

        var networkManager = NetworkManager.Singleton;
        if (networkManager == null ||
            clientId != networkManager.LocalClientId)
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
            StartCoroutine(ClientSceneSyncGuard(15f));
    }

    private void OnNetworkSceneEvent(SceneEvent sceneEvent)
    {
        Debug.Log(
            "[SteamLobby] SceneEvent: " +
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
            SetOperationInProgress(false);
        }
    }

    private IEnumerator ClientSceneSyncGuard(float seconds)
    {
        float elapsed = 0f;

        while (elapsed < seconds)
        {
            var networkManager = NetworkManager.Singleton;
            string activeSceneName =
                SceneManager.GetActiveScene().name;

            if (activeSceneName == gameSceneName)
            {
                Debug.Log(
                    "[SteamLobby] クライアント側シーン同期完了: " +
                    gameSceneName
                );

                SetOperationInProgress(false);
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
                "[SteamLobby] 接続済みですがシーン同期が完了しないため、" +
                "クライアント側でGameRoomへ復旧遷移します。 ActiveScene=" +
                SceneManager.GetActiveScene().name
            );

            SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            SetOperationInProgress(false);
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
                "[SteamLobby] 接続タイムアウト (" + seconds + "秒) — ホストに到達できませんでした"
            );

            NetworkManager.Singleton.OnClientConnectedCallback -=
                OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -=
                OnClientDisconnect;
            NetworkManager.Singleton.SceneManager.OnSceneEvent -=
                OnNetworkSceneEvent;

            NetworkManager.Singleton.Shutdown();
            sessionNetworkManager = null;
            SetOperationInProgress(false);
            clientStartRequested = false;
            SceneManager.LoadScene(menuSceneName);
        }
    }

    /// <summary>
    /// 接続承認
    /// </summary>
    private void ApprovalCheck(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response
    )
    {
        int currentCount =
            NetworkManager.Singleton.ConnectedClients.Count;

        Debug.Log(
            "[SteamLobby] ApprovalCheck: 現在の接続数=" +
            currentCount + " / 最大=" + maxMembers
        );

        response.Pending = true;

        if (currentCount >= maxMembers)
        {
            Debug.LogWarning(
                "[SteamLobby] ApprovalCheck: 満員のため拒否"
            );
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

        Debug.Log(
            "[SteamLobby] ApprovalCheck: 承認 ClientID=" +
            request.ClientNetworkId
        );
    }

    /// <summary>
    /// 切断時
    /// </summary>
    private void OnClientDisconnect(
        ulong clientId
    )
    {
        string reason =
            NetworkManager.Singleton.DisconnectReason;

        Debug.LogWarning(
            "[SteamLobby] クライアント切断 ClientID=" +
            clientId + " 理由=" +
            (string.IsNullOrEmpty(reason) ? "(なし)" : reason)
        );

        NetworkManager.Singleton.OnClientConnectedCallback -=
            OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -=
            OnClientDisconnect;
        NetworkManager.Singleton.SceneManager.OnSceneEvent -=
            OnNetworkSceneEvent;

        StartCoroutine(ShutdownRoutine());
    }

    private IEnumerator ShutdownRoutine()
    {
        var networkManager = NetworkManager.Singleton;
        if (networkManager != null)
        {
            networkManager.Shutdown();
            sessionNetworkManager = null;
            
            // Shutdown完了を待つために0.2秒待機
            yield return new WaitForSeconds(0.2f);
        }

        SetOperationInProgress(false);
        clientStartRequested = false;

        SceneManager.LoadScene(menuSceneName);
    }

    /// <summary>
    /// 部屋コード生成
    /// </summary>
    private string GenerateRoomCode()
    {
        const string chars =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        char[] code = new char[5];

        for (int i = 0; i < code.Length; i++)
        {
            code[i] =
                chars[UnityEngine.Random.Range(0, chars.Length)];
        }

        return new string(code);
    }

    /// <summary>
    /// 部屋コード参加
    /// </summary>
    public void JoinLobbyWithCode(string code)
    {
        if (IsBusy)
        {
            Debug.LogWarning("[SteamLobby] ロビー操作中のため検索を無視します");
            return;
        }

        if (!SteamManager.Initialized)
        {
            Debug.LogError(
                "[SteamLobby] Steam未初期化"
            );

            return;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            Debug.LogError(
                "[SteamLobby] コードが空"
            );

            return;
        }

        string targetCode =
            code.ToUpper().Trim();

        SetOperationInProgress(true);

        Debug.Log(
            "[SteamLobby] 検索コード: " +
            targetCode
        );

        // 検索範囲を全世界（Worldwide）に設定して、遠方のプレイヤーも検索可能にする
        SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);

        // 修正版
        SteamMatchmaking.AddRequestLobbyListStringFilter(
            s_LobbyCodeKey,
            targetCode,
            ELobbyComparison.k_ELobbyComparisonEqual
        );

        SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);
        SteamMatchmaking.AddRequestLobbyListResultCountFilter(1);

        SteamAPICall_t handle =
            SteamMatchmaking.RequestLobbyList();

        m_crLobbyMatchList.Set(handle);
    }

    /// <summary>
    /// ロビー検索完了
    /// </summary>
    private void OnLobbyMatchList(
        LobbyMatchList_t callback,
        bool ioFailure
    )
    {
        if (ioFailure)
        {
            Debug.LogError(
                "[SteamLobby] 検索失敗"
            );

            SetOperationInProgress(false);
            return;
        }

        if (callback.m_nLobbiesMatching <= 0)
        {
            Debug.LogError(
                "[SteamLobby] ロビーが見つかりません"
            );

            SetOperationInProgress(false);
            return;
        }

        CSteamID lobbyID =
            SteamMatchmaking.GetLobbyByIndex(0);

        Debug.Log(
            "[SteamLobby] ロビー発見: " +
            lobbyID.m_SteamID
        );

        JoinLobby(lobbyID, true);
    }

    private void SendLobbyChatMessage(string message)
    {
        if (LobbyID == 0 || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        byte[] messageBytes = Encoding.UTF8.GetBytes(message + "\0");
        bool sent = SteamMatchmaking.SendLobbyChatMsg(
            new CSteamID(LobbyID),
            messageBytes,
            messageBytes.Length
        );

        Debug.Log(
            "[SteamLobby] ロビーチャット送信: " +
            sent + " / " + message
        );
    }

    private void OnLobbyChatMessage(LobbyChatMsg_t callback)
    {
        if (callback.m_ulSteamIDLobby != LobbyID)
        {
            return;
        }

        byte[] buffer = new byte[s_LobbyChatMessageMaxBytes];
        int messageLength = SteamMatchmaking.GetLobbyChatEntry(
            new CSteamID(callback.m_ulSteamIDLobby),
            (int)callback.m_iChatID,
            out CSteamID sender,
            buffer,
            buffer.Length,
            out EChatEntryType chatEntryType
        );

        if (messageLength <= 0)
        {
            return;
        }

        string senderName = SteamFriends.GetFriendPersonaName(sender);
        if (string.IsNullOrWhiteSpace(senderName))
        {
            senderName = sender.m_SteamID.ToString();
        }

        string message = Encoding.UTF8.GetString(buffer, 0, messageLength)
            .TrimEnd('\0');
        Debug.Log(
            "[SteamLobby] ロビーチャット受信 (" +
            chatEntryType + ") " + senderName + ": " + message
        );
    }

    private string GetLocalPersonaName()
    {
        if (string.IsNullOrWhiteSpace(LocalPersonaName))
        {
            LocalPersonaName = SteamFriends.GetPersonaName();
        }

        return string.IsNullOrWhiteSpace(LocalPersonaName)
            ? SteamUser.GetSteamID().ToString()
            : LocalPersonaName;
    }

    private void SetOperationInProgress(bool value)
    {
        if (operationInProgress == value)
        {
            return;
        }

        operationInProgress = value;
        BusyStateChanged?.Invoke(IsBusy);
    }

    /// <summary>
    /// Steam招待UI
    /// </summary>
    public void InviteFriends()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError(
                "[SteamLobby] Steam未初期化"
            );

            return;
        }

        if (LobbyID == 0)
        {
            Debug.LogError(
                "[SteamLobby] ロビー未参加"
            );

            return;
        }

        SteamFriends.ActivateGameOverlayInviteDialog(
            new CSteamID(LobbyID)
        );

        Debug.Log(
            "[SteamLobby] 招待オーバーレイ表示"
        );
    }
}
