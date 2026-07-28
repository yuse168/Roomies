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

        EnsureListLayout();

        // 既存行を削除
        foreach (Transform child in playerListParent)
            Destroy(child.gameObject);

        if (SteamLobby.Instance == null) return;
        if (!SteamManager.Initialized)
        {
            ShowError(SteamManager.InitializationError ?? "Steamとの接続が切れました");
            if (startButton != null) startButton.interactable = false;
            return;
        }

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

    /// <summary>プレイヤー一覧を左寄せで縦に積むレイアウトを保証する。</summary>
    private void EnsureListLayout()
    {
        if (playerListParent == null) return;

        var vlg = playerListParent.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        if (vlg == null)
        {
            // LayoutGroupは1オブジェクトに1つしか持てない。
            // シーン側にGrid/Horizontal等が付いているとAddComponentがnullを返すので、
            // 先に既存のLayoutGroupを外してから付け替える。
            var existing = playerListParent.GetComponent<UnityEngine.UI.LayoutGroup>();
            if (existing != null) DestroyImmediate(existing);

            vlg = playerListParent.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        }
        if (vlg == null) return;

        vlg.childAlignment         = TextAnchor.UpperLeft;
        vlg.childControlWidth      = false;
        vlg.childControlHeight     = false;
        vlg.childForceExpandWidth  = false;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 14f;
    }

    /// <summary>
    /// プレイヤー一覧の1行を生成する（名札バッジ風）。
    /// プレイヤーカラーの角丸フレーム付きアバターが左端にあり、
    /// その右に名前プレートが刺さっているデザイン。行は内容の幅ぶんだけで左寄せ。
    /// </summary>
    private void CreatePlayerRow(Steamworks.CSteamID steamId, string name, Color accent,
                                 bool isSelf, bool isHost)
    {
        const float rowHeight    = 64f;
        const float avatarSize   = 64f;
        const float plateHeight  = 50f;
        const float plateOverlap = 34f;  // プレートがアバターの下に潜り込む量

        string displayName = isSelf ? $"{name} (あなた)" : name;

        // --- 行コンテナ（背景なし・内容幅で左寄せ） ---
        var row = new GameObject("PlayerRow", typeof(RectTransform));
        row.transform.SetParent(playerListParent, false);
        var rowRt = (RectTransform)row.transform;

        // --- 名前プレート（アバターの後ろから右へ伸びる） ---
        var plateGo = new GameObject("Plate", typeof(RectTransform));
        plateGo.transform.SetParent(row.transform, false);
        var plateRt = plateGo.GetComponent<RectTransform>();
        plateRt.anchorMin = new Vector2(0f, 0.5f);
        plateRt.anchorMax = new Vector2(0f, 0.5f);
        plateRt.pivot     = new Vector2(0f, 0.5f);
        plateRt.anchoredPosition = new Vector2(plateOverlap, 0f);

        var plateImg = plateGo.AddComponent<UnityEngine.UI.Image>();
        plateImg.sprite = UITheme.RoundedSprite;
        plateImg.type   = UnityEngine.UI.Image.Type.Sliced;
        plateImg.pixelsPerUnitMultiplier = 2f;
        plateImg.color = isSelf
            ? new Color(accent.r, accent.g, accent.b, 0.22f)   // 自分はプレイヤーカラーで薄く強調
            : new Color(1f, 1f, 1f, 0.07f);
        UITheme.AddShadow(plateGo);

        // --- 名前（プレート内・アバターを避けた位置から） ---
        float contentLeft = (avatarSize - plateOverlap) + 14f;

        var label = UITheme.Label(plateGo.transform, "Name", displayName,
            26f, isSelf ? UITheme.Gold : UITheme.TextMain, TextAlignmentOptions.Left, bold: true);
        label.textWrappingMode = TextWrappingModes.NoWrap;

        float nameWidth = label.GetPreferredValues(displayName).x;

        var nameRt = label.rectTransform;
        nameRt.anchorMin = new Vector2(0f, 0f);
        nameRt.anchorMax = new Vector2(0f, 1f);
        nameRt.pivot     = new Vector2(0f, 0.5f);
        nameRt.anchoredPosition = new Vector2(contentLeft, 0f);
        nameRt.sizeDelta = new Vector2(nameWidth + 4f, 0f);

        // --- HOSTタグ（名前の右） ---
        float tagWidth = 0f;
        if (isHost)
        {
            const float tagGap = 12f;
            tagWidth = 62f + tagGap;

            var tagGo = new GameObject("HostTag", typeof(RectTransform));
            tagGo.transform.SetParent(plateGo.transform, false);
            var tagRt = tagGo.GetComponent<RectTransform>();
            tagRt.anchorMin = new Vector2(0f, 0.5f);
            tagRt.anchorMax = new Vector2(0f, 0.5f);
            tagRt.pivot     = new Vector2(0f, 0.5f);
            tagRt.anchoredPosition = new Vector2(contentLeft + nameWidth + tagGap, 0f);
            tagRt.sizeDelta = new Vector2(62f, 26f);

            var tagImg = tagGo.AddComponent<UnityEngine.UI.Image>();
            tagImg.sprite = UITheme.RoundedSprite;
            tagImg.type   = UnityEngine.UI.Image.Type.Sliced;
            tagImg.pixelsPerUnitMultiplier = 4f;
            tagImg.color = UITheme.Accent;

            var tagText = UITheme.Label(tagGo.transform, "Text", "HOST",
                15f, new Color(0.12f, 0.10f, 0.05f), TextAlignmentOptions.Center, bold: true);
            var ttRt = tagText.rectTransform;
            ttRt.anchorMin = Vector2.zero;
            ttRt.anchorMax = Vector2.one;
            ttRt.offsetMin = Vector2.zero;
            ttRt.offsetMax = Vector2.zero;
        }

        // プレートの幅を内容に合わせて確定
        float plateWidth = contentLeft + nameWidth + tagWidth + 20f;
        plateRt.sizeDelta = new Vector2(plateWidth, plateHeight);

        // --- アバターバッジ（プレイヤーカラーの角丸フレーム、プレートの上に重なる） ---
        var frameGo = new GameObject("AvatarFrame", typeof(RectTransform));
        frameGo.transform.SetParent(row.transform, false);
        var frameRt = frameGo.GetComponent<RectTransform>();
        frameRt.anchorMin = new Vector2(0f, 0.5f);
        frameRt.anchorMax = new Vector2(0f, 0.5f);
        frameRt.pivot     = new Vector2(0f, 0.5f);
        frameRt.anchoredPosition = Vector2.zero;
        frameRt.sizeDelta = new Vector2(avatarSize, avatarSize);

        var frameImg = frameGo.AddComponent<UnityEngine.UI.Image>();
        frameImg.sprite = UITheme.RoundedSprite;
        frameImg.type   = UnityEngine.UI.Image.Type.Sliced;
        frameImg.pixelsPerUnitMultiplier = 1.8f;
        frameImg.color  = accent;
        UITheme.AddShadow(frameGo);

        // フレームの角丸でアバターを切り抜く
        var mask = frameGo.AddComponent<UnityEngine.UI.Mask>();
        mask.showMaskGraphic = true;

        var avatarGo = new GameObject("Avatar", typeof(RectTransform));
        avatarGo.transform.SetParent(frameGo.transform, false);
        var avatarRt = avatarGo.GetComponent<RectTransform>();
        avatarRt.anchorMin = Vector2.zero;
        avatarRt.anchorMax = Vector2.one;
        avatarRt.offsetMin = new Vector2(5f, 5f);   // 縁がプレイヤーカラーのリングになる
        avatarRt.offsetMax = new Vector2(-5f, -5f);

        var avatarImg = avatarGo.AddComponent<UnityEngine.UI.Image>();
        var sprite = SteamAvatar.Get(steamId.m_SteamID);
        if (sprite != null)
        {
            avatarImg.sprite = sprite;
            avatarImg.color  = Color.white;
        }
        else
        {
            avatarImg.color = new Color(0f, 0f, 0f, 0.35f); // 読み込み中はフレーム色の窓
        }

        // --- 行サイズ確定（内容幅ぶんだけ。VerticalLayoutGroupが左寄せで積む） ---
        rowRt.sizeDelta = new Vector2(plateOverlap + plateWidth, rowHeight);
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
