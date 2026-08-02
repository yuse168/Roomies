using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 起動時の「LN Studio」スタジオロゴ演出。BootSceneに1つ置くだけで動く。
///
/// 階層はランタイム生成する（プロジェクト全体のUI方針と同じ。MenuThemer / HudThemer と同様）：
///   SplashCanvas
///     Background            … 中央だけ僅かに明るいラジアルグラデーション
///     LogoRoot (CanvasGroup)… 全体のフェードと保持中のドリフト
///       LNGroup             … LNの 95% → 100% スケール
///         LN_Soft2 / LN_Soft1 … LNの複製。ボケ→シャープの焦点送りを作る
///         LN                … ロゴ本体
///         SheenClip         … 細い光が1回だけ横切るためのマスク
///           Sheen           … LNの複製（白）。マスクの中だけ明るく見える
///       Studio              … LNより少し遅れて出る小さい文字
///
/// 演出はコルーチンで1本の時間軸として持つ。DOTweenは未導入で、
/// 既存の演出（DayTransitionUI / RentPaymentUI / NightEventUI）も同じ手法のため合わせている。
/// 時間はすべて unscaled（起動直後に timeScale が触られていても影響を受けない）。
/// </summary>
[DisallowMultipleComponent]
public sealed class LNStudioSplash : MonoBehaviour
{
    // ---- 遷移先 ----
    [SerializeField] private string nextSceneName = "MainMenuSteam";

    // ---- 時間軸（秒・仕様どおり） ----
    private const float LogoInStart    = 0.50f;
    private const float LogoInEnd      = 1.15f;
    private const float StudioInStart  = 0.95f;
    private const float StudioInEnd    = 1.45f;
    private const float HoldEnd        = 3.85f;
    private const float SheenStart     = 1.80f;
    private const float SheenEnd       = 2.70f;
    private const float FadeOutStart   = 3.85f;
    private const float FadeOutEnd     = 4.60f;
    private const float PreloadAt      = 1.45f;

    // ---- 見た目 ----
    private static readonly Color BackgroundInner = new Color32(0x17, 0x19, 0x1F, 0xFF);
    private static readonly Color BackgroundOuter = new Color32(0x08, 0x09, 0x0C, 0xFF);
    private static readonly Color LogoColor       = new Color32(0xE4, 0xE7, 0xEE, 0xFF);
    private static readonly Color StudioColor     = new Color32(0x9B, 0xA3, 0xB4, 0xFF);
    private static readonly Color RuleColor       = new Color32(0xE4, 0xE7, 0xEE, 0x59);

    // 明朝相当のセリフ体。SplashFontBuilderがAssets/_Fontsの.ttfから生成する。
    // 差し替え候補：LogoSerifAlt SDF（Palatino / より柔らかい）
    private const string FontResourcePath = "Fonts/LogoSerif SDF";

    // セリフ体は太らせず、字間を開けて見せる（明朝ロゴの定石）
    private const float LogoFontSize    = 200f;
    private const float LogoTracking    = 14f;
    private const float StudioFontSize  = 38f;
    private const float StudioTracking  = 30f;
    private const float SheenHalfTravel = 260f;
    private const float SheenClipWidth  = 130f;

    private CanvasGroup logoGroup;
    private RectTransform logoRoot;
    private RectTransform lnGroup;
    private TMP_Text lnText;
    private TMP_Text lnSoft1;
    private TMP_Text lnSoft2;
    private TMP_Text sheenText;
    private RectTransform sheenClip;
    private Image ruleImage;
    private TMP_Text studioText;
    private Vector2 studioRestPosition;

    private const float RuleWidth = 168f;

    private AsyncOperation preload;
    private bool finished;

    private void Awake()
    {
        BuildUI();
    }

    private void Start()
    {
        StartCoroutine(PlayRoutine());
    }

    // ================================================================
    // 階層の組み立て
    // ================================================================

    private void BuildUI()
    {
        Canvas canvas = UITheme.CreateCanvas(transform, "SplashCanvas", 32760);

        // ---- 背景（中央だけ僅かに明るい。単色に見えるほど弱く） ----
        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(canvas.transform, false);
        Stretch(background.GetComponent<RectTransform>());
        Image backgroundImage = background.GetComponent<Image>();
        backgroundImage.sprite = UITheme.RadialGradient(BackgroundInner, BackgroundOuter);
        backgroundImage.raycastTarget = false;

        // ---- ロゴ全体 ----
        var root = new GameObject("LogoRoot", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(canvas.transform, false);
        logoRoot = root.GetComponent<RectTransform>();
        Center(logoRoot, Vector2.zero, new Vector2(960f, 440f));
        logoGroup = root.GetComponent<CanvasGroup>();
        logoGroup.alpha = 0f;
        logoGroup.interactable = false;
        logoGroup.blocksRaycasts = false;

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>(FontResourcePath);

        // ---- LN（ボケ用の複製2枚 → 本体 → 光沢の順に重ねる） ----
        var group = new GameObject("LNGroup", typeof(RectTransform));
        group.transform.SetParent(logoRoot, false);
        lnGroup = group.GetComponent<RectTransform>();
        Center(lnGroup, Vector2.zero, new Vector2(960f, 440f));

        lnSoft2 = CreateLogoLabel(lnGroup, "LN_Soft2", font);
        lnSoft1 = CreateLogoLabel(lnGroup, "LN_Soft1", font);
        lnText  = CreateLogoLabel(lnGroup, "LN", font);

        // 光沢はLNと同じ文字を白で重ね、細いマスクの中だけ見せる。
        // マスクを動かしても文字が動かないよう、毎フレーム逆方向へオフセットする。
        var clip = new GameObject("SheenClip", typeof(RectTransform), typeof(RectMask2D));
        clip.transform.SetParent(lnGroup, false);
        sheenClip = clip.GetComponent<RectTransform>();
        Center(sheenClip, new Vector2(-SheenHalfTravel, 42f),
            new Vector2(SheenClipWidth, 280f));
        var mask = clip.GetComponent<RectMask2D>();
        mask.softness = new Vector2Int(52, 0);

        sheenText = CreateLogoLabel(sheenClip, "Sheen", font);
        sheenText.color = Color.white;
        sheenText.alpha = 0f;

        // ---- LNとStudioを分ける細い罫線（明朝ロゴの定番の割り） ----
        var rule = new GameObject("Rule", typeof(RectTransform), typeof(Image));
        rule.transform.SetParent(logoRoot, false);
        ruleImage = rule.GetComponent<Image>();
        ruleImage.color = RuleColor;
        ruleImage.raycastTarget = false;
        Center(ruleImage.rectTransform, new Vector2(0f, -66f), new Vector2(RuleWidth, 1f));

        // ---- Studio（LNの下に、LNの横幅に収まる幅で） ----
        studioText = UITheme.Label(logoRoot, "Studio", "Studio",
            StudioFontSize, StudioColor, TextAlignmentOptions.Center, bold: false);
        studioText.font = font != null ? font : studioText.font;
        studioText.characterSpacing = StudioTracking;
        studioText.textWrappingMode = TextWrappingModes.NoWrap;
        studioText.raycastTarget = false;
        studioText.alpha = 0f;
        studioRestPosition = new Vector2(0f, -112f);
        Center(studioText.rectTransform, studioRestPosition, new Vector2(760f, 66f));
    }

    private static TMP_Text CreateLogoLabel(Transform parent, string name, TMP_FontAsset font)
    {
        // セリフ体は擬似ボールドをかけると細い線が潰れるので、Regularのまま使う
        TMP_Text label = UITheme.Label(parent, name, "LN",
            LogoFontSize, LogoColor, TextAlignmentOptions.Center, bold: false);
        if (font != null) label.font = font;
        label.characterSpacing = LogoTracking;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.alpha = 0f;
        Center(label.rectTransform, new Vector2(0f, 40f), new Vector2(900f, 260f));
        return label;
    }

    // ================================================================
    // 演出
    // ================================================================

    private IEnumerator PlayRoutine()
    {
        float elapsed = 0f;

        while (elapsed < FadeOutEnd)
        {
            elapsed += Time.unscaledDeltaTime;

            // スキップは「即カット」ではなく、フェードアウトへ飛ばして自然に終わらせる
            if (SkipRequested() && elapsed < FadeOutStart)
                elapsed = FadeOutStart;

            if (preload == null && elapsed >= PreloadAt)
                BeginPreload();

            ApplyFrame(elapsed);
            yield return null;
        }

        ApplyFrame(FadeOutEnd);
        GoToNextScene();
    }

    /// <summary>経過時間から全チャンネルを毎フレーム算出する（重なる区間を正しく扱うため）。</summary>
    private void ApplyFrame(float t)
    {
        // ---- LN：フェードイン ＋ 95%→100% ＋ ボケ→シャープ ----
        float ln = EaseOut(Progress(t, LogoInStart, LogoInEnd));
        lnText.alpha = ln;
        lnGroup.localScale = Vector3.one * Mathf.Lerp(0.95f, 1f, ln);

        // 複製を大きめ・薄めに重ね、中盤だけ滲ませてから解像させる。
        // セリフ体の細い線が潰れないよう、サンセリフ時より滲みを弱くしている。
        float soft = 1f - ln;
        lnSoft1.alpha = ln * soft * 0.70f;
        lnSoft2.alpha = ln * soft * 0.40f;
        lnSoft1.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.035f, 1f, ln);
        lnSoft2.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.075f, 1f, ln);

        // ---- 罫線：Studioと一緒に、中央から横へ開く ----
        float ruleOpen = EaseOut(Progress(t, StudioInStart, StudioInEnd + 0.15f));
        ruleImage.rectTransform.sizeDelta = new Vector2(RuleWidth * ruleOpen, 1f);
        Color ruleColor = RuleColor;
        ruleColor.a *= ruleOpen;
        ruleImage.color = ruleColor;

        // ---- Studio：少し遅れて、わずかに下から ----
        float studio = EaseOut(Progress(t, StudioInStart, StudioInEnd));
        studioText.alpha = studio;
        studioText.rectTransform.anchoredPosition =
            studioRestPosition + new Vector2(0f, Mathf.Lerp(-10f, 0f, studio));

        // ---- 保持：1%だけゆっくり拡大 → 退場でもう少し ----
        float hold = Progress(t, StudioInEnd, HoldEnd);
        float exit = Progress(t, FadeOutStart, FadeOutEnd);
        logoRoot.localScale = Vector3.one *
            Mathf.Lerp(Mathf.Lerp(1f, 1.010f, hold), 1.025f, exit);

        // ---- 光沢：1回だけ横切る ----
        float sweep = Progress(t, SheenStart, SheenEnd);
        if (sweep > 0f && sweep < 1f)
        {
            float x = Mathf.Lerp(-SheenHalfTravel, SheenHalfTravel, EaseInOut(sweep));
            sheenClip.anchoredPosition = new Vector2(x, 42f);
            // マスクが動いても文字は止まって見えるように逆オフセット
            sheenText.rectTransform.anchoredPosition = new Vector2(-x, 0f);
            sheenText.alpha = Mathf.Sin(sweep * Mathf.PI) * 0.42f;
        }
        else
        {
            sheenText.alpha = 0f;
        }

        // ---- 全体のフェード ----
        logoGroup.alpha = 1f - exit;
    }

    private static bool SkipRequested()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null &&
            (keyboard.spaceKey.wasPressedThisFrame ||
             keyboard.enterKey.wasPressedThisFrame ||
             keyboard.numpadEnterKey.wasPressedThisFrame ||
             keyboard.escapeKey.wasPressedThisFrame))
            return true;

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            return true;

        Gamepad pad = Gamepad.current;
        return pad != null &&
            (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame);
    }

    // ================================================================
    // シーン遷移
    // ================================================================

    /// <summary>保持中に裏で読み込んでおき、演出の最後で切り替える（引っかかりを消す）。</summary>
    private void BeginPreload()
    {
        if (!SceneExists(nextSceneName))
        {
            Debug.LogError(
                $"[LNStudioSplash] 遷移先シーン「{nextSceneName}」がBuild Settingsにありません。");
            return;
        }

        preload = SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Single);
        if (preload != null) preload.allowSceneActivation = false;
    }

    private void GoToNextScene()
    {
        if (finished) return;
        finished = true;

        if (preload != null)
        {
            preload.allowSceneActivation = true;
            return;
        }

        if (SceneExists(nextSceneName)) SceneManager.LoadScene(nextSceneName);
    }

    private static bool SceneExists(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;

        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName)
                return true;
        }
        return false;
    }

    // ================================================================
    // 共通
    // ================================================================

    /// <summary>startからendの区間を0〜1へ。区間外は0または1で止まる。</summary>
    private static float Progress(float t, float start, float end)
    {
        if (end <= start) return t >= end ? 1f : 0f;
        return Mathf.Clamp01((t - start) / (end - start));
    }

    private static float EaseOut(float t) => 1f - Mathf.Pow(1f - t, 3f);

    private static float EaseInOut(float t) =>
        t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;

    private static void Center(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
