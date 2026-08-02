using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// MainMenuSteamのUIを「3Dの部屋が主役、UIは画面の縁に置かれた物」という
/// パーティーゲームの構成へ組み替える。
///
/// 設計方針：
///  ・画面の上半分にはUIを置かない（常に部屋とキャラが見える）
///  ・1画面1決定。巨大アクション1個＋小さい副操作。あそぶ→つくる/はいるの段階遷移
///  ・白いカードを使わない。3Dの上に乗るのは「濃い面＋太い白フチ」
///  ・ボタンは板ではなく厚みのあるカプセル（UITheme.StylePill）
///
/// シーンのGameObjectは追加・削除・改名しない。
/// MainMenuManager / LobbyUIManager の参照はインスペクタ配線なので、
/// RectTransformとGraphicの差し替えだけで見た目を作る。
/// </summary>
public class MenuThemer : MonoBehaviour
{
    // 1920x1080基準のレイアウト定数
    private const float Margin      = 86f;
    private const float FooterY     = 60f;
    private const float FooterH      = 58f;
    private const float ActionY     = 142f;
    private const float ActionH     = 112f;
    private const float TaglineY    = 274f;
    private const float TitleY      = 322f;

    private TMP_Text waitingText;
    private float pulseTime;

    private Button playButton;
    private Button playBackButton;
    private Button hostButton;
    private Button joinButton;

    private void Awake()
    {
        BuildMainMenu();
        BuildJoinPanel();
        BuildLobbyPanel();
        ApplyStatusTexts();
    }

    // ================================================================
    // メインメニュー
    // ================================================================

    private void BuildMainMenu()
    {
        var panel = UITheme.FindDeep("MainMenuPanel");
        if (panel == null) return;

        var root = (RectTransform)panel.transform;

        // 単色の板と中央の白カードを消して、3Dの部屋をそのまま見せる
        HideGraphic(FindIn(root, "BG"));
        HideGraphic(FindIn(root, "Card"));
        EnsureScrims(root);

        // --- ブランド（左上） ---
        TMP_Text brand = EnsureLabel(root, "BrandLabel", "ROOMIES", 34f, Color.white,
            TextAlignmentOptions.Left);
        SetTopLeft(brand.rectTransform, new Vector2(Margin, -60f), new Vector2(380f, 46f));
        brand.characterSpacing = 9f;
        Outline(brand, 0.22f);
        PopIn(brand.gameObject, 0f);

        // --- タイトル（左下ブロックの一番上） ---
        var title = FindIn(root, "Title");
        if (title != null && title.TryGetComponent(out TMP_Text titleText))
        {
            titleText.text = "3日後の<color=#FF5A9D>家賃</color>、\n払える？";
            titleText.fontStyle = FontStyles.Bold;
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 52f;
            titleText.fontSizeMax = 88f;
            titleText.alignment = TextAlignmentOptions.BottomLeft;
            titleText.color = Color.white;
            titleText.characterSpacing = -2f;
            titleText.lineSpacing = -10f;
            titleText.textWrappingMode = TextWrappingModes.NoWrap;
            SetBottomLeft(titleText.rectTransform,
                new Vector2(Margin - 4f, TitleY), new Vector2(860f, 210f));
            Outline(titleText, 0.24f);
            PopIn(title, 0.04f);
        }

        // --- タグライン ---
        var subtitle = FindIn(root, "Subtitle");
        if (subtitle != null && subtitle.TryGetComponent(out TMP_Text subtitleText))
        {
            subtitleText.text = "友達と稼いで、遊んで、家賃を払おう。";
            subtitleText.fontStyle = FontStyles.Bold;
            subtitleText.fontSize = 26f;
            subtitleText.enableAutoSizing = false;
            subtitleText.color = new Color(1f, 1f, 1f, 0.92f);
            subtitleText.alignment = TextAlignmentOptions.Left;
            SetBottomLeft(subtitleText.rectTransform,
                new Vector2(Margin, TaglineY), new Vector2(780f, 40f));
            Outline(subtitleText, 0.18f);
            PopIn(subtitle, 0.08f);
        }

        // --- アクション行（あそぶ ⇄ つくる/はいる/もどる） ---
        hostButton = FindIn(root, "HostButton")?.GetComponent<Button>();
        joinButton = FindIn(root, "JoinButton")?.GetComponent<Button>();

        LayoutAction(hostButton, Margin, 282f);
        LayoutAction(joinButton, Margin + 298f, 282f);
        SetButtonText(hostButton, "部屋をつくる");
        SetButtonText(joinButton, "部屋にはいる");
        if (hostButton != null) UITheme.StylePill(hostButton, UITheme.Pink, Color.white, 32f, 10f);
        if (joinButton != null) UITheme.StylePill(joinButton, UITheme.Cyan, Color.white, 32f, 10f);

        playBackButton = CreatePill(root, "PlayBackButton", "もどる",
            new Vector2(Margin + 596f, ActionY), new Vector2(132f, ActionH),
            UITheme.Grape, Color.white, 24f);
        playBackButton.onClick.AddListener(HidePlayOptions);

        playButton = CreatePill(root, "PlayButton", "▶  あそぶ",
            new Vector2(Margin, ActionY), new Vector2(480f, ActionH),
            UITheme.Pink, Color.white, 48f, 12f);
        playButton.onClick.AddListener(ShowPlayOptions);
        PopIn(playButton.gameObject, 0.12f);

        // --- フッター（設定・やめる） ---
        Button settingsButton = CreatePill(root, "SettingsButton", "設定",
            new Vector2(Margin, FooterY), new Vector2(190f, FooterH),
            UITheme.Grape, Color.white, 24f, 6f);

        var settings = GetComponent<MainMenuSettingsUI>();
        if (settings == null) settings = gameObject.AddComponent<MainMenuSettingsUI>();
        settings.Initialize(root);
        settingsButton.onClick.AddListener(settings.Open);
        PopIn(settingsButton.gameObject, 0.16f);

        var quit = FindIn(root, "QuitButton");
        if (quit != null && quit.TryGetComponent(out Button quitButton))
        {
            SetBottomLeft((RectTransform)quit.transform,
                new Vector2(Margin + 206f, FooterY), new Vector2(150f, FooterH));
            SetButtonText(quitButton, "やめる");
            UITheme.StylePill(quitButton, UITheme.Grape, new Color(1f, 0.78f, 0.80f), 24f, 6f);
            PopIn(quit, 0.18f);
        }

        // --- ステータス（下中央のトースト位置） ---
        var status = FindIn(root, "StatusText");
        if (status != null)
        {
            var rect = (RectTransform)status.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 66f);
            rect.sizeDelta = new Vector2(760f, 54f);
            if (status.TryGetComponent(out TMP_Text statusText))
                statusText.alignment = TextAlignmentOptions.Center;
        }

        HidePlayOptions();
    }

    private void LayoutAction(Button button, float x, float width)
    {
        if (button == null) return;
        SetBottomLeft((RectTransform)button.transform,
            new Vector2(x, ActionY), new Vector2(width, ActionH));
    }

    /// <summary>
    /// 左からのグラデと下からのグラデを重ねて、文字が乗る場所だけ暗くする。
    /// 一枚の単純なグラデ背景ではなく、3Dの部屋を読みやすくするための遮光。
    /// </summary>
    private static void EnsureScrims(RectTransform root)
    {
        if (FindIn(root, "MenuLeftScrim") == null)
        {
            var left = NewImage(root, "MenuLeftScrim");
            var rect = left.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0.68f, 1f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            left.sprite = UITheme.HorizontalGradient(
                new Color(0.06f, 0.03f, 0.14f, 0.88f),
                new Color(0.06f, 0.03f, 0.14f, 0f));
            left.transform.SetSiblingIndex(1);
        }

        if (FindIn(root, "MenuBottomScrim") == null)
        {
            var bottom = NewImage(root, "MenuBottomScrim");
            var rect = bottom.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 430f);
            bottom.sprite = UITheme.VerticalGradient(
                new Color(0.06f, 0.03f, 0.14f, 0f),
                new Color(0.06f, 0.03f, 0.14f, 0.66f));
            bottom.transform.SetSiblingIndex(2);
        }
    }

    // ================================================================
    // 参加コード入力
    // ================================================================

    private void BuildJoinPanel()
    {
        var joinPanel = UITheme.FindDeep("JoinPanel");
        if (joinPanel == null) return;
        var panelRoot = joinPanel.transform;

        // 背景は完全に隠さず、後ろの部屋が薄く見える暗幕にする
        var dim = FindIn(panelRoot, "BG");
        if (dim != null && dim.TryGetComponent(out Image dimImage))
        {
            dimImage.sprite = null;
            dimImage.color = new Color(0.06f, 0.03f, 0.13f, 0.66f);
            Stretch((RectTransform)dim.transform);
        }
        else if (joinPanel.TryGetComponent(out Image ownDim))
        {
            ownDim.sprite = null;
            ownDim.color = new Color(0.06f, 0.03f, 0.13f, 0.66f);
        }

        var card = FindIn(panelRoot, "Card");
        if (card == null) return;

        UITheme.MenuSurface(card, 42f, 7f);
        SetCenter((RectTransform)card.transform, Vector2.zero, new Vector2(780f, 460f));
        var cardRect = (RectTransform)card.transform;

        // レイアウトを確定させるため、関係要素をカードの中へ集める
        Reparent(FindIn(panelRoot, "Label"), cardRect);
        Reparent(UITheme.FindDeep("CodeInputField"), cardRect);
        Reparent(FindIn(panelRoot, "ConfirmJoinButton"), cardRect);
        Reparent(FindIn(panelRoot, "CancelButton"), cardRect);
        Reparent(FindIn(panelRoot, "ErrorText"), cardRect);

        var label = FindIn(cardRect, "Label");
        if (label != null && label.TryGetComponent(out TMP_Text labelText))
        {
            labelText.text = "ルームコードを入力";
            labelText.color = Color.white;
            labelText.fontStyle = FontStyles.Bold;
            labelText.fontSize = 32f;
            labelText.enableAutoSizing = false;
            labelText.alignment = TextAlignmentOptions.Center;
            SetTopCenter(labelText.rectTransform, new Vector2(0f, -44f), new Vector2(660f, 52f));
        }

        var inputGo = UITheme.FindDeep("CodeInputField");
        if (inputGo != null && inputGo.TryGetComponent(out TMP_InputField input))
        {
            SetTopCenter((RectTransform)inputGo.transform,
                new Vector2(0f, -120f), new Vector2(560f, 116f));

            if (inputGo.TryGetComponent(out Image inputBackground))
            {
                inputBackground.sprite = UITheme.RoundedSprite;
                inputBackground.color = UITheme.MenuInkSoft;
                UITheme.SetCornerRadius(inputBackground, 26f);
            }
            var inputOutline = inputGo.GetComponent<Outline>();
            if (inputOutline != null) inputOutline.enabled = false;

            if (input.textComponent != null)
            {
                input.textComponent.fontStyle = FontStyles.Bold;
                input.textComponent.color = UITheme.Sun;
                input.textComponent.fontSize = 54f;
                input.textComponent.enableAutoSizing = false;
                input.textComponent.characterSpacing = 16f;
                input.textComponent.alignment = TextAlignmentOptions.Center;
            }

            if (input.placeholder is TMP_Text placeholder)
            {
                placeholder.text = "- - - - -";
                placeholder.color = new Color(1f, 1f, 1f, 0.32f);
                placeholder.fontSize = 44f;
                placeholder.enableAutoSizing = false;
                placeholder.fontStyle = FontStyles.Bold;
                placeholder.characterSpacing = 12f;
                placeholder.alignment = TextAlignmentOptions.Center;
            }
        }

        var confirm = FindIn(cardRect, "ConfirmJoinButton")?.GetComponent<Button>();
        if (confirm != null)
        {
            SetBottomCenter((RectTransform)confirm.transform,
                new Vector2(105f, 44f), new Vector2(280f, 96f));
            SetButtonText(confirm, "はいる");
            UITheme.StylePill(confirm, UITheme.Pink, Color.white, 32f, 10f);
        }

        var cancel = FindIn(cardRect, "CancelButton")?.GetComponent<Button>();
        if (cancel != null)
        {
            SetBottomCenter((RectTransform)cancel.transform,
                new Vector2(-155f, 44f), new Vector2(200f, 96f));
            SetButtonText(cancel, "やめる");
            UITheme.StylePill(cancel, UITheme.Grape, Color.white, 28f, 10f);
        }

        var error = FindIn(cardRect, "ErrorText");
        if (error != null && error.TryGetComponent(out TMP_Text errorText))
        {
            errorText.fontSize = 24f;
            errorText.enableAutoSizing = false;
            errorText.alignment = TextAlignmentOptions.Center;
            SetBottomCenter(errorText.rectTransform, new Vector2(0f, 158f), new Vector2(660f, 40f));
        }
    }

    // ================================================================
    // ロビー = 「部屋の内見」画面
    // ================================================================

    private void BuildLobbyPanel()
    {
        var lobbyPanel = UITheme.FindDeep("LobbyPanel");
        if (lobbyPanel == null) return;
        var root = (RectTransform)lobbyPanel.transform;

        // 部屋を見せたままにするので、暗幕はごく薄く
        if (lobbyPanel.TryGetComponent(out Image lobbyBackground))
        {
            lobbyBackground.sprite = null;
            lobbyBackground.color = new Color(0.07f, 0.04f, 0.16f, 0.22f);
        }
        HideGraphic(FindIn(root, "BG"));
        HideGraphic(FindIn(root, "Card"));   // 白い大カードを廃止
        EnsureScrims(root);

        // --- 見出し（左上） ---
        var title = FindIn(root, "Title");
        if (title != null && title.TryGetComponent(out TMP_Text titleText))
        {
            titleText.text = "みんなの部屋";
            titleText.color = Color.white;
            titleText.fontStyle = FontStyles.Bold;
            titleText.fontSize = 46f;
            titleText.enableAutoSizing = false;
            titleText.alignment = TextAlignmentOptions.Left;
            SetTopLeft(titleText.rectTransform, new Vector2(Margin, -62f), new Vector2(700f, 76f));
            Outline(titleText, 0.22f);
        }

        // --- ルームコード（上中央の大きな面） ---
        var codeBox = UITheme.FindDeep("CodeBox");
        if (codeBox != null)
        {
            Reparent(codeBox, root);
            var boxRect = (RectTransform)codeBox.transform;
            SetTopCenter(boxRect, new Vector2(-90f, -56f), new Vector2(520f, 116f));
            UITheme.MenuSurface(codeBox, 30f, 5f);

            var codeLabel = UITheme.FindDeep("CodeLabel");
            if (codeLabel != null && codeLabel.TryGetComponent(out TMP_Text codeLabelText))
            {
                Reparent(codeLabel, boxRect);
                codeLabelText.text = "ルームコード";
                codeLabelText.fontSize = 20f;
                codeLabelText.enableAutoSizing = false;
                codeLabelText.fontStyle = FontStyles.Bold;
                codeLabelText.color = new Color(1f, 1f, 1f, 0.66f);
                codeLabelText.alignment = TextAlignmentOptions.Left;
                SetTopLeft(codeLabelText.rectTransform,
                    new Vector2(30f, -14f), new Vector2(300f, 26f));
            }

            var codeText = UITheme.FindDeep("PartyCodeText");
            if (codeText != null && codeText.TryGetComponent(out TMP_Text code))
            {
                Reparent(codeText, boxRect);
                code.fontStyle = FontStyles.Bold;
                code.color = UITheme.Sun;
                code.fontSize = 56f;
                code.enableAutoSizing = false;
                code.characterSpacing = 14f;
                code.alignment = TextAlignmentOptions.Center;
                SetBottomCenter(code.rectTransform, new Vector2(0f, 8f), new Vector2(470f, 74f));
            }
        }

        var copyButton = UITheme.FindDeep("CopyButton")?.GetComponent<Button>();
        if (copyButton != null)
        {
            Reparent(copyButton.gameObject, root);
            SetTopCenter((RectTransform)copyButton.transform,
                new Vector2(270f, -60f), new Vector2(160f, 88f));
            SetButtonText(copyButton, "コピー");
            UITheme.StylePill(copyButton, UITheme.Cyan, Color.white, 26f, 8f);
        }

        var feedback = UITheme.FindDeep("CopyFeedbackText");
        if (feedback != null && feedback.TryGetComponent(out TMP_Text feedbackText))
        {
            Reparent(feedback, root);
            feedbackText.fontSize = 24f;
            feedbackText.enableAutoSizing = false;
            feedbackText.alignment = TextAlignmentOptions.Center;
            SetTopCenter(feedbackText.rectTransform, new Vector2(0f, -190f), new Vector2(560f, 40f));
        }

        // --- メンバー（下端に横一列の名札） ---
        var playersLabel = UITheme.FindDeep("PlayersLabel");
        if (playersLabel != null && playersLabel.TryGetComponent(out TMP_Text players))
        {
            Reparent(playersLabel, root);
            players.text = "いま部屋にいるメンバー";
            players.color = new Color(1f, 1f, 1f, 0.86f);
            players.fontStyle = FontStyles.Bold;
            players.fontSize = 24f;
            players.enableAutoSizing = false;
            players.alignment = TextAlignmentOptions.Center;
            SetBottomCenter(players.rectTransform, new Vector2(0f, 258f), new Vector2(760f, 36f));
            Outline(players, 0.2f);
        }

        var listParent = UITheme.FindDeep("PlayerListParent");
        if (listParent != null)
        {
            Reparent(listParent, root);
            SetBottomCenter((RectTransform)listParent.transform,
                new Vector2(0f, 148f), new Vector2(1560f, 100f));
        }

        // --- 操作 ---
        var start = UITheme.FindDeep("StartButton")?.GetComponent<Button>();
        if (start != null)
        {
            Reparent(start.gameObject, root);
            SetBottomRight((RectTransform)start.transform,
                new Vector2(-Margin - 10f, 58f), new Vector2(400f, 108f));
            SetButtonText(start, "ゲーム開始");
            UITheme.StylePill(start, UITheme.Pink, Color.white, 38f, 11f);
        }

        var leave = UITheme.FindDeep("LeaveButton")?.GetComponent<Button>();
        if (leave != null)
        {
            Reparent(leave.gameObject, root);
            SetBottomLeft((RectTransform)leave.transform,
                new Vector2(Margin + 10f, 58f), new Vector2(200f, 80f));
            SetButtonText(leave, "退出");
            UITheme.StylePill(leave, UITheme.Grape, new Color(1f, 0.76f, 0.78f), 26f, 8f);
        }

        var invite = UITheme.FindDeep("InviteButton")?.GetComponent<Button>();
        if (invite != null)
        {
            Reparent(invite.gameObject, root);
            SetBottomLeft((RectTransform)invite.transform,
                new Vector2(Margin + 230f, 58f), new Vector2(320f, 80f));
            SetButtonText(invite, "フレンドを招待");
            UITheme.StylePill(invite, UITheme.Cyan, Color.white, 26f, 8f);
        }

        var waiting = UITheme.FindDeep("WaitingText");
        if (waiting != null && waiting.TryGetComponent(out TMP_Text waitText))
        {
            Reparent(waiting, root);
            waitingText = waitText;
            waitText.color = new Color(1f, 1f, 1f, 0.9f);
            waitText.fontStyle = FontStyles.Bold;
            waitText.fontSize = 28f;
            waitText.enableAutoSizing = false;
            waitText.alignment = TextAlignmentOptions.Center;
            SetBottomCenter(waitText.rectTransform, new Vector2(0f, 72f), new Vector2(560f, 52f));
            Outline(waitText, 0.2f);
        }
    }

    private void ApplyStatusTexts()
    {
        SetTextStyle("StatusText", UITheme.Sun);
        SetTextStyle("ErrorText", UITheme.Red);
        SetTextStyle("CopyFeedbackText", UITheme.Lime);
    }

    // ================================================================
    // あそぶ ⇄ つくる/はいる の段階遷移
    // ================================================================

    private void ShowPlayOptions()
    {
        SetButtonVisible(playButton, false);
        SetButtonVisible(hostButton, true);
        SetButtonVisible(joinButton, true);
        SetButtonVisible(playBackButton, true);
        PopIn(hostButton?.gameObject, 0f);
        PopIn(joinButton?.gameObject, 0.05f);
        PopIn(playBackButton?.gameObject, 0.1f);
        if (EventSystem.current != null && hostButton != null)
            EventSystem.current.SetSelectedGameObject(hostButton.gameObject);
    }

    private void HidePlayOptions()
    {
        SetButtonVisible(playButton, true);
        SetButtonVisible(hostButton, false);
        SetButtonVisible(joinButton, false);
        SetButtonVisible(playBackButton, false);
        if (EventSystem.current != null && playButton != null)
            EventSystem.current.SetSelectedGameObject(playButton.gameObject);
    }

    private static void SetButtonVisible(Button button, bool visible)
    {
        if (button == null) return;
        CanvasGroup group = button.GetComponent<CanvasGroup>();
        if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    // ================================================================
    // 生成・スタイルのヘルパー
    // ================================================================

    private static Button CreatePill(
        Transform parent, string name, string text,
        Vector2 position, Vector2 size,
        Color background, Color foreground, float fontSize, float depth = 8f)
    {
        var existing = FindIn(parent, name);
        GameObject go = existing;
        if (go == null)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
        }

        SetBottomLeft((RectTransform)go.transform, position, size);

        Button button = go.GetComponent<Button>();
        if (button == null) button = go.AddComponent<Button>();

        if (go.GetComponentInChildren<TMP_Text>(true) == null)
            UITheme.Label(go.transform, "Label", text, fontSize, foreground,
                TextAlignmentOptions.Center, true);
        else
            SetButtonText(button, text);

        UITheme.StylePill(button, background, foreground, fontSize, depth);
        return button;
    }

    private static Image NewImage(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private static void HideGraphic(GameObject go)
    {
        if (go == null || !go.TryGetComponent(out Image image)) return;
        image.sprite = null;
        image.color = Color.clear;
        image.enabled = false;
        image.raycastTarget = false;
    }

    private static void Reparent(GameObject go, Transform parent)
    {
        if (go == null || parent == null) return;
        if (go.transform.parent == parent) return;
        go.transform.SetParent(parent, false);
    }

    private static void SetButtonText(Button button, string value)
    {
        if (button == null) return;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = value;
    }

    private static void SetTextStyle(string name, Color color)
    {
        var go = UITheme.FindDeep(name);
        if (go == null || !go.TryGetComponent(out TMP_Text text)) return;
        text.color = color;
        text.fontStyle = FontStyles.Bold;
        Outline(text, 0.22f);
    }

    /// <summary>
    /// 3Dの上に乗る文字を読めるようにする縁取り。
    /// 初期非表示のパネル内のTMPはマテリアル未生成で即時適用できないので、
    /// 表示された瞬間に適用するコンポーネントに任せる。
    /// </summary>
    private static void Outline(TMP_Text text, float width)
    {
        if (text == null) return;
        var applier = text.GetComponent<UITextOutline>();
        if (applier == null) applier = text.gameObject.AddComponent<UITextOutline>();
        applier.Configure(width, new Color32(24, 12, 44, 220));
    }

    private static TMP_Text EnsureLabel(
        Transform parent, string name, string value, float size,
        Color color, TextAlignmentOptions alignment)
    {
        var existing = FindIn(parent, name);
        TMP_Text label = existing != null ? existing.GetComponent<TMP_Text>() : null;
        if (label == null)
            label = UITheme.Label(parent, name, value, size, color, alignment, true);

        label.text = value;
        label.fontSize = size;
        label.enableAutoSizing = false;
        label.color = color;
        label.alignment = alignment;
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;
        return label;
    }

    private static GameObject FindIn(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root.gameObject;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindIn(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    // ---- RectTransform ----------------------------------------------

    private static void SetCenter(RectTransform rect, Vector2 position, Vector2 size)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void SetTopCenter(RectTransform rect, Vector2 position, Vector2 size)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void SetBottomLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void SetBottomCenter(RectTransform rect, Vector2 position, Vector2 size)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void SetBottomRight(RectTransform rect, Vector2 position, Vector2 size)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void Stretch(RectTransform rect)
    {
        if (rect == null) return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void PopIn(GameObject go, float delay)
    {
        if (go == null) return;
        var pop = go.GetComponent<UIPopIn>();
        if (pop == null) pop = go.AddComponent<UIPopIn>();
        pop.Play(delay);
    }

    private void Update()
    {
        if (waitingText == null || !waitingText.gameObject.activeInHierarchy) return;
        pulseTime += Time.unscaledDeltaTime;
        var color = waitingText.color;
        color.a = 0.55f + 0.45f * Mathf.PingPong(pulseTime * 0.9f, 1f);
        waitingText.color = color;
    }
}

/// <summary>
/// TMPの縁取りを、そのテキストが実際に表示されたタイミングで適用する。
/// 初期非表示のパネル内ではTMPのマテリアルがまだ無く、Awake中に設定できないため。
/// </summary>
public class UITextOutline : MonoBehaviour
{
    private float width = 0.2f;
    private Color32 color = new Color32(24, 12, 44, 220);

    public void Configure(float outlineWidth, Color32 outlineColor)
    {
        width = outlineWidth;
        color = outlineColor;
        Apply();
    }

    private void OnEnable() => Apply();

    private void Apply()
    {
        var text = GetComponent<TMP_Text>();
        if (text == null || !isActiveAndEnabled || !text.gameObject.activeInHierarchy) return;
        if (text.font == null || text.fontSharedMaterial == null) return;

        text.outlineWidth = width;
        text.outlineColor = color;
    }
}

/// <summary>
/// 表示された瞬間に下からバネで飛び込んでくる演出。
/// 「UIが軽快」を成立させるための最小限の入場アニメーション。
/// </summary>
public class UIPopIn : MonoBehaviour
{
    private const float Stiffness = 520f;
    private const float Damping   = 24f;

    private RectTransform rect;
    private Vector2 restPosition;
    private float offset;
    private float velocity;
    private float wait;
    private bool running;

    public void Play(float delay)
    {
        rect = (RectTransform)transform;
        // 再生中に呼ばれた場合、いまのズレを本来の位置と誤認しないようにする
        if (!running) restPosition = rect.anchoredPosition;
        offset = -46f;
        velocity = 0f;
        wait = delay;
        running = true;
        Apply();
    }

    private void Update()
    {
        if (!running) return;

        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        if (wait > 0f)
        {
            wait -= dt;
            return;
        }

        velocity += -offset * Stiffness * dt;
        velocity *= Mathf.Exp(-Damping * dt);
        offset += velocity * dt;

        if (Mathf.Abs(offset) < 0.3f && Mathf.Abs(velocity) < 2f)
        {
            offset = 0f;
            running = false;
        }
        Apply();
    }

    private void Apply()
    {
        if (rect == null) return;
        rect.anchoredPosition = restPosition + new Vector2(0f, offset);
    }
}
