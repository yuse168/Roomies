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

        // プレイヤーリスト更新ループ開始（開くたびに必ず1回組み直す）
        listSignature = null;
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

    // 中身が変わっていないのに1.5秒ごとに作り直すと、
    // 名札の入場アニメーションが永久にやり直しになるので署名で差分を見る。
    private string listSignature;

    private void RefreshPlayerList()
    {
        if (playerListParent == null) return;

        EnsureListLayout();

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

        // Start ボタンの有効/無効
        if (startButton != null && SteamLobby.Instance.IsHost)
        {
            startButton.interactable = members.Count >= minPlayersToStart;
        }

        string signature = BuildSignature(members, hostId);
        if (signature == listSignature) return;
        listSignature = signature;

        // 既存行を削除
        foreach (Transform child in playerListParent)
            Destroy(child.gameObject);

        int index = 0;
        foreach (var (id, name) in members)
        {
            bool isSelf = (id == mySteamId);
            bool isHost = (id == hostId);
            CreatePlayerRow(id, name, PlayerColors[index % PlayerColors.Length], isSelf, isHost);
            index++;
        }
    }

    /// <summary>
    /// メンバー構成とアバター読み込み状況を1本の文字列にする。
    /// アバターは非同期で後から届くので、届いたら作り直せるように署名に含める。
    /// </summary>
    private static string BuildSignature(
        List<(CSteamID id, string name)> members, CSteamID hostId)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append(hostId.m_SteamID);
        foreach (var (id, name) in members)
        {
            builder.Append('|').Append(id.m_SteamID).Append(':').Append(name);
            builder.Append(SteamAvatar.Get(id.m_SteamID) != null ? '+' : '-');
        }
        return builder.ToString();
    }

    /// <summary>プレイヤーの名札を画面下端に横一列で並べるレイアウトを保証する。</summary>
    private void EnsureListLayout()
    {
        if (playerListParent == null) return;

        var hlg = playerListParent.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        if (hlg == null)
        {
            // LayoutGroupは1オブジェクトに1つしか持てない。
            // シーン側にGrid/Vertical等が付いているとAddComponentがnullを返すので、
            // 先に既存のLayoutGroupを外してから付け替える。
            var existing = playerListParent.GetComponent<UnityEngine.UI.LayoutGroup>();
            if (existing != null) DestroyImmediate(existing);

            hlg = playerListParent.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        }
        if (hlg == null) return;

        hlg.childAlignment         = TextAnchor.MiddleCenter;
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        hlg.spacing = 18f;
    }

    /// <summary>
    /// プレイヤーの名札を1枚生成する（画面下端に横一列で並ぶ）。
    /// プレイヤーカラーのカプセル型の枠に、アバターと名前が入る。
    /// 白いカードの中の一覧行ではなく、部屋の手前に置かれた札として見せる。
    /// </summary>
    private void CreatePlayerRow(Steamworks.CSteamID steamId, string name, Color accent,
                                 bool isSelf, bool isHost)
    {
        const float plateHeight = 92f;
        const float edge        = 5f;    // プレイヤーカラーの枠の太さ
        const float avatarSize  = 72f;
        const float avatarLeft  = 10f;
        const float namePadding = 22f;

        string displayName = isSelf ? $"{name}（あなた）" : name;

        // --- 名札本体（枠＝プレイヤーカラー） ---
        var row = new GameObject("PlayerRow", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        row.transform.SetParent(playerListParent, false);
        var rowRt = (RectTransform)row.transform;

        var plate = row.GetComponent<UnityEngine.UI.Image>();
        plate.sprite = UITheme.PillSprite;
        plate.color  = accent;
        UITheme.SetCornerRadius(plate, plateHeight * 0.5f);
        UITheme.AddShadow(row, 0.42f, 8f);

        // --- 内側の面（子は親のImageより手前に描かれる） ---
        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        fillGo.transform.SetParent(row.transform, false);
        var fillRt = (RectTransform)fillGo.transform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(edge, edge);
        fillRt.offsetMax = new Vector2(-edge, -edge);

        var fill = fillGo.GetComponent<UnityEngine.UI.Image>();
        fill.sprite = UITheme.PillSprite;
        fill.color = isSelf
            ? Color.Lerp(UITheme.MenuInk, accent, 0.26f)   // 自分はプレイヤーカラー寄り
            : UITheme.MenuInk;
        fill.raycastTarget = false;
        UITheme.SetCornerRadius(fill, plateHeight * 0.5f - edge);

        // --- 名前 ---
        float contentLeft = avatarLeft + avatarSize + 16f;

        var label = UITheme.Label(row.transform, "Name", displayName,
            26f, isSelf ? UITheme.Sun : Color.white, TextAlignmentOptions.Left, bold: true);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;

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
            const float tagGap = 14f;
            tagWidth = 74f + tagGap;

            var tagGo = new GameObject("HostTag", typeof(RectTransform),
                typeof(UnityEngine.UI.Image));
            tagGo.transform.SetParent(row.transform, false);
            var tagRt = (RectTransform)tagGo.transform;
            tagRt.anchorMin = new Vector2(0f, 0.5f);
            tagRt.anchorMax = new Vector2(0f, 0.5f);
            tagRt.pivot     = new Vector2(0f, 0.5f);
            tagRt.anchoredPosition = new Vector2(contentLeft + nameWidth + tagGap, 0f);
            tagRt.sizeDelta = new Vector2(74f, 32f);

            var tagImg = tagGo.GetComponent<UnityEngine.UI.Image>();
            tagImg.sprite = UITheme.PillSprite;
            tagImg.color  = UITheme.Sun;
            tagImg.raycastTarget = false;
            UITheme.SetCornerRadius(tagImg, 16f);

            var tagText = UITheme.Label(tagGo.transform, "Text", "HOST",
                16f, new Color(0.18f, 0.10f, 0.02f), TextAlignmentOptions.Center, bold: true);
            tagText.raycastTarget = false;
            var ttRt = tagText.rectTransform;
            ttRt.anchorMin = Vector2.zero;
            ttRt.anchorMax = Vector2.one;
            ttRt.offsetMin = Vector2.zero;
            ttRt.offsetMax = Vector2.zero;
        }

        // --- アバター（プレイヤーカラーのリングで丸く切り抜く） ---
        var frameGo = new GameObject("AvatarFrame", typeof(RectTransform),
            typeof(UnityEngine.UI.Image));
        frameGo.transform.SetParent(row.transform, false);
        var frameRt = (RectTransform)frameGo.transform;
        frameRt.anchorMin = new Vector2(0f, 0.5f);
        frameRt.anchorMax = new Vector2(0f, 0.5f);
        frameRt.pivot     = new Vector2(0f, 0.5f);
        frameRt.anchoredPosition = new Vector2(avatarLeft + avatarSize * 0.5f, 0f);
        frameRt.sizeDelta = new Vector2(avatarSize, avatarSize);

        var frameImg = frameGo.GetComponent<UnityEngine.UI.Image>();
        frameImg.sprite = UITheme.PillSprite;
        frameImg.color  = accent;
        frameImg.raycastTarget = false;
        UITheme.SetCornerRadius(frameImg, avatarSize * 0.5f);

        var mask = frameGo.AddComponent<UnityEngine.UI.Mask>();
        mask.showMaskGraphic = true;

        var avatarGo = new GameObject("Avatar", typeof(RectTransform),
            typeof(UnityEngine.UI.Image));
        avatarGo.transform.SetParent(frameGo.transform, false);
        var avatarRt = (RectTransform)avatarGo.transform;
        avatarRt.anchorMin = Vector2.zero;
        avatarRt.anchorMax = Vector2.one;
        avatarRt.offsetMin = new Vector2(4f, 4f);   // 縁がプレイヤーカラーのリングになる
        avatarRt.offsetMax = new Vector2(-4f, -4f);

        var avatarImg = avatarGo.GetComponent<UnityEngine.UI.Image>();
        avatarImg.raycastTarget = false;
        var sprite = SteamAvatar.Get(steamId.m_SteamID);
        if (sprite != null)
        {
            avatarImg.sprite = sprite;
            avatarImg.color  = Color.white;
        }
        else
        {
            avatarImg.color = new Color(0f, 0f, 0f, 0.35f); // 読み込み中はリング色の窓
        }

        // --- 名札の幅を内容に合わせて確定（HorizontalLayoutGroupが中央に並べる） ---
        rowRt.sizeDelta = new Vector2(
            contentLeft + nameWidth + tagWidth + namePadding, plateHeight);
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
