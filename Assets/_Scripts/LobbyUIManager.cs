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

    // パーティーゲーム風の明るいプレイヤーカラー（接続順に割り当て）
    private static readonly Color[] PlayerColors =
    {
        new Color(0.40f, 0.85f, 0.34f), // ライム
        new Color(0.16f, 0.68f, 0.97f), // シアン
        new Color(0.98f, 0.34f, 0.48f), // ピンク
        new Color(1.00f, 0.62f, 0.12f), // オレンジ
        new Color(0.58f, 0.46f, 0.92f), // パープル
        new Color(1.00f, 0.85f, 0.20f), // イエロー
        new Color(0.20f, 0.85f, 0.75f), // ターコイズ
        new Color(0.95f, 0.45f, 0.85f), // マゼンタ
    };

    private void RefreshPlayerList()
    {
        if (playerListParent == null) return;

        // 既存行を削除
        foreach (Transform child in playerListParent)
            Destroy(child.gameObject);

        if (SteamLobby.Instance == null) return;

        List<(CSteamID id, string name)> members = SteamLobby.Instance.GetLobbyMembers();
        CSteamID mySteamId = SteamUser.GetSteamID();
        CSteamID hostId    = SteamLobby.Instance.GetLobbyOwner();

        int index = 0;
        foreach (var (id, name) in members)
        {
            bool isSelf = (id == mySteamId);
            bool isHost = (id == hostId);
            CreatePlayerRow(id, name, PlayerColors[index % PlayerColors.Length], isSelf, isHost);
            index++;
        }

        // Start ボタンの有効/無効
        if (startButton != null && SteamLobby.Instance.IsHost)
        {
            startButton.interactable = members.Count >= minPlayersToStart;
        }
    }

    /// <summary>
    /// プレイヤー一覧の1行を生成する（参考画像風）。
    /// [カラーバー | アバター | 名前＋タグ] の横並びカード。
    /// </summary>
    private void CreatePlayerRow(Steamworks.CSteamID steamId, string name, Color accent,
                                 bool isSelf, bool isHost)
    {
        // --- 行コンテナ（カード背景つき） ---
        var row = new GameObject("PlayerRow", typeof(RectTransform));
        row.transform.SetParent(playerListParent, false);

        var rowLe = row.AddComponent<UnityEngine.UI.LayoutElement>();
        rowLe.minHeight = rowLe.preferredHeight = 56f;

        var rowBg = row.AddComponent<UnityEngine.UI.Image>();
        rowBg.sprite = UITheme.RoundedSprite;
        rowBg.type   = UnityEngine.UI.Image.Type.Sliced;
        rowBg.pixelsPerUnitMultiplier = 2f;
        rowBg.color = new Color(1f, 1f, 1f, 0.06f); // 半透明の明るいカード

        var hlg = row.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        hlg.spacing                = 12f;
        hlg.padding                = new RectOffset(0, 14, 4, 4);
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;

        // --- 左端のカラーバー（プレイヤーカラー） ---
        var barGo = new GameObject("ColorBar", typeof(RectTransform));
        barGo.transform.SetParent(row.transform, false);
        var barLe = barGo.AddComponent<UnityEngine.UI.LayoutElement>();
        barLe.minWidth = barLe.preferredWidth = 8f;
        barLe.minHeight = barLe.preferredHeight = 56f;
        var barImg = barGo.AddComponent<UnityEngine.UI.Image>();
        barImg.sprite = UITheme.RoundedSprite;
        barImg.type   = UnityEngine.UI.Image.Type.Sliced;
        barImg.pixelsPerUnitMultiplier = 6f;
        barImg.color = accent;

        // --- アバター画像（丸枠風） ---
        var avatarGo = new GameObject("Avatar", typeof(RectTransform));
        avatarGo.transform.SetParent(row.transform, false);
        var avatarLe = avatarGo.AddComponent<UnityEngine.UI.LayoutElement>();
        avatarLe.minWidth = avatarLe.preferredWidth = 44f;
        avatarLe.minHeight = avatarLe.preferredHeight = 44f;

        var img = avatarGo.AddComponent<UnityEngine.UI.Image>();
        var sprite = SteamAvatar.Get(steamId.m_SteamID);
        if (sprite != null)
        {
            img.sprite = sprite;
            img.color  = Color.white;
        }
        else
        {
            img.color = accent * new Color(1f, 1f, 1f, 0.5f); // 読み込み中はカラーで代用
        }

        // --- 名前 ---
        var nameGo = new GameObject("Name", typeof(RectTransform));
        nameGo.transform.SetParent(row.transform, false);
        var nameLe = nameGo.AddComponent<UnityEngine.UI.LayoutElement>();
        nameLe.flexibleWidth = 1f;

        var label = nameGo.AddComponent<TextMeshProUGUI>();
        label.text = isSelf ? $"{name} (あなた)" : name;
        label.color = isSelf ? new Color(1f, 0.85f, 0.3f) : new Color(0.97f, 0.98f, 1f);
        label.fontSize = 26f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Left;
        label.textWrappingMode = TextWrappingModes.NoWrap;

        // --- HOSTタグ ---
        if (isHost)
        {
            var tagGo = new GameObject("HostTag", typeof(RectTransform));
            tagGo.transform.SetParent(row.transform, false);
            var tagLe = tagGo.AddComponent<UnityEngine.UI.LayoutElement>();
            tagLe.minWidth = tagLe.preferredWidth = 70f;
            var tagImg = tagGo.AddComponent<UnityEngine.UI.Image>();
            tagImg.sprite = UITheme.RoundedSprite;
            tagImg.type   = UnityEngine.UI.Image.Type.Sliced;
            tagImg.pixelsPerUnitMultiplier = 3f;
            tagImg.color = new Color(1f, 0.62f, 0.12f); // オレンジ

            var tagTextGo = new GameObject("Text", typeof(RectTransform));
            tagTextGo.transform.SetParent(tagGo.transform, false);
            var ttRt = tagTextGo.GetComponent<RectTransform>();
            ttRt.anchorMin = Vector2.zero; ttRt.anchorMax = Vector2.one;
            ttRt.offsetMin = Vector2.zero; ttRt.offsetMax = Vector2.zero;
            var tagText = tagTextGo.AddComponent<TextMeshProUGUI>();
            tagText.text = "HOST";
            tagText.color = new Color(0.12f, 0.1f, 0.05f);
            tagText.fontSize = 18f;
            tagText.fontStyle = FontStyles.Bold;
            tagText.alignment = TextAlignmentOptions.Center;
            tagText.textWrappingMode = TextWrappingModes.NoWrap;
        }
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
