using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// メインメニューとロビーを、情報量を絞った製品ゲーム風のトーンへ統一する。
/// シーンの機能は変えず、既存UIをランタイムで再配置・着せ替えする。
/// </summary>
public class MenuThemer : MonoBehaviour
{
    TMP_Text waitingText;
    float pulseTime;

    void Awake()
    {
        ApplyBackground();
        BuildMainMenuComposition();
        ApplyMainButtons();
        ApplyJoinPanel();
        ApplyLobbyPanel();
        ApplyStatusTexts();
    }

    void ApplyBackground()
    {
        foreach (var bg in UITheme.FindAllDeep("BG"))
        {
            if (!bg.TryGetComponent(out Image image)) continue;
            image.sprite = UITheme.VerticalGradient(UITheme.WarmTop, UITheme.WarmBottom);
            image.color = Color.white;
            image.raycastTarget = false;
        }

        var lobbyPanel = UITheme.FindDeep("LobbyPanel");
        if (lobbyPanel != null && lobbyPanel.TryGetComponent(out Image lobbyBackground))
        {
            lobbyBackground.sprite = UITheme.VerticalGradient(
                new Color(0.055f, 0.062f, 0.075f),
                UITheme.WarmBottom);
            lobbyBackground.color = Color.white;
        }
    }

    void BuildMainMenuComposition()
    {
        var mainPanel = UITheme.FindDeep("MainMenuPanel");
        if (mainPanel == null) return;

        RectTransform root = mainPanel.GetComponent<RectTransform>();
        BuildRoomBackdrop(root);

        var card = FindIn(root, "Card");
        if (card != null && card.TryGetComponent(out Image cardImage))
        {
            SetCenter(card.GetComponent<RectTransform>(), new Vector2(590f, -12f), new Vector2(520f, 590f));
            cardImage.sprite = UITheme.RoundedSprite;
            cardImage.type = Image.Type.Sliced;
            cardImage.color = new Color(0.025f, 0.029f, 0.036f, 0.965f);
            UITheme.AddShadow(card);
            UITheme.AddBorder(card, new Color(1f, 1f, 1f, 0.08f));
        }

        var title = FindIn(root, "Title");
        if (title != null && title.TryGetComponent(out TMP_Text titleText))
        {
            titleText.text = "3日後の<color=#F6C52E>家賃</color>、払える？";
            titleText.fontStyle = FontStyles.Bold;
            titleText.fontSize = 72f;
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 46f;
            titleText.fontSizeMax = 72f;
            titleText.alignment = TextAlignmentOptions.Left;
            titleText.color = UITheme.TextMain;
            titleText.characterSpacing = -1f;
            titleText.textWrappingMode = TextWrappingModes.NoWrap;
            SetTopLeft(titleText.rectTransform, new Vector2(84f, -130f), new Vector2(1030f, 116f));
        }

        var subtitle = FindIn(root, "Subtitle");
        if (subtitle != null && subtitle.TryGetComponent(out TMP_Text subtitleText))
        {
            subtitleText.text = "ルームシェア × 借金 × 3日間の共同生活";
            subtitleText.fontStyle = FontStyles.Normal;
            subtitleText.fontSize = 24f;
            subtitleText.color = UITheme.TextSub;
            subtitleText.alignment = TextAlignmentOptions.Left;
            SetTopLeft(subtitleText.rectTransform, new Vector2(88f, -250f), new Vector2(740f, 46f));

            var tag = EnsureImage(root, "SubtitleTag", UITheme.Accent);
            SetTopLeft(tag.rectTransform, new Vector2(84f, -244f), new Vector2(3f, 34f));
            tag.transform.SetSiblingIndex(Mathf.Max(1, subtitle.transform.GetSiblingIndex()));
            tag.raycastTarget = false;
        }

        var brand = EnsureLabel(root, "BrandLabel", "ROOMIES", 25f, UITheme.TextMain,
            TextAlignmentOptions.Left, true);
        SetTopLeft(brand.rectTransform, new Vector2(84f, -54f), new Vector2(300f, 40f));
        brand.characterSpacing = 10f;

        var cardHeader = EnsureLabel(root, "MenuCardHeader", "ゲームを始める", 30f,
            UITheme.TextMain, TextAlignmentOptions.Left, true);
        SetCenter(cardHeader.rectTransform, new Vector2(590f, 208f), new Vector2(420f, 58f));

        var cardSub = EnsureLabel(root, "MenuCardSub", "オンラインでフレンドと遊ぶ", 19f,
            UITheme.TextSub, TextAlignmentOptions.Left, false);
        SetCenter(cardSub.rectTransform, new Vector2(590f, 162f), new Vector2(420f, 38f));

        LayoutElement(root, "HostButton", new Vector2(590f, 70f), new Vector2(420f, 68f));
        LayoutElement(root, "JoinButton", new Vector2(590f, -14f), new Vector2(420f, 68f));
        LayoutElement(root, "QuitButton", new Vector2(590f, -120f), new Vector2(190f, 50f));
        LayoutElement(root, "StatusText", new Vector2(590f, -200f), new Vector2(430f, 44f));
    }

    void BuildRoomBackdrop(RectTransform root)
    {
        if (FindIn(root, "MoodBackdrop") != null) return;

        var backdrop = new GameObject("MoodBackdrop", typeof(RectTransform));
        backdrop.transform.SetParent(root, false);
        RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
        Stretch(backdropRect);
        backdrop.transform.SetSiblingIndex(1);

        var rail = EnsureImage(backdrop.transform, "AccentRail", UITheme.Accent);
        SetTopLeft(rail.rectTransform, new Vector2(84f, -334f), new Vector2(4f, 250f));
        rail.raycastTarget = false;

        var objective = UITheme.Label(backdrop.transform, "Objective",
            "働く、買う、賭ける。\n3日後までに家賃を作れ。", 30f,
            UITheme.TextMain, TextAlignmentOptions.Left, true);
        SetTopLeft(objective.rectTransform, new Vector2(112f, -338f), new Vector2(700f, 110f));
        objective.lineSpacing = 7f;

        var description = UITheme.Label(backdrop.transform, "Description",
            "毎日の選択が、共同口座と部屋の未来を変える。", 21f,
            UITheme.TextSub, TextAlignmentOptions.Left, false);
        SetTopLeft(description.rectTransform, new Vector2(112f, -470f), new Vector2(720f, 42f));

        var meta = UITheme.Label(backdrop.transform, "Meta",
            "ONLINE CO-OP    •    1–4 PLAYERS", 17f,
            UITheme.TextSub, TextAlignmentOptions.Left, true);
        SetBottomLeft(meta.rectTransform, new Vector2(84f, 58f), new Vector2(620f, 34f));
        meta.characterSpacing = 5f;

        var line = EnsureImage(backdrop.transform, "BottomLine", new Color(1f, 1f, 1f, 0.10f));
        SetBottomLeft(line.rectTransform, new Vector2(84f, 104f), new Vector2(720f, 1f));
        line.raycastTarget = false;
    }

    void ApplyMainButtons()
    {
        StyleButtonByName("HostButton", UITheme.Accent, new Color(0.10f, 0.08f, 0.04f), 34f);
        StyleButtonByName("JoinButton", UITheme.DarkButton, UITheme.TextMain, 32f);
        StyleButtonByName("QuitButton", UITheme.DarkButton, UITheme.TextSub, 27f);
    }

    void ApplyJoinPanel()
    {
        var joinPanel = UITheme.FindDeep("JoinPanel");
        if (joinPanel != null && joinPanel.TryGetComponent(out Image dim))
        {
            dim.sprite = null;
            dim.color = new Color(0.008f, 0.010f, 0.014f, 0.84f);
        }

        var joinCard = FindIn(joinPanel != null ? joinPanel.transform : null, "Card");
        StyleCard(joinCard);

        var label = FindIn(joinPanel != null ? joinPanel.transform : null, "Label");
        if (label != null && label.TryGetComponent(out TMP_Text labelText))
        {
            labelText.text = "ルームコードを入力";
            labelText.color = UITheme.TextMain;
            labelText.fontStyle = FontStyles.Bold;
        }

        var inputGo = UITheme.FindDeep("CodeInputField");
        if (inputGo != null && inputGo.TryGetComponent(out TMP_InputField input))
        {
            if (input.TryGetComponent(out Image inputBackground))
            {
                inputBackground.sprite = UITheme.RoundedSprite;
                inputBackground.type = Image.Type.Sliced;
                inputBackground.color = UITheme.PanelSoft;
                UITheme.AddBorder(inputGo);
            }

            if (input.textComponent != null)
            {
                input.textComponent.fontStyle = FontStyles.Bold;
                input.textComponent.color = UITheme.Gold;
                input.textComponent.characterSpacing = 10f;
                input.textComponent.alignment = TextAlignmentOptions.Center;
            }

            if (input.placeholder is TMP_Text placeholder)
            {
                placeholder.color = new Color(UITheme.TextSub.r, UITheme.TextSub.g, UITheme.TextSub.b, 0.55f);
                placeholder.alignment = TextAlignmentOptions.Center;
                placeholder.fontStyle = FontStyles.Italic;
            }
        }

        StyleButtonByName("ConfirmJoinButton", UITheme.Accent,
            new Color(0.10f, 0.08f, 0.04f), 30f);
        StyleButtonByName("CancelButton", UITheme.DarkButton, UITheme.TextSub, 26f);
    }

    void ApplyLobbyPanel()
    {
        var lobbyPanel = UITheme.FindDeep("LobbyPanel");
        if (lobbyPanel != null)
        {
            StyleCard(FindIn(lobbyPanel.transform, "Card"));
            var title = FindIn(lobbyPanel.transform, "Title");
            if (title != null && title.TryGetComponent(out TMP_Text titleText))
            {
                titleText.text = "みんなの部屋";
                titleText.color = UITheme.TextMain;
                titleText.fontStyle = FontStyles.Bold;
            }
        }

        var codeBox = UITheme.FindDeep("CodeBox");
        if (codeBox != null && codeBox.TryGetComponent(out Image boxImage))
        {
            boxImage.sprite = UITheme.RoundedSprite;
            boxImage.type = Image.Type.Sliced;
            boxImage.color = UITheme.PanelSoft;
            UITheme.AddShadow(codeBox);
            UITheme.AddBorder(codeBox);
        }

        var codeText = UITheme.FindDeep("PartyCodeText");
        if (codeText != null && codeText.TryGetComponent(out TMP_Text code))
        {
            code.fontStyle = FontStyles.Bold;
            code.color = UITheme.Gold;
            code.characterSpacing = 14f;
        }

        var playersLabel = UITheme.FindDeep("PlayersLabel");
        if (playersLabel != null && playersLabel.TryGetComponent(out TMP_Text players))
        {
            players.text = "いま部屋にいるメンバー";
            players.color = UITheme.TextSub;
            players.fontStyle = FontStyles.Bold;
        }

        StyleButtonByName("CopyButton", UITheme.DarkButton, UITheme.TextMain, 24f);
        StyleButtonByName("StartButton", UITheme.Accent, new Color(0.08f, 0.07f, 0.03f), 34f);
        StyleButtonByName("LeaveButton", UITheme.DarkButton, UITheme.Red, 26f);
        StyleButtonByName("InviteButton", UITheme.DarkButton, UITheme.TextMain, 26f);

        var waiting = UITheme.FindDeep("WaitingText");
        if (waiting != null && waiting.TryGetComponent(out TMP_Text waitText))
        {
            waitingText = waitText;
            waitText.color = UITheme.TextSub;
            waitText.fontStyle = FontStyles.Italic;
        }
    }

    void ApplyStatusTexts()
    {
        SetTextStyle("StatusText", UITheme.Gold);
        SetTextStyle("ErrorText", UITheme.Red);
        SetTextStyle("CopyFeedbackText", UITheme.Green);
    }

    static void SetTextStyle(string name, Color color)
    {
        var go = UITheme.FindDeep(name);
        if (go == null || !go.TryGetComponent(out TMP_Text text)) return;
        text.color = color;
        text.fontStyle = FontStyles.Bold;
    }

    static void StyleButtonByName(string name, Color background, Color foreground, float maxFontSize)
    {
        var go = UITheme.FindDeep(name);
        if (go != null && go.TryGetComponent(out Button button))
            UITheme.StyleButton(button, background, foreground, maxFontSize);
    }

    static void StyleCardByName(string name)
    {
        foreach (var go in UITheme.FindAllDeep(name))
        {
            if (!go.TryGetComponent(out Image image)) continue;
            image.sprite = UITheme.RoundedSprite;
            image.type = Image.Type.Sliced;
            image.color = UITheme.Panel;
            UITheme.AddShadow(go);
            UITheme.AddBorder(go);
        }
    }

    static void StyleCard(GameObject go)
    {
        if (go == null || !go.TryGetComponent(out Image image)) return;
        image.sprite = UITheme.RoundedSprite;
        image.type = Image.Type.Sliced;
        image.color = UITheme.Panel;
        UITheme.AddShadow(go);
        UITheme.AddBorder(go);
    }

    static void LayoutElement(Transform root, string name, Vector2 position, Vector2 size)
    {
        var go = FindIn(root, name);
        if (go != null)
            SetCenter(go.GetComponent<RectTransform>(), position, size);
    }

    static GameObject FindIn(Transform root, string name)
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

    static Image EnsureImage(Transform parent, string name, Color color)
    {
        var existing = FindIn(parent, name);
        Image image;
        if (existing != null)
        {
            image = existing.GetComponent<Image>();
            if (image == null) image = existing.AddComponent<Image>();
        }
        else
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            image = go.AddComponent<Image>();
        }

        image.sprite = UITheme.RoundedSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        return image;
    }

    static TMP_Text EnsureLabel(
        Transform parent, string name, string value, float size,
        Color color, TextAlignmentOptions alignment, bool bold)
    {
        var existing = FindIn(parent, name);
        TMP_Text label = existing != null ? existing.GetComponent<TMP_Text>() : null;
        if (label == null)
            label = UITheme.Label(parent, name, value, size, color, alignment, bold);

        label.text = value;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        label.raycastTarget = false;
        return label;
    }

    static void SetCenter(RectTransform rect, Vector2 position, Vector2 size)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void SetBottomLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    void Update()
    {
        if (waitingText == null || !waitingText.gameObject.activeInHierarchy) return;
        pulseTime += Time.deltaTime;
        var color = waitingText.color;
        color.a = 0.55f + 0.45f * Mathf.PingPong(pulseTime * 0.9f, 1f);
        waitingText.color = color;
    }
}
