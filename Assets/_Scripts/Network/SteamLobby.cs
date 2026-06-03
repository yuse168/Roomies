using System;
using System.Text;
using Steamworks;
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

        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamLobby] Steam未初期化");
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

        Debug.Log("[SteamLobby] LobbyData game_started=true");
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

        if (!ulong.TryParse(hostAddress, out ulong hostSteamId))
        {
            Debug.LogError(
                "[SteamLobby] HostAddressがSteamIDとして不正です: " +
                hostAddress
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
