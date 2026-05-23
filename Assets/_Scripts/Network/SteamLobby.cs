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

    //ロビー作成コールバック
    private CallResult<LobbyCreated_t> m_crLobbyCreated;
    //ロビー入出コールバック
    private Callback<LobbyEnter_t> m_lobbyEnter;
    //ゲーム招待コールバック
    private Callback<GameLobbyJoinRequested_t> m_gameLobbyJoinRequested;

    //ロビーデータ設定用キー
    private const string s_HostAddressKey = "HostAddress";

    public ulong LobbyID { get; private set; }

    public void Start()
    {
        //SteamManagerの初期化が完了していたら
        if (SteamManager.Initialized)
        {
            m_crLobbyCreated = CallResult<LobbyCreated_t>.Create(OnCreateLobby);
            m_lobbyEnter = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            m_gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
            Debug.Log($"[SteamLobby] Steam initialized. My SteamID: {SteamUser.GetSteamID()}");
            return;
        }

        Debug.LogError("[SteamLobby] SteamManager is not initialized. Is Steam running, and is steam_appid.txt present?");
    }

    /// <summary>
    /// ロビー作成（ゲームをホスト）
    /// </summary>
    public void CreateLobby()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamLobby] CreateLobby failed because Steam is not initialized.");
            return;
        }

        Debug.Log("[SteamLobby] CreateLobby called.");
        SteamAPICall_t hCreateLobby = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, maxMembers);
        m_crLobbyCreated.Set(hCreateLobby);
    }

    //ロビー作成完了コールバック
    private void OnCreateLobby(LobbyCreated_t pCallback, bool bIOFailure)
    {
        //ロビー作成成功していなかった場合
        if (pCallback.m_eResult != EResult.k_EResultOK || bIOFailure)
        {
            Debug.LogError($"[SteamLobby] CreateLobby failed. Result: {pCallback.m_eResult}, IOFailure: {bIOFailure}");
            return;
        }

        //ホストのアドレス（SteamID）を登録
        SteamMatchmaking.SetLobbyData(
            new CSteamID(pCallback.m_ulSteamIDLobby),
            s_HostAddressKey,
            SteamUser.GetSteamID().ToString());

        //ロビーID保存
        LobbyID = pCallback.m_ulSteamIDLobby;
        Debug.Log($"[SteamLobby] Lobby created. LobbyID: {LobbyID}");

        //サーバー開始コールバック
        NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
        NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;
        //ホスト開始
        bool started = NetworkManager.Singleton.StartHost();
        Debug.Log($"[SteamLobby] StartHost result: {started}");
        //シーンを切り替え
        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

    /// <summary>
    /// ロビー入出
    /// </summary>
    public void JoinLobby(CSteamID lobbyID)
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamLobby] JoinLobby failed because Steam is not initialized.");
            return;
        }

        Debug.Log($"[SteamLobby] JoinLobby called. LobbyID: {lobbyID.m_SteamID}");
        SteamMatchmaking.JoinLobby(lobbyID);
    }

    /// <summary>
    /// ゲームの招待を受けた時のコールバック
    /// </summary>
    /// <param name="callback"></param>
    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    /// <summary>
    /// ロビー入室コールバック
    /// </summary>
    /// <param name="callback"></param>
    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        //入室失敗時
        if ((EChatRoomEnterResponse)callback.m_EChatRoomEnterResponse != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            Debug.LogError($"[SteamLobby] Lobby enter failed. Response: {(EChatRoomEnterResponse)callback.m_EChatRoomEnterResponse}");
            return;
        }

        //ホストのSteamIDを取得
        string hostAddress = SteamMatchmaking.GetLobbyData(
            new CSteamID(callback.m_ulSteamIDLobby),
            s_HostAddressKey);

        Debug.Log($"[SteamLobby] Entered LobbyID: {callback.m_ulSteamIDLobby}, HostAddress: {hostAddress}");

        //ホスト（CreateLobbyした本人）もここを通るのでクライアント接続しないようにリターン
        if (hostAddress == SteamUser.GetSteamID().ToString()) { return; }

        if (string.IsNullOrWhiteSpace(hostAddress))
        {
            Debug.LogError("[SteamLobby] HostAddress was empty. Lobby data was not set or the key did not match.");
            return;
        }

        //ロビーID保存
        LobbyID = callback.m_ulSteamIDLobby;

        //Netcodeでクライアント接続
        var stp = (SteamNetworkingSocketsTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
        stp.ConnectToSteamID = ulong.Parse(hostAddress);

        //ホストに接続
        bool result = NetworkManager.Singleton.StartClient();
        Debug.Log($"[SteamLobby] StartClient result: {result}");
        //切断時
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;

        Debug.Log($"[SteamLobby] Connecting to host SteamID: {hostAddress}");
    }

    /// <summary>
    /// 接続承認
    /// </summary>
    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        // 追加の承認手順が必要な場合は、追加の手順が完了するまでこれを true に設定します
        // true から false に遷移すると、接続承認応答が処理されます。
        response.Pending = true;

        //最大
        if (NetworkManager.Singleton.ConnectedClients.Count >= maxMembers)
        {
            response.Approved = false;
            response.Pending = false;
            return;
        }

        //ここからは接続成功クライアントに向けた処理
        response.Approved = true;//接続を許可

        //PlayerObjectを生成するかどうか
        response.CreatePlayerObject = true;
        //生成するPlayerObjectのPrefabハッシュ値。nullの場合NetworkManagerに登録したプレハブが使用される
        response.PlayerPrefabHash = null;

        // nullの場合、PlayerPrefabに設定された初期位置・回転が使用される
        response.Position = null;
        response.Rotation = null;

        response.Pending = false;
    }

    /// <summary>
    /// クライアントが切断したとき
    /// </summary>
    private void OnClientDisconnect(ulong clientId)
    {
        //クライアント切断コールバック
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
        //ネットワークマネージャーを破棄（これで新しくNetworkManagerを作る（使う）ことができる）
        NetworkManager.Singleton.Shutdown();
        //メインシーンに戻る
        SceneManager.LoadScene(menuSceneName);
    }

    //簡易的なシングルトン
    private static SteamLobby instance;
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
}
