using System;
using System.Text;
using Steamworks;
using Unity.Netcode;
using UnityEngine;

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
    public NetworkSessionState State => NetworkSessionManager.Instance.State;
    public bool IsBusy => operationInProgress ||
        NetworkSessionManager.Instance.IsBusy;
    public event Action<bool> BusyStateChanged;

    // シングルトン
    private static SteamLobby instance;
    private bool operationInProgress;
    private bool clientStartRequested;
    private bool steamCallbacksRegistered;

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

        RegisterSteamCallbacks();

        // Steamリレーネットワークを起動（ファイアウォール環境での接続に必要）
        SteamNetworkingUtils.InitRelayNetworkAccess();

        LocalPersonaName = SteamFriends.GetPersonaName();

        Debug.Log(
            "[SteamLobby] Steam初期化成功: " +
            LocalPersonaName + " (" +
            SteamUser.GetSteamID() + ")"
        );
    }

    private void RegisterSteamCallbacks()
    {
        if (steamCallbacksRegistered)
        {
            Debug.Log("[SteamLobby] Steam callbacks are already registered.");
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

        steamCallbacksRegistered = true;
        Debug.Log("[SteamLobby] Steam callbacks registered.");
    }

    private bool EnsureSteamCallbacksRegistered()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamLobby] Steam未初期化");
            return false;
        }

        if (!steamCallbacksRegistered)
        {
            RegisterSteamCallbacks();
        }

        return steamCallbacksRegistered;
    }

    private void OnEnable()
    {
        NetworkSessionManager.Instance.StateChanged -= OnSessionStateChanged;
        NetworkSessionManager.Instance.StateChanged += OnSessionStateChanged;
    }

    private void OnDisable()
    {
        NetworkSessionManager sessionManager =
            FindAnyObjectByType<NetworkSessionManager>();

        if (sessionManager != null)
        {
            sessionManager.StateChanged -= OnSessionStateChanged;
        }
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

        if (!EnsureSteamCallbacksRegistered())
        {
            return;
        }

        NetworkSessionManager.Instance.SetState(NetworkSessionState.CreatingLobby);
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

            NetworkSessionManager.Instance.SetState(NetworkSessionState.Idle);
            SetOperationInProgress(false);
            return;
        }

        LobbyID = callback.m_ulSteamIDLobby;

        Debug.Log(
            "[SteamLobby] ロビー作成成功: " +
            LobbyID
        );

        CSteamID createdLobbyId = new CSteamID(LobbyID);
        CSteamID localSteamId = SteamUser.GetSteamID();
        CSteamID ownerSteamId = SteamMatchmaking.GetLobbyOwner(createdLobbyId);

        Debug.Log(
            "[SteamLobby] ロビー作成時SteamID " +
            "LocalSteamID=" + localSteamId.m_SteamID +
            " LobbyOwnerSteamID=" + ownerSteamId.m_SteamID
        );

        // 部屋コード生成
        LobbyCode = GenerateRoomCode();

        // ホストSteamID保存
        SteamMatchmaking.SetLobbyData(
            createdLobbyId,
            s_HostAddressKey,
            localSteamId.m_SteamID.ToString()
        );

        // 部屋コード保存
        SteamMatchmaking.SetLobbyData(
            createdLobbyId,
            s_LobbyCodeKey,
            LobbyCode
        );

        SteamMatchmaking.SetLobbyData(
            createdLobbyId,
            s_GameStartedKey,
            "false"
        );

        Debug.Log(
            "[SteamLobby] LobbyData設定 " +
            s_HostAddressKey + "=" + localSteamId.m_SteamID +
            " " + s_GameStartedKey + "=false"
        );

        Debug.Log(
            "[SteamLobby] 部屋コード: " +
            LobbyCode
        );

        NetworkSessionManager.Instance.MarkLobbyReady();

        bool sessionStarted =
            NetworkSessionManager.Instance.StartSteamHost(
                maxMembers,
                gameSceneName,
                menuSceneName,
                MarkGameStarted
            );

        if (!sessionStarted)
        {
            SetOperationInProgress(false);
            return;
        }
    }

    private void MarkGameStarted()
    {
        if (LobbyID == 0)
        {
            return;
        }

        SteamMatchmaking.SetLobbyData(
            new CSteamID(LobbyID),
            s_GameStartedKey,
            "true"
        );

        ulong localSteamId = SteamUser.GetSteamID().m_SteamID;
        ulong ownerSteamId =
            SteamMatchmaking.GetLobbyOwner(new CSteamID(LobbyID)).m_SteamID;
        string hostAddress =
            SteamMatchmaking.GetLobbyData(
                new CSteamID(LobbyID),
                s_HostAddressKey
            );

        Debug.Log(
            "[SteamLobby] LobbyData game_started=true " +
            "LocalSteamID=" + localSteamId +
            " LobbyOwnerSteamID=" + ownerSteamId +
            " HostAddress=" + hostAddress
        );
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
        if (!ResetListeningNetworkManagerBeforeJoin())
        {
            return;
        }

        if ((operationInProgress ||
            NetworkSessionManager.Instance.State == NetworkSessionState.StartingHost ||
            NetworkSessionManager.Instance.State == NetworkSessionState.Connecting) &&
            !fromSearch)
        {
            Debug.LogWarning("[SteamLobby] ロビー操作中のため参加を無視します");
            return;
        }

        if (!EnsureSteamCallbacksRegistered())
        {
            return;
        }

        NetworkSessionManager.Instance.SetState(NetworkSessionState.InLobby);
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

            NetworkSessionManager.Instance.SetState(NetworkSessionState.Idle);
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
            ownerId.m_SteamID +
            " LocalSteamID=" +
            SteamUser.GetSteamID().m_SteamID
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
        CSteamID ownerId = SteamMatchmaking.GetLobbyOwner(lobbyId);

        if (gameStarted != "true")
        {
            Debug.Log(
                "[SteamLobby] ホスト準備待ち Source=" +
                source +
                " game_started=" + gameStarted +
                " LocalSteamID=" + SteamUser.GetSteamID().m_SteamID +
                " LobbyOwnerSteamID=" + ownerId.m_SteamID
            );
            return;
        }

        string hostAddress =
            SteamMatchmaking.GetLobbyData(lobbyId, s_HostAddressKey);
        ulong ownerSteamId = ownerId.m_SteamID;
        ulong hostSteamId = ownerSteamId;

        if (ownerSteamId == 0)
        {
            if (string.IsNullOrWhiteSpace(hostAddress) ||
                !ulong.TryParse(hostAddress, out hostSteamId))
            {
                Debug.LogError(
                    "[SteamLobby] LobbyOwnerとHostAddressの両方から接続先SteamIDを取得できません。 " +
                    "HostAddress=" + hostAddress
                );
                SetOperationInProgress(false);
                return;
            }

            Debug.LogWarning(
                "[SteamLobby] LobbyOwnerが不明のためHostAddressを使用します: " +
                hostSteamId
            );
        }
        else if (!string.IsNullOrWhiteSpace(hostAddress) &&
            ulong.TryParse(hostAddress, out ulong lobbyDataHostSteamId) &&
            lobbyDataHostSteamId != ownerSteamId)
        {
            Debug.LogWarning(
                "[SteamLobby] HostAddressとLobbyOwnerが一致しません。LobbyOwnerを接続先として優先します。 " +
                "HostAddress=" + lobbyDataHostSteamId +
                " LobbyOwnerSteamID=" + ownerSteamId
            );
        }

        Debug.Log(
            "[SteamLobby] StartClient要求 Source=" + source +
            " LocalSteamID=" + SteamUser.GetSteamID().m_SteamID +
            " HostAddress=" + hostAddress +
            " HostSteamID=" + hostSteamId +
            " LobbyOwnerSteamID=" + ownerSteamId +
            " game_started=" + gameStarted
        );

        if (hostSteamId == SteamUser.GetSteamID().m_SteamID)
        {
            Debug.LogError(
                "[SteamLobby] HostSteamIDが自分自身です。クライアント開始を中止します。"
            );
            SetOperationInProgress(false);
            return;
        }

        clientStartRequested = true;
        bool sessionStarted =
            NetworkSessionManager.Instance.StartSteamClient(
                hostSteamId,
                gameSceneName,
                menuSceneName
            );

        if (!sessionStarted)
        {
            SetOperationInProgress(false);
            clientStartRequested = false;
        }
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
        if (!ResetListeningNetworkManagerBeforeJoin())
        {
            return;
        }

        if (IsBusy)
        {
            Debug.LogWarning("[SteamLobby] ロビー操作中のため検索を無視します");
            return;
        }

        if (!EnsureSteamCallbacksRegistered())
        {
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

    private bool ResetListeningNetworkManagerBeforeJoin()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            Debug.LogError("[SteamLobby] NetworkManager.Singletonがありません。Joinを中止します。");
            return false;
        }

        if (networkManager.IsListening)
        {
            Debug.LogWarning(
                "[SteamLobby] Join前にNetworkManagerがListening状態のためShutdownします。 " +
                "IsHost=" + networkManager.IsHost +
                " IsServer=" + networkManager.IsServer +
                " IsClient=" + networkManager.IsClient
            );
            networkManager.Shutdown();
        }

        return true;
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

            NetworkSessionManager.Instance.SetState(NetworkSessionState.Idle);
            SetOperationInProgress(false);
            return;
        }

        if (callback.m_nLobbiesMatching <= 0)
        {
            Debug.LogError(
                "[SteamLobby] ロビーが見つかりません"
            );

            NetworkSessionManager.Instance.SetState(NetworkSessionState.Idle);
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

        if (message.StartsWith("Join request from ", StringComparison.Ordinal))
        {
            Debug.Log(
                "[SteamLobby] Join request詳細 " +
                "SenderSteamID=" + sender.m_SteamID +
                " LocalSteamID=" + SteamUser.GetSteamID().m_SteamID +
                " LobbyOwnerSteamID=" +
                SteamMatchmaking.GetLobbyOwner(
                    new CSteamID(callback.m_ulSteamIDLobby)
                ).m_SteamID +
                " HostAddress=" +
                SteamMatchmaking.GetLobbyData(
                    new CSteamID(callback.m_ulSteamIDLobby),
                    s_HostAddressKey
                ) +
                " game_started=" +
                SteamMatchmaking.GetLobbyData(
                    new CSteamID(callback.m_ulSteamIDLobby),
                    s_GameStartedKey
                )
            );
        }
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

    private void OnSessionStateChanged(NetworkSessionState state)
    {
        if (state == NetworkSessionState.Idle ||
            state == NetworkSessionState.InLobby ||
            state == NetworkSessionState.InGame)
        {
            operationInProgress = false;
        }

        if (state == NetworkSessionState.Idle)
        {
            clientStartRequested = false;
        }

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
