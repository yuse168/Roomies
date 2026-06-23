using System.Collections;
using System.Collections.Generic;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Steam Lobbyの待機画面UI。
/// iiwakekingのNetUIManager(RenderLobby)を参考にRoomies向けに実装。
///
/// [Unityエディタでの設定]
/// MainMenuSteamシーンに LobbyPanel (Canvas以下のPanel) を作成し、
/// 以下のUI要素を割り当てること。
///   - lobbyPanel        : ロビーUI全体のルートObject（初期はSetActive(false)）
///   - partyCodeText     : TMP_Text — パーティーコード表示
///   - copyCodeButton    : Button   — コードをクリップボードへコピー
///   - copyFeedbackText  : TMP_Text — "Copied!" フィードバック（初期はSetActive(false)）
///   - playerListParent  : Transform — プレイヤー行を生成する親
///   - playerRowPrefab   : GameObject — プレイヤー行Prefab（TMP_Textを1つ持つ）
///   - startButton       : Button   — Start Game（ホストのみ表示）
///   - waitingText       : TMP_Text — "Waiting for host..."（クライアントのみ表示）
///   - leaveButton       : Button   — Leave Lobby
///   - inviteButton      : Button   — Invite Friends（ホストのみ表示・任意）
///   - joinErrorText     : TMP_Text — Join失敗メッセージ（MainMenu側のUIでもOK）
///   - mainMenuPanel     : GameObject — MainMenuのルートPanel（ロビー表示時に非表示にする）
/// </summary>
public class LobbyUIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject mainMenuPanel;

    [Header("Party Code")]
    [SerializeField] private TMP_Text partyCodeText;
    [SerializeField] private Button   copyCodeButton;
    [SerializeField] private TMP_Text copyFeedbackText;

    [Header("Player List")]
    [SerializeField] private Transform   playerListParent;
    // プレイヤー行はコードで生成するためPrefabは不要

    [Header("Controls")]
    [SerializeField] private Button   startButton;
    [SerializeField] private TMP_Text waitingText;
    [SerializeField] private Button   leaveButton;
    [SerializeField] private Button   inviteButton;

    [Header("Error")]
    [SerializeField] private TMP_Text joinErrorText;

    // 最低起動人数（ホストを含む）
    [SerializeField] private int minPlayersToStart = 2;

    private Coroutine refreshCoroutine;
    private Coroutine feedbackCoroutine;

    // =========================================================
    // Unity ライフサイクル
    // =========================================================

    private void Awake()
    {
        if (lobbyPanel != null)   lobbyPanel.SetActive(false);
        if (copyFeedbackText != null) copyFeedbackText.gameObject.SetActive(false);
        if (joinErrorText != null)    joinErrorText.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (SteamLobby.Instance == null) return;

        SteamLobby.Instance.OnLobbyReady      += HandleLobbyReady;
        SteamLobby.Instance.OnLobbyJoined     += HandleLobbyJoined;
        SteamLobby.Instance.OnJoinFailed       += HandleJoinFailed;
        SteamLobby.Instance.OnMembersChanged   += RefreshPlayerList;
        SteamLobby.Instance.OnHostStartedGame  += HandleGameStarting;
        SteamLobby.Instance.OnLobbyDisbanded   += HandleLobbyDisbanded;
    }

    private void OnDisable()
    {
        if (SteamLobby.Instance == null) return;

        SteamLobby.Instance.OnLobbyReady      -= HandleLobbyReady;
        SteamLobby.Instance.OnLobbyJoined     -= HandleLobbyJoined;
        SteamLobby.Instance.OnJoinFailed       -= HandleJoinFailed;
        SteamLobby.Instance.OnMembersChanged   -= RefreshPlayerList;
        SteamLobby.Instance.OnHostStartedGame  -= HandleGameStarting;
        SteamLobby.Instance.OnLobbyDisbanded   -= HandleLobbyDisbanded;
    }

    // =========================================================
    // イベントハンドラ
    // =========================================================

    private void HandleLobbyReady()
    {
        ShowLobbyUI(isHost: true);
    }

    private void HandleLobbyJoined()
    {
        ShowLobbyUI(isHost: false);
    }

    private void HandleJoinFailed(string reason)
    {
        ShowError(reason);
    }

    private void HandleGameStarting()
    {
        // クライアント側: NGO接続開始を待つのでUIはそのまま
        // 接続が確立するとNGOがGameRoomシーンを同期ロードする
        if (waitingText != null)
            waitingText.text = "接続中...";
        if (startButton != null)
            startButton.interactable = false;
    }

    private void HandleLobbyDisbanded()
    {
        HideLobbyUI();
        ShowError("ホストがロビーを解散しました");
    }

    // =========================================================
    // UI 表示制御
    // =========================================================

    private void ShowLobbyUI(bool isHost)
    {
        HideError();

        if (mainMenuPanel != null)  mainMenuPanel.SetActive(false);
        if (lobbyPanel != null)     lobbyPanel.SetActive(true);

        // パーティーコード
        if (partyCodeText != null)
            partyCodeText.text = SteamLobby.Instance.LobbyCode ?? "-----";

        // コピーボタン
        if (copyCodeButton != null)
        {
            copyCodeButton.onClick.RemoveAllListeners();
            copyCodeButton.onClick.AddListener(OnCopyCode);
        }

        // ホスト専用
        if (startButton != null)
        {
            startButton.gameObject.SetActive(isHost);
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(OnStartGame);
        }
        if (inviteButton != null)
        {
            inviteButton.gameObject.SetActive(isHost);
            inviteButton.onClick.RemoveAllListeners();
            inviteButton.onClick.AddListener(() => SteamLobby.Instance.InviteFriends());
        }

        // クライアント専用
        if (waitingText != null)
            waitingText.gameObject.SetActive(!isHost);

        // Leave
        if (leaveButton != null)
        {
            leaveButton.onClick.RemoveAllListeners();
            leaveButton.onClick.AddListener(OnLeave);
        }

        // プレイヤーリスト更新ループ開始
        if (refreshCoroutine != null) StopCoroutine(refreshCoroutine);
        refreshCoroutine = StartCoroutine(RefreshLoop());
    }

    private void HideLobbyUI()
    {
        if (refreshCoroutine != null)
        {
            StopCoroutine(refreshCoroutine);
            refreshCoroutine = null;
        }

        if (lobbyPanel != null)    lobbyPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
    }

    // =========================================================
    // プレイヤーリスト
    // =========================================================

    private IEnumerator RefreshLoop()
    {
        while (true)
        {
            RefreshPlayerList();
            yield return new WaitForSeconds(1.5f);
        }
    }

    private void RefreshPlayerList()
    {
        if (playerListParent == null) return;

        // 既存行を削除
        foreach (Transform child in playerListParent)
            Destroy(child.gameObject);

        if (SteamLobby.Instance == null) return;

        List<(CSteamID id, string name)> members = SteamLobby.Instance.GetLobbyMembers();
        CSteamID mySteamId = SteamUser.GetSteamID();

        foreach (var (id, name) in members)
        {
            bool isSelf = (id == mySteamId);
            CreatePlayerRow(
                isSelf ? $"{name} (あなた)" : name,
                isSelf ? new Color(1f, 0.9f, 0.2f) : Color.white);
        }

        // Start ボタンの有効/無効
        if (startButton != null && SteamLobby.Instance.IsHost)
        {
            startButton.interactable = members.Count >= minPlayersToStart;
        }
    }

    /// <summary>プレイヤー一覧の1行をコードで生成する。</summary>
    private void CreatePlayerRow(string text, Color color)
    {
        var go = new GameObject("PlayerRow", typeof(RectTransform));
        go.transform.SetParent(playerListParent, false);

        var le = go.AddComponent<UnityEngine.UI.LayoutElement>();
        le.minHeight = 40f;
        le.preferredHeight = 40f;

        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.color = color;
        label.fontSize = 28f;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
    }

    // =========================================================
    // ボタン処理
    // =========================================================

    private void OnCopyCode()
    {
        string code = SteamLobby.Instance?.LobbyCode;
        if (string.IsNullOrEmpty(code)) return;

        GUIUtility.systemCopyBuffer = code;

        if (feedbackCoroutine != null) StopCoroutine(feedbackCoroutine);
        feedbackCoroutine = StartCoroutine(ShowCopyFeedback());
    }

    private IEnumerator ShowCopyFeedback()
    {
        if (copyFeedbackText == null) yield break;
        copyFeedbackText.gameObject.SetActive(true);
        copyFeedbackText.text = "コピーしました!";
        yield return new WaitForSeconds(2f);
        copyFeedbackText.gameObject.SetActive(false);
    }

    private void OnStartGame()
    {
        if (SteamLobby.Instance == null) return;
        if (!SteamLobby.Instance.IsHost) return;

        startButton.interactable = false;
        SteamLobby.Instance.StartGame();
    }

    private void OnLeave()
    {
        if (SteamLobby.Instance != null)
            SteamLobby.Instance.LeaveLobby();

        HideLobbyUI();
    }

    // =========================================================
    // エラー表示
    // =========================================================

    private void ShowError(string message)
    {
        if (joinErrorText == null) return;
        joinErrorText.text = message;
        joinErrorText.gameObject.SetActive(true);
    }

    private void HideError()
    {
        if (joinErrorText != null)
            joinErrorText.gameObject.SetActive(false);
    }
}
