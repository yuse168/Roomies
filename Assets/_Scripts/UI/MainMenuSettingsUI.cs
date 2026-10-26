using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>MainMenuSteam用の、情報を絞ったパーティーゲーム設定画面。</summary>
public sealed class MainMenuSettingsUI : MonoBehaviour
{
    private const float PanelWidth  = 760f;
    private const float PanelHeight = 560f;

    // シーンに置いた設定画面の部品（組み立てで入る。空なら Bind が名前で探す）
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Slider sensitivity;
    [SerializeField] private Slider volume;
    [SerializeField] private TMP_Text sensitivityValue;
    [SerializeField] private TMP_Text volumeValue;
    [SerializeField] private TMP_Text fullscreenValue;
    [SerializeField] private Button fullscreenButton;
    [SerializeField] private Button closeButton;

    private bool wired;

    /// <summary>
    /// 実行時に、シーンに置いてある設定画面へ参照とボタンの反応を結ぶ。
    /// エディタで組み立てたときの AddListener はシーンに保存されないため、ここでつなぎ直す。
    /// 部品の名前は Initialize が付ける名前と同じ（変えるときは両方そろえる）。
    /// </summary>
    public bool Bind(Transform parent)
    {
        if (wired) return true;

        if (group == null && parent != null)
        {
            Transform overlay = parent.Find("MainMenuSettings");
            if (overlay != null)
            {
                group = overlay.GetComponent<CanvasGroup>();
                sensitivity = FindComponent<Slider>(overlay, "マウス感度Slider");
                volume = FindComponent<Slider>(overlay, "マスター音量Slider");
                sensitivityValue = FindComponent<TMP_Text>(overlay, "マウス感度Value");
                volumeValue = FindComponent<TMP_Text>(overlay, "マスター音量Value");
                fullscreenButton = FindComponent<Button>(overlay, "FullscreenButton");
                closeButton = FindComponent<Button>(overlay, "CloseButton");
                if (fullscreenButton != null)
                    fullscreenValue = fullscreenButton.GetComponentInChildren<TMP_Text>(true);
            }
        }

        if (group == null || sensitivity == null || volume == null || sensitivityValue == null ||
            volumeValue == null || fullscreenButton == null || fullscreenValue == null || closeButton == null)
            return false;

        GameSettings.EnsureInitialized();
        WireListeners();
        SetVisible(false);
        wired = true;
        return true;
    }

    private void WireListeners()
    {
        sensitivity.onValueChanged.AddListener(value =>
        {
            GameSettings.SetMouseSensitivity(value);
            sensitivityValue.text = value.ToString("0.00");
        });
        volume.onValueChanged.AddListener(value =>
        {
            GameSettings.SetMasterVolume(value);
            volumeValue.text = $"{Mathf.RoundToInt(value * 100f)}%";
        });
        fullscreenButton.onClick.AddListener(() =>
        {
            GameSettings.SetFullscreen(!GameSettings.Fullscreen);
            RefreshValues();
        });
        closeButton.onClick.AddListener(Close);
    }

    private static T FindComponent<T>(Transform root, string name) where T : Component
    {
        foreach (T component in root.GetComponentsInChildren<T>(true))
            if (component.name == name) return component;
        return null;
    }

    /// <summary>設定画面を組み立てる（エディタの配置ツールから呼ぶ。すでにあれば作らない）。</summary>
    public void Initialize(Transform parent)
    {
        if (parent == null) return;
        if (group != null || parent.Find("MainMenuSettings") != null) return;
        GameSettings.EnsureInitialized();

        var overlay = new GameObject("MainMenuSettings", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(parent, false);
        Stretch(overlay.GetComponent<RectTransform>());
        overlay.transform.SetAsLastSibling();
        overlay.GetComponent<Image>().color = new Color(0.06f, 0.03f, 0.13f, 0.68f);
        group = overlay.AddComponent<CanvasGroup>();

        // 白いカードではなく「濃い面＋太い白フチ」。後ろの部屋が薄く見える。
        var panelGo = new GameObject("SettingsPanel", typeof(RectTransform));
        panelGo.transform.SetParent(overlay.transform, false);
        RectTransform panelRect = (RectTransform)panelGo.transform;
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        UITheme.MenuSurface(panelGo, 44f, 7f);

        TMP_Text title = UITheme.Label(panelGo.transform, "Title", "設定",
            44f, Color.white, TextAlignmentOptions.Left, true);
        SetBox(title.rectTransform, 56f, 40f, 640f, 62f);
        title.characterSpacing = 2f;

        sensitivity = CreateSlider(
            panelGo.transform, "マウス感度", 158f, 0.05f, 1.5f, out sensitivityValue);

        volume = CreateSlider(
            panelGo.transform, "マスター音量", 256f, 0f, 1f, out volumeValue);

        fullscreenButton = CreateButton(
            panelGo.transform, "FullscreenButton", "", 350f,
            UITheme.Cyan, Color.white, 420f, 76f);
        fullscreenValue = fullscreenButton.GetComponentInChildren<TMP_Text>();

        closeButton = CreateButton(
            panelGo.transform, "CloseButton", "もどる", 446f,
            UITheme.Pink, Color.white, 300f, 76f);

        // 押したときの処理は実行時に Bind で結ぶ（ここで AddListener してもシーンに残らない）
        SetVisible(false);
    }

    public void Open()
    {
        RefreshValues();
        SetVisible(true);
        if (EventSystem.current != null && sensitivity != null)
            EventSystem.current.SetSelectedGameObject(sensitivity.gameObject);
    }

    public void Close()
    {
        SetVisible(false);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void RefreshValues()
    {
        sensitivity.SetValueWithoutNotify(GameSettings.MouseSensitivity);
        volume.SetValueWithoutNotify(GameSettings.MasterVolume);
        sensitivityValue.text = GameSettings.MouseSensitivity.ToString("0.00");
        volumeValue.text = $"{Mathf.RoundToInt(GameSettings.MasterVolume * 100f)}%";
        fullscreenValue.text = GameSettings.Fullscreen ? "フルスクリーン　ON" : "フルスクリーン　OFF";
    }

    private void SetVisible(bool visible)
    {
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    private static Slider CreateSlider(
        Transform parent, string labelText, float top,
        float min, float max, out TMP_Text valueText)
    {
        TMP_Text label = UITheme.Label(parent, labelText + "Label", labelText,
            26f, Color.white, TextAlignmentOptions.Left, true);
        SetBox(label.rectTransform, 60f, top, 250f, 52f);

        var sliderGo = new GameObject(labelText + "Slider", typeof(RectTransform), typeof(Slider));
        sliderGo.transform.SetParent(parent, false);
        SetBox(sliderGo.GetComponent<RectTransform>(), 316f, top + 4f, 320f, 44f);
        Slider slider = sliderGo.GetComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;

        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(sliderGo.transform, false);
        RectTransform bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.30f);
        bgRect.anchorMax = new Vector2(1f, 0.70f);
        bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
        Image bg = background.GetComponent<Image>();
        bg.sprite = UITheme.PillSprite;
        bg.color = UITheme.MenuInkSoft;
        UITheme.SetCornerRadius(bg, 8f);

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(sliderGo.transform, false);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0.30f);
        fillRect.anchorMax = new Vector2(1f, 0.70f);
        fillRect.offsetMin = new Vector2(4f, 0f);
        fillRect.offsetMax = new Vector2(-4f, 0f);
        Image fillImage = fill.GetComponent<Image>();
        fillImage.sprite = UITheme.PillSprite;
        fillImage.color = UITheme.Pink;
        UITheme.SetCornerRadius(fillImage, 7f);

        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(sliderGo.transform, false);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(38f, 38f);
        Image handleImage = handle.GetComponent<Image>();
        handleImage.sprite = UITheme.PillSprite;
        handleImage.color = Color.white;
        UITheme.SetCornerRadius(handleImage, 19f);
        UITheme.AddShadow(handle, 0.38f, 4f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;

        valueText = UITheme.Label(parent, labelText + "Value", "",
            24f, UITheme.Sun, TextAlignmentOptions.Center, true);
        SetBox(valueText.rectTransform, 652f, top, 92f, 52f);
        return slider;
    }

    private static Button CreateButton(
        Transform parent, string name, string text, float top,
        Color background, Color foreground, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        SetBox(go.GetComponent<RectTransform>(), (PanelWidth - width) * 0.5f, top, width, height);
        Button button = go.GetComponent<Button>();
        UITheme.Label(go.transform, "Label", text, 28f, foreground,
            TextAlignmentOptions.Center, true);
        UITheme.StylePill(button, background, foreground, 28f, 9f);
        return button;
    }

    private static void SetBox(RectTransform rect, float left, float top, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
