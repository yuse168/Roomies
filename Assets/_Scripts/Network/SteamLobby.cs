using System;
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

    // ロビーデータキー
    private const string s_HostAddressKey = "HostAddress";

    public ulong LobbyID { get; private set; }
    public string LobbyCode { get; private set; }
    public bool IsBusy => operationInProgress ||
        (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening);
    public event Action<bool> BusyStateChanged;

    // シングルトン
    private static SteamLobby instance;
    private bool operationInProgress;

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

        Debug.Log(
            "[SteamLobby] Steam初期化成功: " +
            SteamUser.GetSteamID()
        );
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
            "LobbyCode",
            LobbyCode
        );

        Debug.Log(
            "[SteamLobby] 部屋コード: " +
            LobbyCode
        );

        // 接続承認
        NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
        NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;

        // ホスト開始
        bool started =
            NetworkManager.Singleton.StartHost();

        Debug.Log(
            "[SteamLobby] StartHost: " +
            started
        );

        if (!started)
        {
            SetOperationInProgress(false);
            return;
        }

        // シーン移動
        NetworkManager.Singleton.SceneManager.LoadScene(
            gameSceneName,
            LoadSceneMode.Single
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

        string hostAddress =
            SteamMatchmaking.GetLobbyData(
                new CSteamID(callback.m_ulSteamIDLobby),
                s_HostAddressKey
            );

        Debug.Log(
            "[SteamLobby] ロビー入室成功 Host: " +
            hostAddress
        );

        // 自分がホストなら終了
        if (hostAddress ==
            SteamUser.GetSteamID().ToString())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(hostAddress))
        {
            Debug.LogError(
                "[SteamLobby] HostAddressが空"
            );

            SetOperationInProgress(false);
            return;
        }

        LobbyID = callback.m_ulSteamIDLobby;

        LobbyCode =
            SteamMatchmaking.GetLobbyData(
                new CSteamID(LobbyID),
                "LobbyCode"
            );

        Debug.Log(
            "[SteamLobby] 部屋コード取得: " +
            LobbyCode
        );

        // Transport取得
        var networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            Debug.LogError("[SteamLobby] NetworkManagerがありません");
            SetOperationInProgress(false);
            return;
        }

        if (networkManager.IsListening)
        {
            Debug.LogWarning("[SteamLobby] StartClient skipped because NetworkManager is already listening.");
            SetOperationInProgress(false);
            return;
        }

        var transport =
            (SteamNetworkingSocketsTransport)
            networkManager.NetworkConfig.NetworkTransport;

        transport.ConnectToSteamID =
            ulong.Parse(hostAddress);

        // クライアント接続
        bool result =
            networkManager.StartClient();

        Debug.Log(
            "[SteamLobby] StartClient: " +
            result
        );

        if (!result)
        {
            SetOperationInProgress(false);
            return;
        }

        networkManager.OnClientDisconnectCallback -=
            OnClientDisconnect;
        networkManager.OnClientDisconnectCallback +=
            OnClientDisconnect;
    }

    /// <summary>
    /// 接続承認
    /// </summary>
    private void ApprovalCheck(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response
    )
    {
        response.Pending = true;

        // 最大人数
        if (NetworkManager.Singleton.ConnectedClients.Count
            >= maxMembers)
        {
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

    /// <summary>
    /// 切断時
    /// </summary>
    private void OnClientDisconnect(
        ulong clientId
    )
    {
        NetworkManager.Singleton.OnClientDisconnectCallback -=
            OnClientDisconnect;

        NetworkManager.Singleton.Shutdown();

        SetOperationInProgress(false);

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
                chars[Random.Range(0, chars.Length)];
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

        // 修正版
        SteamMatchmaking.AddRequestLobbyListStringFilter(
            "LobbyCode",
            targetCode,
            ELobbyComparison.k_ELobbyComparisonEqual
        );

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
