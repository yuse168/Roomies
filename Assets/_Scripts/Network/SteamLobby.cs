using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Netcode.Transports;
using Steamworks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Steam Lobby管理クラス。
/// ロビー作成→Lobby UI表示→ホストがStartしたらGameRoom遷移という流れを管理する。
/// iiwakekingのSteamNetSession/OnlineSessionの設計を参考にRoomies向けに実装。
/// </summary>
public class SteamLobby : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "GameRoom";
    [SerializeField] private string menuSceneName = "MainMenuSteam";
    [SerializeField] private int maxMembers = 4;

    // ----- Steamコールバック -----
    private CallResult<LobbyCreated_t>    m_crLobbyCreated;
    private Callback<LobbyEnter_t>        m_cbLobbyEnter;
    private Callback<GameLobbyJoinRequested_t> m_cbJoinRequested;
    private CallResult<LobbyMatchList_t>  m_crLobbyMatchList;
    private Callback<LobbyChatMsg_t>      m_cbChatMsg;
    private Callback<LobbyChatUpdate_t>   m_cbChatUpdate;   // メンバー増減検知
    private Callback<LobbyDataUpdate_t>   m_cbDataUpdate;   // ゲーム開始通知

    // ----- Lobbyデータキー -----
    private const string KeyHostAddress  = "HostAddress";
    private const string KeyLobbyCode    = "LobbyCode";
    private const string KeyGameStarted  = "GameStarted";

    // ----- 公開プロパティ -----
    public ulong  LobbyID          { get; private set; }
    public string LobbyCode        { get; private set; }
    public string LocalPersonaName { get; private set; }
    public bool   IsHost           { get; private set; }

    public bool IsBusy =>
        operationInProgress ||
        (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening);

    // ----- イベント -----
    /// <summary>ロビー作成完了（ホスト側）</summary>
    public event Action           OnLobbyReady;
    /// <summary>ロビー参加完了（クライアント側）</summary>
    public event Action           OnLobbyJoined;
    /// <summary>コード検索でロビーが見つからなかった</summary>
    public event Action<string>   OnJoinFailed;
    /// <summary>メンバー数が変わった</summary>
    public event Action           OnMembersChanged;
    /// <summary>ホストがゲーム開始した（全員通知）</summary>
    public event Action           OnHostStartedGame;
    /// <summary>ロビーが解散した（ホストが退出）</summary>
    public event Action           OnLobbyDisbanded;
    /// <summary>Busy状態変化</summary>
    public event Action<bool>     BusyStateChanged;

    // ----- 内部状態 -----
    private bool   operationInProgress;
    private string cachedHostAddress;

    // クライアント接続処理の多重起動ガード。
    // LobbyDataUpdateはメンバー退出など何度でも発火するため、
    // ガード無しだと接続ハンドシェイク中にShutdown→再接続が走って接続できない。
    private bool clientStartRequested;

    // 接続試行ごとの診断・タイムアウトコルーチン（古い試行のタイマーが
    // 新しい試行を殺さないよう、参照を持って明示的に止める）
    private Coroutine connectionDiagCoroutine;
    private Coroutine connectionTimeoutCoroutine;

    // ----- シングルトン -----
    private static SteamLobby instance;
    public static SteamLobby Instance
    {
        get
        {
            if (instance == null) instance = FindAnyObjectByType<SteamLobby>();
            return instance;
        }
    }

    // =========================================================
    // Unity ライフサイクル
    // =========================================================

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

        // マルチプレイ必須：ウィンドウが非フォーカスでも動かし続ける。
        // これが無いと、同じPCでの2インスタンステストや裏に回したホストが
        // Steamコールバックのポンプを止めてしまい、接続要求を受け付けられない。
        Application.runInBackground = true;
    }

    private void Start()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamLobby] SteamManagerが初期化されていません");
            return;
        }

        m_crLobbyCreated   = CallResult<LobbyCreated_t>.Create(OnCreateLobbyResult);
        m_cbLobbyEnter     = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        m_cbJoinRequested  = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        m_crLobbyMatchList = CallResult<LobbyMatchList_t>.Create(OnLobbyMatchList);
        m_cbChatMsg        = Callback<LobbyChatMsg_t>.Create(OnLobbyChatMessage);
        m_cbChatUpdate     = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
        m_cbDataUpdate     = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);

        SteamNetworkingUtils.InitRelayNetworkAccess();
        LocalPersonaName = SteamFriends.GetPersonaName();

        Debug.Log($"[SteamLobby] Steam初期化成功: {LocalPersonaName} ({SteamUser.GetSteamID()})");
    }

    // =========================================================
    // 公開API - ロビー操作
    // =========================================================

    /// <summary>Steam Lobbyを作成する（ホスト用）。完了後 OnLobbyReady が発火。</summary>
    public void CreateLobby()
    {
        if (IsBusy)
        {
            Debug.LogWarning("[SteamLobby] 操作中のため作成を無視");
            return;
        }
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamLobby] Steam未初期化");
            return;
        }

        IsHost = true;
        SetBusy(true);

        SteamAPICall_t handle = SteamMatchmaking.CreateLobby(
            ELobbyType.k_ELobbyTypePublic, maxMembers);
        m_crLobbyCreated.Set(handle);

        Debug.Log("[SteamLobby] ロビー作成開始");
    }

    /// <summary>部屋コードでロビーを検索して参加する（クライアント用）。</summary>
    public void JoinLobbyWithCode(string code)
    {
        if (IsBusy)
        {
            Debug.LogWarning("[SteamLobby] 操作中のため検索を無視");
            return;
        }
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamLobby] Steam未初期化");
            return;
        }
        if (string.IsNullOrWhiteSpace(code))
        {
            OnJoinFailed?.Invoke("コードが空です");
            return;
        }

        IsHost = false;
        string target = code.ToUpperInvariant().Trim();
        SetBusy(true);

        SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
        SteamMatchmaking.AddRequestLobbyListStringFilter(KeyLobbyCode, target, ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListResultCountFilter(1);

        SteamAPICall_t handle = SteamMatchmaking.RequestLobbyList();
        m_crLobbyMatchList.Set(handle);

        Debug.Log($"[SteamLobby] コード検索: {target}");
    }

    /// <summary>LobbyIDを直接指定して参加（Steam招待など）。</summary>
    public void JoinLobby(CSteamID lobbyID)
    {
        if (!SteamManager.Initialized) return;
        IsHost = false;
        SetBusy(true);
        SteamMatchmaking.JoinLobby(lobbyID);
        Debug.Log($"[SteamLobby] ロビー直接参加: {lobbyID.m_SteamID}");
    }

    /// <summary>
    /// ホストがゲームを開始する。
    /// Steam lobby dataで全クライアントに通知後、StartHost→GameRoom遷移。
    /// </summary>
    public void StartGame()
    {
        if (!IsHost)
        {
            Debug.LogWarning("[SteamLobby] ホスト以外はStartGameできません");
            return;
        }
        if (LobbyID == 0)
        {
            Debug.LogError("[SteamLobby] ロビー未参加");
            return;
        }

        // ConnectionApprovalは使わない。
        // - 最大人数はSteamロビーのmaxMembersで制限済み
        // - プレイヤー生成はAutoSpawnPlayerPrefabClientSide＋PlayerPrefabで自動
        // 承認ハンドシェイクを挟むとシーン遷移中に接続が固まることがあるため無効化する。
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.NetworkConfig.ConnectionApproval = false;
            nm.ConnectionApprovalCallback = null;
        }

        // 注意: クライアントへの開始通知(GameStarted=1)は、
        // ホストがStartHost()でリッスンを開始した「後」に行う。
        // 先に通知するとクライアントがホスト未リッスン状態で接続を試み、
        // P2P接続が確立できずタイムアウトする。
        StartCoroutine(StartHostRoutine());
    }

    /// <summary>ロビーから退出する。ホストが退出するとロビーは解散。</summary>
    public void LeaveLobby()
    {
        if (LobbyID != 0)
        {
            SteamMatchmaking.LeaveLobby(new CSteamID(LobbyID));
            LobbyID = 0;
            LobbyCode = null;
        }

        IsHost = false;
        cachedHostAddress = null;

        StopConnectionWatchers();
        clientStartRequested = false;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            nm.Shutdown();
        }

        SetBusy(false);
    }

    /// <summary>現在のロビーメンバー一覧を返す。</summary>
    public List<(CSteamID id, string name)> GetLobbyMembers()
    {
        var result = new List<(CSteamID, string)>();
        if (LobbyID == 0) return result;

        var lid = new CSteamID(LobbyID);
        int count = SteamMatchmaking.GetNumLobbyMembers(lid);
        for (int i = 0; i < count; i++)
        {
            CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(lid, i);
            string name = SteamFriends.GetFriendPersonaName(member);
            if (string.IsNullOrWhiteSpace(name))
                name = member.m_SteamID.ToString();
            result.Add((member, name));
        }
        return result;
    }

    /// <summary>現在のロビーのホスト(オーナー)のSteamIDを返す。未参加なら無効値。</summary>
    public CSteamID GetLobbyOwner()
    {
        if (LobbyID == 0) return CSteamID.Nil;
        return SteamMatchmaking.GetLobbyOwner(new CSteamID(LobbyID));
    }

    /// <summary>Steamフレンド招待オーバーレイを開く。</summary>
    public void InviteFriends()
    {
        if (!SteamManager.Initialized || LobbyID == 0) return;
        SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(LobbyID));
    }

    // =========================================================
    // Steamコールバック
    // =========================================================

    private void OnCreateLobbyResult(LobbyCreated_t cb, bool ioFailure)
    {
        if (ioFailure || cb.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError("[SteamLobby] ロビー作成失敗");
            IsHost = false;
            SetBusy(false);
            OnJoinFailed?.Invoke("ロビーの作成に失敗しました");
            return;
        }

        LobbyID   = cb.m_ulSteamIDLobby;
        LobbyCode = GenerateRoomCode();

        SteamMatchmaking.SetLobbyData(new CSteamID(LobbyID), KeyHostAddress, SteamUser.GetSteamID().ToString());
        SteamMatchmaking.SetLobbyData(new CSteamID(LobbyID), KeyLobbyCode,   LobbyCode);
        SteamMatchmaking.SetLobbyData(new CSteamID(LobbyID), KeyGameStarted, "0");

        SetBusy(false);

        Debug.Log($"[SteamLobby] ロビー作成成功 ID={LobbyID} Code={LobbyCode}");
        OnLobbyReady?.Invoke();
    }

    private void OnLobbyEntered(LobbyEnter_t cb)
    {
        var response = (EChatRoomEnterResponse)cb.m_EChatRoomEnterResponse;
        if (response != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            Debug.LogError($"[SteamLobby] ロビー入室失敗: {response}");
            SetBusy(false);
            OnJoinFailed?.Invoke($"ロビー入室失敗: {response}");
            return;
        }

        LobbyID = cb.m_ulSteamIDLobby;
        cachedHostAddress = SteamMatchmaking.GetLobbyData(new CSteamID(LobbyID), KeyHostAddress);
        LobbyCode = SteamMatchmaking.GetLobbyData(new CSteamID(LobbyID), KeyLobbyCode);

        Debug.Log($"[SteamLobby] ロビー入室成功 Host={cachedHostAddress} Code={LobbyCode}");

        // ホスト自身のコールバックはスキップ
        if (IsHost) return;

        SetBusy(false);
        OnLobbyJoined?.Invoke();
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t cb)
    {
        JoinLobby(cb.m_steamIDLobby);
    }

    private void OnLobbyMatchList(LobbyMatchList_t cb, bool ioFailure)
    {
        if (ioFailure || cb.m_nLobbiesMatching <= 0)
        {
            Debug.LogError("[SteamLobby] ロビーが見つかりません");
            SetBusy(false);
            OnJoinFailed?.Invoke("コードに一致するロビーが見つかりませんでした");
            return;
        }

        CSteamID lobbyID = SteamMatchmaking.GetLobbyByIndex(0);
        Debug.Log($"[SteamLobby] ロビー発見: {lobbyID.m_SteamID}");
        SteamMatchmaking.JoinLobby(lobbyID);
    }

    private void OnLobbyChatUpdate(LobbyChatUpdate_t cb)
    {
        if (cb.m_ulSteamIDLobby != LobbyID) return;

        var stateChange = (EChatMemberStateChange)cb.m_rgfChatMemberStateChange;
        var whoChanged  = new CSteamID(cb.m_ulSteamIDUserChanged);

        Debug.Log($"[SteamLobby] メンバー変化: {stateChange} / {SteamFriends.GetFriendPersonaName(whoChanged)}");

        // 退出したのがホストかどうか。
        // ※現在のロビーオーナーとの比較だけでは不十分：Steamはホスト退出時に
        //   オーナーを残りメンバーへ自動移譲するため、比較時点では既に別人になっている。
        //   参加時に記録したホストのSteamID（cachedHostAddress）で判定する。
        bool leftIsHost = whoChanged == SteamMatchmaking.GetLobbyOwner(new CSteamID(LobbyID));
        if (!leftIsHost &&
            !string.IsNullOrWhiteSpace(cachedHostAddress) &&
            ulong.TryParse(cachedHostAddress, out ulong hostSteamId))
        {
            leftIsHost = whoChanged.m_SteamID == hostSteamId;
        }

        // クライアントがHostの退出を検知→ロビー解散
        if (!IsHost &&
            (stateChange == EChatMemberStateChange.k_EChatMemberStateChangeLeft ||
             stateChange == EChatMemberStateChange.k_EChatMemberStateChangeDisconnected) &&
            leftIsHost)
        {
            Debug.LogWarning("[SteamLobby] ホストが退出しました — ロビー解散");
            LobbyID = 0;
            LobbyCode = null;

            // 接続試行中なら打ち切る（死んだホストへ再接続し続けない）
            AbortClientConnectionAttempt();

            SetBusy(false);
            OnLobbyDisbanded?.Invoke();
            return;
        }

        OnMembersChanged?.Invoke();
    }

    /// <summary>クライアントの接続試行を中断して状態を巻き戻す。</summary>
    private void AbortClientConnectionAttempt()
    {
        StopConnectionWatchers();
        clientStartRequested = false;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsClient && !nm.IsConnectedClient)
        {
            nm.OnClientConnectedCallback  -= OnClientConnected;
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
            nm.Shutdown();
        }
    }

    /// <summary>接続試行ごとの診断・タイムアウトコルーチンを止める。</summary>
    private void StopConnectionWatchers()
    {
        if (connectionDiagCoroutine != null)
        {
            StopCoroutine(connectionDiagCoroutine);
            connectionDiagCoroutine = null;
        }
        if (connectionTimeoutCoroutine != null)
        {
            StopCoroutine(connectionTimeoutCoroutine);
            connectionTimeoutCoroutine = null;
        }
    }

    private void OnLobbyDataUpdate(LobbyDataUpdate_t cb)
    {
        if (cb.m_ulSteamIDLobby != LobbyID) return;
        if (IsHost) return; // ホストは自分で知っている

        string started = SteamMatchmaking.GetLobbyData(new CSteamID(LobbyID), KeyGameStarted);
        if (started != "1") return;

        // 既に接続処理中／接続済みなら何もしない。
        // LobbyDataUpdateはメンバー退出等でも再発火するため、ガード無しだと
        // 接続中のNGOをShutdownして最初からやり直してしまう（接続不能の原因）。
        if (clientStartRequested) return;

        if (string.IsNullOrWhiteSpace(cachedHostAddress))
        {
            cachedHostAddress = SteamMatchmaking.GetLobbyData(new CSteamID(LobbyID), KeyHostAddress);
        }

        if (string.IsNullOrWhiteSpace(cachedHostAddress))
        {
            Debug.LogError("[SteamLobby] HostAddressが取得できません");
            return;
        }

        clientStartRequested = true;

        Debug.Log($"[SteamLobby] ホストがゲーム開始 → StartClient (Host={cachedHostAddress})");
        OnHostStartedGame?.Invoke();
        StartCoroutine(StartClientRoutine(cachedHostAddress));
    }

    private void OnLobbyChatMessage(LobbyChatMsg_t cb)
    {
        if (cb.m_ulSteamIDLobby != LobbyID) return;

        byte[] buf = new byte[4096];
        int len = SteamMatchmaking.GetLobbyChatEntry(
            new CSteamID(cb.m_ulSteamIDLobby),
            (int)cb.m_iChatID,
            out CSteamID sender,
            buf, buf.Length,
            out EChatEntryType _);

        if (len <= 0) return;
        string msg = Encoding.UTF8.GetString(buf, 0, len).TrimEnd('\0');
        string name = SteamFriends.GetFriendPersonaName(sender);
        Debug.Log($"[SteamLobby] チャット {name}: {msg}");
    }

    // =========================================================
    // NGO 開始ルーティン
    // =========================================================

    private IEnumerator StartHostRoutine()
    {
        yield return WaitForRelay();

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.isActiveAndEnabled)
        {
            SetBusy(false);
            yield break;
        }

        // トランスポートの接続ログを見るため Developer レベルにする
        nm.LogLevel = LogLevel.Developer;

        // シーンに空の設定要素が残っていると、SteamNetworkingSocketsTransportが
        // 無効な設定をCreateListenSocketP2Pへ渡してP2P待受を作れない。
        var transport = nm.NetworkConfig.NetworkTransport as SteamNetworkingSocketsTransport;
        if (transport == null)
        {
            Debug.LogError("[SteamLobby] SteamNetworkingSocketsTransportが見つかりません");
            SetBusy(false);
            yield break;
        }
        transport.options = Array.Empty<SteamNetworkingConfigValue_t>();

        bool started = nm.StartHost();
        Debug.Log($"[SteamLobby] StartHost: {started}");

        if (!started)
        {
            SetBusy(false);
            yield break;
        }

        // ホスト側診断: クライアント接続/切断とシーン同期イベントをログ出力
        nm.OnClientConnectedCallback    -= OnHostSawClientConnected;
        nm.OnClientConnectedCallback    += OnHostSawClientConnected;
        nm.OnClientDisconnectCallback   -= OnHostSawClientDisconnected;
        nm.OnClientDisconnectCallback   += OnHostSawClientDisconnected;
        if (nm.SceneManager != null)
        {
            nm.SceneManager.OnSceneEvent     -= OnSceneEventDiag;
            nm.SceneManager.OnSceneEvent     += OnSceneEventDiag;
            // ホストがGameRoomを「完全に読み込んでから」クライアントへ通知する。
            // 読み込み中にクライアントが接続するとホストのPollEventが走らず
            // P2P接続要求を取りこぼすため。
            nm.SceneManager.OnLoadComplete   -= OnHostSceneLoaded;
            nm.SceneManager.OnLoadComplete   += OnHostSceneLoaded;
        }

        Debug.Log("[SteamLobby] GameRoomを読み込み開始（読み込み完了後にクライアントへ通知）");
        nm.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

    /// <summary>
    /// ホストのGameRoom読み込み完了時に、初めてクライアントへゲーム開始を通知する。
    /// </summary>
    private void OnHostSceneLoaded(ulong clientId, string sceneName, LoadSceneMode mode)
    {
        if (sceneName != gameSceneName) return;

        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // ホスト自身の読み込み完了のみを対象にする
        if (clientId != nm.LocalClientId) return;

        nm.SceneManager.OnLoadComplete -= OnHostSceneLoaded;

        if (IsHost && LobbyID != 0)
        {
            SteamMatchmaking.SetLobbyData(new CSteamID(LobbyID), KeyGameStarted, "1");
            Debug.Log("[SteamLobby] ホストGameRoom読み込み完了 → GameStarted=1 を通知");
        }
    }

    private IEnumerator StartClientRoutine(string hostAddress)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            clientStartRequested = false;
            yield break;
        }

        if (nm.IsListening)
        {
            nm.Shutdown();
            yield return new WaitForSeconds(0.2f);
        }

        yield return WaitForRelay();

        nm = NetworkManager.Singleton;
        if (nm == null || !nm.isActiveAndEnabled)
        {
            clientStartRequested = false;
            SetBusy(false);
            yield break;
        }

        var transport = nm.NetworkConfig.NetworkTransport as SteamNetworkingSocketsTransport;
        if (transport == null)
        {
            Debug.LogError("[SteamLobby] SteamNetworkingSocketsTransportが見つかりません");
            clientStartRequested = false;
            SetBusy(false);
            yield break;
        }

        if (!ulong.TryParse(hostAddress, out ulong hostSteamId))
        {
            Debug.LogError($"[SteamLobby] hostAddressのパース失敗: {hostAddress}");
            clientStartRequested = false;
            SetBusy(false);
            yield break;
        }

        transport.ConnectToSteamID = hostSteamId;
        // カスタム設定は使用しない。Unityシーンに残った空要素を渡すと、
        // ConnectP2Pが正常に接続を開始できない。
        transport.options = Array.Empty<SteamNetworkingConfigValue_t>();

        Debug.Log($"[SteamLobby] StartClient準備: ConnectToSteamID={hostSteamId}");

        // トランスポートの接続ログを見るため Developer レベルにする
        nm.LogLevel = LogLevel.Developer;

        nm.OnClientConnectedCallback    -= OnClientConnected;
        nm.OnClientConnectedCallback    += OnClientConnected;
        nm.OnClientDisconnectCallback   -= OnClientDisconnected;
        nm.OnClientDisconnectCallback   += OnClientDisconnected;

        bool result = nm.StartClient();
        Debug.Log($"[SteamLobby] StartClient: {result}");

        if (!result)
        {
            Debug.LogError("[SteamLobby] StartClientに失敗");
            nm.OnClientConnectedCallback  -= OnClientConnected;
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
            clientStartRequested = false;
            SetBusy(false);
        }
        else
        {
            // シーン同期イベントの診断ログを仕込む（StartClient後にSceneManagerが存在）
            if (nm.SceneManager != null)
            {
                nm.SceneManager.OnSceneEvent -= OnSceneEventDiag;
                nm.SceneManager.OnSceneEvent += OnSceneEventDiag;
                Debug.Log("[SteamLobby] SceneManager診断フック登録");
            }
            else
            {
                Debug.LogWarning("[SteamLobby] StartClient後もSceneManagerがnull");
            }

            // 前回の試行のタイマーが残っていたら止めてから張り直す
            StopConnectionWatchers();
            connectionDiagCoroutine    = StartCoroutine(ConnectionDiag());
            connectionTimeoutCoroutine = StartCoroutine(ConnectionTimeout(30f));
        }
    }

    /// <summary>NGOのシーン同期イベントを逐次ログ出力する（診断用）。</summary>
    private void OnSceneEventDiag(SceneEvent e)
    {
        Debug.Log($"[SteamLobby][Scene] {e.SceneEventType} scene={e.SceneName} clientId={e.ClientId}");
    }

    /// <summary>接続状態を数秒間ログ出力して、どこで止まっているか切り分ける（診断用）。</summary>
    private IEnumerator ConnectionDiag()
    {
        for (int i = 0; i < 15; i++)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) yield break;
            Debug.Log($"[SteamLobby][Diag] t={i}s IsClient={nm.IsClient} " +
                      $"IsConnectedClient={nm.IsConnectedClient} " +
                      $"Scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            if (nm.IsConnectedClient) yield break;
            yield return new WaitForSeconds(2f);
        }
    }

    private IEnumerator WaitForRelay()
    {
        float elapsed = 0f;
        const float timeout = 10f;
        while (elapsed < timeout)
        {
            var avail = SteamNetworkingUtils.GetRelayNetworkStatus(out _);
            if (avail == ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Current)
            {
                Debug.Log("[SteamLobby] Steamリレー準備完了");
                yield break;
            }
            if (avail == ESteamNetworkingAvailability.k_ESteamNetworkingAvailability_Failed)
            {
                Debug.LogWarning("[SteamLobby] Steamリレー失敗 — 直接接続で試みます");
                yield break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        Debug.LogWarning("[SteamLobby] Steamリレー待機タイムアウト");
    }

    // ホスト側診断: クライアントの接続/切断を検知
    private void OnHostSawClientConnected(ulong clientId)
    {
        Debug.Log($"[SteamLobby][HOST] クライアント接続を検知 ClientID={clientId}");
    }

    private void OnHostSawClientDisconnected(ulong clientId)
    {
        string reason = NetworkManager.Singleton?.DisconnectReason ?? "(なし)";
        Debug.LogWarning($"[SteamLobby][HOST] クライアント切断を検知 ClientID={clientId} 理由={reason}");
    }

    private IEnumerator ConnectionTimeout(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsClient && !nm.IsConnectedClient)
        {
            Debug.LogError($"[SteamLobby] 接続タイムアウト ({seconds}秒)");
            nm.OnClientConnectedCallback  -= OnClientConnected;
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
            nm.Shutdown();
            clientStartRequested = false; // 次のLobbyDataUpdateで再挑戦できるように戻す
            SetBusy(false);
            SceneManager.LoadScene(menuSceneName);
        }
    }

    // =========================================================
    // NGO コールバック
    // =========================================================

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[SteamLobby] クライアント接続成功 ID={clientId}");
        // タイムアウト・診断タイマーだけ止める
        // （StopAllCoroutinesだと進行中の他の処理まで巻き込むため使わない）
        StopConnectionWatchers();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        string reason = NetworkManager.Singleton?.DisconnectReason ?? "(なし)";
        Debug.LogWarning($"[SteamLobby] クライアント切断 ID={clientId} 理由={reason}");

        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnClientConnectedCallback  -= OnClientConnected;
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        StopConnectionWatchers();
        clientStartRequested = false;

        StartCoroutine(ShutdownAndReturn());
    }

    private IEnumerator ShutdownAndReturn()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.Shutdown();
            yield return new WaitForSeconds(0.2f);
        }
        SetBusy(false);
        SceneManager.LoadScene(menuSceneName);
    }

    // =========================================================
    // 接続承認
    // =========================================================

    private void ApprovalCheck(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        int current = NetworkManager.Singleton.ConnectedClients.Count;
        Debug.Log($"[SteamLobby] ApprovalCheck: {current}/{maxMembers}");

        if (current >= maxMembers)
        {
            response.Approved = false;
            response.Pending  = false;
            return;
        }

        response.Approved          = true;
        response.CreatePlayerObject = true;
        response.PlayerPrefabHash  = null;
        response.Position          = null;
        response.Rotation          = null;
        response.Pending           = false;
    }

    // =========================================================
    // ユーティリティ
    // =========================================================

    private string GenerateRoomCode()
    {
        // iiwakekingと同様、紛らわしい文字を除外
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        char[] code = new char[5];
        for (int i = 0; i < code.Length; i++)
            code[i] = chars[UnityEngine.Random.Range(0, chars.Length)];
        return new string(code);
    }

    private void SetBusy(bool value)
    {
        if (operationInProgress == value) return;
        operationInProgress = value;
        BusyStateChanged?.Invoke(IsBusy);
    }
}
