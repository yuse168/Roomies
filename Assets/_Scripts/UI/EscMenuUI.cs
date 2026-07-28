using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// GameRoom用のESCメニュー。マルチプレイ中もゲーム時間は止めず、
/// ローカルプレイヤーの操作だけを止める。
/// </summary>
public sealed class EscMenuUI : MonoBehaviour
{
    const string GameSceneName = "GameRoom";
    const string MenuSceneName = "MainMenuSteam";

    enum ConfirmAction
    {
        None,
        MainMenu,
        Quit
    }

    public static bool IsOpen { get; private set; }

    GameObject overlay;
    CanvasGroup overlayGroup;
    RectTransform card;
    CanvasGroup mainGroup;
    CanvasGroup settingsGroup;
    CanvasGroup confirmGroup;
    Button resumeButton;
    Button settingsBackButton;
    Button confirmCancelButton;
    TMP_Text sensitivityValue;
    TMP_Text volumeValue;
    TMP_Text fullscreenValue;
    TMP_Text resolutionValue;
    TMP_Text qualityValue;
    TMP_Text cameraMotionValue;
    TMP_Text confirmTitle;
    TMP_Text confirmMessage;
    Slider sensitivitySlider;
    Slider volumeSlider;
    ConfirmAction pendingAction;
    bool isLeavingScene;
    Coroutine animationCoroutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterSceneHook()
    {
        IsOpen = false;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        IsOpen = false;
        if (scene.name != GameSceneName || FindAnyObjectByType<EscMenuUI>() != null) return;

        var host = new GameObject(nameof(EscMenuUI));
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<EscMenuUI>();
    }

    void Awake()
    {
        GameSettings.EnsureInitialized();
        EnsureEventSystem();
        BuildUI();
        SetOpen(false, true);
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || isLeavingScene || !keyboard.escapeKey.wasPressedThisFrame) return;

        if (!IsOpen)
        {
            OpenMenu();
        }
        else if (pendingAction != ConfirmAction.None)
        {
            HideConfirmation();
        }
        else if (settingsGroup.interactable)
        {
            ShowMain();
        }
        else
        {
            CloseMenu();
        }
    }

    void OnDestroy()
    {
        IsOpen = false;
        if (!isLeavingScene && SceneManager.GetActiveScene().name == GameSceneName)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void OpenMenu()
    {
        if (isLeavingScene) return;
        ShowMain();
        SetOpen(true);
    }

    public void CloseMenu()
    {
        if (!isLeavingScene) SetOpen(false);
    }

    public void OpenSettings()
    {
        if (!IsOpen || isLeavingScene) return;
        RefreshSettings();
        SetVisible(mainGroup, false);
        SetVisible(settingsGroup, true);
        SetVisible(confirmGroup, false);
        pendingAction = ConfirmAction.None;
        Select(settingsBackButton);
    }

    void BuildUI()
    {
        Canvas canvas = UITheme.CreateCanvas(transform, "EscMenuCanvas", 32000);
        canvas.gameObject.AddComponent<GraphicRaycaster>();

        overlay = new GameObject("Overlay", typeof(RectTransform));
        overlay.transform.SetParent(canvas.transform, false);
        Stretch(overlay.GetComponent<RectTransform>());
        var dim = overlay.AddComponent<Image>();
        dim.color = new Color(0.025f, 0.035f, 0.075f, 0.82f);
        overlayGroup = overlay.AddComponent<CanvasGroup>();

        var cardImage = UITheme.Card(overlay.transform, "MenuCard");
        card = cardImage.rectTransform;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
        card.anchoredPosition = Vector2.zero;
        card.sizeDelta = new Vector2(900f, 820f);
        cardImage.color = new Color(0.07f, 0.085f, 0.15f, 0.995f);

        CreateAccent(card);
        TMP_Text title = UITheme.Label(
            card, "Title", "PAUSE!", 54f, UITheme.Gold,
            TextAlignmentOptions.Center, true);
        SetBox(title.rectTransform, 40f, 28f, 820f, 70f);
        UITheme.AddTextOutline(title, 0.16f);

        mainGroup = CreateGroup(card, "Main");
        BuildMainPanel(mainGroup.transform);

        settingsGroup = CreateGroup(card, "Settings");
        BuildSettingsPanel(settingsGroup.transform);

        confirmGroup = CreateGroup(card, "Confirmation");
        BuildConfirmation(confirmGroup.transform);
    }

    void BuildMainPanel(Transform parent)
    {
        TMP_Text subtitle = UITheme.Label(
            parent, "Subtitle", "ちょっとひと休み", 24f, UITheme.TextSub,
            TextAlignmentOptions.Center, true);
        SetBox(subtitle.rectTransform, 40f, 100f, 820f, 44f);

        resumeButton = CreateButton(parent, "ResumeButton", "ゲームにもどる",
            190f, UITheme.Green, 210f, 480f);
        resumeButton.onClick.AddListener(CloseMenu);

        Button settingsButton = CreateButton(parent, "SettingsButton", "設定",
            288f, UITheme.Blue, 210f, 480f);
        settingsButton.onClick.AddListener(OpenSettings);

        Button menuButton = CreateButton(parent, "MenuButton", "メインメニューへ",
            386f, new Color(0.42f, 0.36f, 0.82f), 210f, 480f);
        menuButton.onClick.AddListener(() => ShowConfirmation(ConfirmAction.MainMenu));

        Button quitButton = CreateButton(parent, "QuitButton", "ゲームを終了",
            484f, UITheme.Red, 210f, 480f);
        quitButton.onClick.AddListener(() => ShowConfirmation(ConfirmAction.Quit));

        TMP_Text notice = UITheme.Label(
            parent, "OnlineNotice",
            "オンライン中は、メニューを開いてもゲームは進み続けます",
            19f, UITheme.TextSub, TextAlignmentOptions.Center);
        SetBox(notice.rectTransform, 70f, 622f, 760f, 40f);

        TMP_Text hint = UITheme.Label(
            parent, "Hint", "ESCでもゲームにもどれます", 19f, UITheme.TextSub,
            TextAlignmentOptions.Center);
        SetBox(hint.rectTransform, 70f, 716f, 760f, 42f);
    }

    void BuildSettingsPanel(Transform parent)
    {
        TMP_Text heading = UITheme.Label(
            parent, "Heading", "ゲーム設定", 30f, Color.white,
            TextAlignmentOptions.Center, true);
        SetBox(heading.rectTransform, 40f, 98f, 820f, 48f);

        sensitivitySlider = CreateSliderRow(
            parent, "マウス感度", 164f, 0.05f, 1.5f, out sensitivityValue);
        sensitivitySlider.SetValueWithoutNotify(GameSettings.MouseSensitivity);
        sensitivitySlider.onValueChanged.AddListener(value =>
        {
            GameSettings.SetMouseSensitivity(value);
            sensitivityValue.text = value.ToString("0.00");
        });

        volumeSlider = CreateSliderRow(
            parent, "マスター音量", 242f, 0f, 1f, out volumeValue);
        volumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
        volumeSlider.onValueChanged.AddListener(value =>
        {
            GameSettings.SetMasterVolume(value);
            volumeValue.text = $"{Mathf.RoundToInt(value * 100f)}%";
        });

        CreateToggleRow(
            parent, "画面モード", 320f, out fullscreenValue,
            () =>
            {
                GameSettings.SetFullscreen(!GameSettings.Fullscreen);
                RefreshSettings();
            });

        CreateCycleRow(
            parent, "解像度", 398f, out resolutionValue,
            () =>
            {
                GameSettings.SetResolutionIndex(GameSettings.ResolutionIndex - 1);
                RefreshSettings();
            },
            () =>
            {
                GameSettings.SetResolutionIndex(GameSettings.ResolutionIndex + 1);
                RefreshSettings();
            });

        CreateCycleRow(
            parent, "グラフィック品質", 476f, out qualityValue,
            () =>
            {
                GameSettings.SetQualityLevel(GameSettings.QualityLevel - 1);
                RefreshSettings();
            },
            () =>
            {
                GameSettings.SetQualityLevel(GameSettings.QualityLevel + 1);
                RefreshSettings();
            });

        CreateToggleRow(
            parent, "カメラの揺れ", 554f, out cameraMotionValue,
            () =>
            {
                GameSettings.SetCameraMotion(!GameSettings.CameraMotion);
                RefreshSettings();
            });

        Button resetButton = CreateButton(
            parent, "ResetButton", "初期設定にもどす", 655f,
            new Color(0.42f, 0.45f, 0.52f), 80f, 330f, 64f, 23f);
        resetButton.onClick.AddListener(() =>
        {
            GameSettings.ResetDefaults();
            RefreshSettings();
        });

        settingsBackButton = CreateButton(
            parent, "BackButton", "もどる", 655f,
            UITheme.Blue, 490f, 330f, 64f, 24f);
        settingsBackButton.onClick.AddListener(ShowMain);

        RefreshSettings();
    }

    void BuildConfirmation(Transform parent)
    {
        var shade = parent.gameObject.AddComponent<Image>();
        shade.color = new Color(0.01f, 0.015f, 0.04f, 0.9f);

        var dialogImage = UITheme.Card(parent, "Dialog");
        RectTransform dialog = dialogImage.rectTransform;
        dialog.anchorMin = dialog.anchorMax = dialog.pivot = new Vector2(0.5f, 0.5f);
        dialog.anchoredPosition = Vector2.zero;
        dialog.sizeDelta = new Vector2(670f, 390f);
        dialogImage.color = new Color(0.095f, 0.11f, 0.19f, 1f);

        confirmTitle = UITheme.Label(
            dialog, "Title", "確認", 38f, UITheme.Gold,
            TextAlignmentOptions.Center, true);
        SetBox(confirmTitle.rectTransform, 35f, 38f, 600f, 58f);

        confirmMessage = UITheme.Label(
            dialog, "Message", "", 24f, Color.white,
            TextAlignmentOptions.Center, true);
        SetBox(confirmMessage.rectTransform, 48f, 120f, 574f, 80f);

        confirmCancelButton = CreateButton(
            dialog, "CancelButton", "キャンセル", 258f,
            new Color(0.42f, 0.45f, 0.52f), 50f, 260f, 72f, 23f);
        confirmCancelButton.onClick.AddListener(HideConfirmation);

        Button executeButton = CreateButton(
            dialog, "ExecuteButton", "はい", 258f,
            UITheme.Red, 360f, 260f, 72f, 24f);
        executeButton.onClick.AddListener(ExecutePendingAction);
    }

    void RefreshSettings()
    {
        if (sensitivityValue == null) return;

        sensitivitySlider.SetValueWithoutNotify(GameSettings.MouseSensitivity);
        volumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
        sensitivityValue.text = GameSettings.MouseSensitivity.ToString("0.00");
        volumeValue.text = $"{Mathf.RoundToInt(GameSettings.MasterVolume * 100f)}%";
        fullscreenValue.text = GameSettings.Fullscreen ? "フルスクリーン" : "ウィンドウ";

        var options = GameSettings.ResolutionOptions;
        int resolutionIndex = Mathf.Clamp(GameSettings.ResolutionIndex, 0, options.Count - 1);
        resolutionValue.text = options[resolutionIndex].Label;

        string[] qualityNames = QualitySettings.names;
        qualityValue.text = qualityNames.Length == 0
            ? "-"
            : qualityNames[Mathf.Clamp(GameSettings.QualityLevel, 0, qualityNames.Length - 1)];
        cameraMotionValue.text = GameSettings.CameraMotion ? "ON" : "OFF";
    }

    void ShowMain()
    {
        pendingAction = ConfirmAction.None;
        SetVisible(mainGroup, true);
        SetVisible(settingsGroup, false);
        SetVisible(confirmGroup, false);
        Select(resumeButton);
    }

    void ShowConfirmation(ConfirmAction action)
    {
        pendingAction = action;
        confirmTitle.text = action == ConfirmAction.Quit ? "ゲームを終了しますか？" : "メニューへ戻りますか？";
        confirmMessage.text = action == ConfirmAction.Quit
            ? "現在のゲームを終了します"
            : "現在のルームから退出します";
        SetVisible(confirmGroup, true);
        Select(confirmCancelButton);
    }

    void HideConfirmation()
    {
        pendingAction = ConfirmAction.None;
        SetVisible(confirmGroup, false);
        Select(resumeButton);
    }

    void ExecutePendingAction()
    {
        if (pendingAction == ConfirmAction.MainMenu)
            ReturnToMainMenu();
        else if (pendingAction == ConfirmAction.Quit)
            QuitGame();
    }

    void SetOpen(bool open, bool immediate = false)
    {
        if (overlayGroup == null) return;

        IsOpen = open;
        overlayGroup.alpha = open ? 1f : 0f;
        overlayGroup.interactable = open;
        overlayGroup.blocksRaycasts = open;
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;

        if (!open)
        {
            pendingAction = ConfirmAction.None;
            SetVisible(confirmGroup, false);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            return;
        }

        if (animationCoroutine != null)
            StopCoroutine(animationCoroutine);

        if (immediate)
            card.localScale = Vector3.one;
        else
            animationCoroutine = StartCoroutine(AnimateOpen());
    }

    IEnumerator AnimateOpen()
    {
        card.localScale = Vector3.one * 0.84f;
        float elapsed = 0f;
        const float duration = 0.18f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            card.localScale = Vector3.one * Mathf.LerpUnclamped(0.84f, 1.04f, eased);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < 0.08f)
        {
            elapsed += Time.unscaledDeltaTime;
            card.localScale = Vector3.one * Mathf.Lerp(1.04f, 1f, elapsed / 0.08f);
            yield return null;
        }

        card.localScale = Vector3.one;
        animationCoroutine = null;
    }

    void ReturnToMainMenu()
    {
        if (isLeavingScene) return;
        isLeavingScene = true;

        if (SteamLobby.Instance != null)
            SteamLobby.Instance.LeaveLobby();
        else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        IsOpen = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(MenuSceneName);
    }

    void QuitGame()
    {
        if (isLeavingScene) return;
        isLeavingScene = true;

        if (SteamLobby.Instance != null)
            SteamLobby.Instance.LeaveLobby();
        else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        IsOpen = false;
        Application.Quit();
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    static CanvasGroup CreateGroup(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());
        return go.AddComponent<CanvasGroup>();
    }

    static void CreateAccent(Transform parent)
    {
        var accent = new GameObject("Accent", typeof(RectTransform));
        accent.transform.SetParent(parent, false);
        RectTransform rt = accent.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, 14f);
        rt.anchoredPosition = Vector2.zero;
        var image = accent.AddComponent<Image>();
        image.sprite = UITheme.RoundedSprite;
        image.type = Image.Type.Sliced;
        image.color = UITheme.Accent;
    }

    static Slider CreateSliderRow(
        Transform parent,
        string labelText,
        float top,
        float min,
        float max,
        out TMP_Text valueText)
    {
        TMP_Text label = UITheme.Label(
            parent, labelText + "Label", labelText, 23f, Color.white,
            TextAlignmentOptions.MidlineLeft, true);
        SetBox(label.rectTransform, 82f, top, 250f, 58f);

        var sliderGo = new GameObject(labelText + "Slider", typeof(RectTransform));
        sliderGo.transform.SetParent(parent, false);
        RectTransform sliderRt = sliderGo.GetComponent<RectTransform>();
        SetBox(sliderRt, 360f, top + 8f, 330f, 42f);
        var slider = sliderGo.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;

        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(sliderGo.transform, false);
        RectTransform backgroundRt = background.GetComponent<RectTransform>();
        backgroundRt.anchorMin = new Vector2(0f, 0.35f);
        backgroundRt.anchorMax = new Vector2(1f, 0.65f);
        backgroundRt.offsetMin = backgroundRt.offsetMax = Vector2.zero;
        Image backgroundImage = background.GetComponent<Image>();
        backgroundImage.sprite = UITheme.RoundedSprite;
        backgroundImage.type = Image.Type.Sliced;
        backgroundImage.color = new Color(0.18f, 0.2f, 0.28f);

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderGo.transform, false);
        RectTransform fillAreaRt = fillArea.GetComponent<RectTransform>();
        fillAreaRt.anchorMin = new Vector2(0f, 0.35f);
        fillAreaRt.anchorMax = new Vector2(1f, 0.65f);
        fillAreaRt.offsetMin = new Vector2(5f, 0f);
        fillAreaRt.offsetMax = new Vector2(-5f, 0f);

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fillRt = fill.GetComponent<RectTransform>();
        Stretch(fillRt);
        Image fillImage = fill.GetComponent<Image>();
        fillImage.sprite = UITheme.RoundedSprite;
        fillImage.type = Image.Type.Sliced;
        fillImage.color = UITheme.Accent;

        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderGo.transform, false);
        RectTransform handleAreaRt = handleArea.GetComponent<RectTransform>();
        Stretch(handleAreaRt);
        handleAreaRt.offsetMin = new Vector2(10f, 0f);
        handleAreaRt.offsetMax = new Vector2(-10f, 0f);

        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        RectTransform handleRt = handle.GetComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(30f, 30f);
        Image handleImage = handle.GetComponent<Image>();
        handleImage.sprite = UITheme.RoundedSprite;
        handleImage.type = Image.Type.Sliced;
        handleImage.color = Color.white;

        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handleImage;

        valueText = UITheme.Label(
            parent, labelText + "Value", "", 22f, UITheme.Gold,
            TextAlignmentOptions.Center, true);
        SetBox(valueText.rectTransform, 710f, top, 110f, 58f);
        return slider;
    }

    static void CreateToggleRow(
        Transform parent,
        string labelText,
        float top,
        out TMP_Text valueText,
        UnityEngine.Events.UnityAction action)
    {
        TMP_Text label = UITheme.Label(
            parent, labelText + "Label", labelText, 23f, Color.white,
            TextAlignmentOptions.MidlineLeft, true);
        SetBox(label.rectTransform, 82f, top, 300f, 58f);

        Button button = CreateButton(
            parent, labelText + "Button", "", top,
            new Color(0.24f, 0.48f, 0.68f), 520f, 300f, 58f, 21f);
        valueText = button.GetComponentInChildren<TMP_Text>();
        button.onClick.AddListener(action);
    }

    static void CreateCycleRow(
        Transform parent,
        string labelText,
        float top,
        out TMP_Text valueText,
        UnityEngine.Events.UnityAction previous,
        UnityEngine.Events.UnityAction next)
    {
        TMP_Text label = UITheme.Label(
            parent, labelText + "Label", labelText, 23f, Color.white,
            TextAlignmentOptions.MidlineLeft, true);
        SetBox(label.rectTransform, 82f, top, 300f, 58f);

        Button previousButton = CreateButton(
            parent, labelText + "Previous", "◀", top,
            UITheme.Blue, 450f, 62f, 58f, 20f);
        previousButton.onClick.AddListener(previous);

        var valueBackground = UITheme.Card(parent, labelText + "ValueBackground");
        SetBox(valueBackground.rectTransform, 522f, top, 226f, 58f);
        valueBackground.color = new Color(0.12f, 0.14f, 0.22f, 1f);
        valueText = UITheme.Label(
            valueBackground.transform, "Value", "", 20f, Color.white,
            TextAlignmentOptions.Center, true);
        Stretch(valueText.rectTransform);

        Button nextButton = CreateButton(
            parent, labelText + "Next", "▶", top,
            UITheme.Blue, 758f, 62f, 58f, 20f);
        nextButton.onClick.AddListener(next);
    }

    static Button CreateButton(
        Transform parent,
        string name,
        string text,
        float top,
        Color color,
        float left,
        float width,
        float height = 76f,
        float fontSize = 27f)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        SetBox(go.GetComponent<RectTransform>(), left, top, width, height);

        var button = go.AddComponent<Button>();
        TMP_Text label = UITheme.Label(
            go.transform, "Label", text, fontSize, Color.white,
            TextAlignmentOptions.Center, true);
        Stretch(label.rectTransform);
        UITheme.StyleButton(button, color, Color.white, 32f);
        return button;
    }

    static void SetVisible(CanvasGroup group, bool visible)
    {
        if (group == null) return;
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    static void Select(Button button)
    {
        if (EventSystem.current != null && button != null)
            EventSystem.current.SetSelectedGameObject(button.gameObject);
    }

    static void SetBox(RectTransform rt, float left, float top, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(left, -top);
        rt.sizeDelta = new Vector2(width, height);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
