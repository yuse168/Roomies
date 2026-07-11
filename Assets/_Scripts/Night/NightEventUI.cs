using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 夜イベント・トラブルの告知バナー。
/// シーンに配置不要：NightEventManagerがランタイムで生成する。
///
/// 見た目：角丸パネル＋ドロップシャドウ＋スタイル色のアイコンバッジ＋上端のアクセントライン。
/// 角丸はコード生成したスプライト（9-slice）なので画像アセット不要。
/// 演出：上からポップイン（オーバーシュート）→ 数秒表示 → 上へ抜けながらフェードアウト。
/// 連続で来たメッセージはキューに積んで順番に表示する。
/// </summary>
public class NightEventUI : MonoBehaviour
{
    private CanvasGroup group;
    private RectTransform panelRt;
    private Image accentLine;
    private Image iconBadge;
    private TextMeshProUGUI iconLabel;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI bodyLabel;

    // 演出のタイミング
    private const float InTime   = 0.45f;
    private const float HoldTime = 3.6f;
    private const float OutTime  = 0.35f;

    // バナーの定位置（上端からの距離）
    private const float RestY = -70f;

    // バナー色（イベントの種類ごとの雰囲気分け）
    public static readonly Color ColorInfo   = new Color(0.30f, 0.62f, 0.95f); // 青（お知らせ）
    public static readonly Color ColorDanger = new Color(0.93f, 0.33f, 0.33f); // 赤（請求・トラブル）
    public static readonly Color ColorFun    = new Color(0.98f, 0.65f, 0.18f); // オレンジ（おもしろ系）
    public static readonly Color ColorPeace  = new Color(0.36f, 0.78f, 0.52f); // 緑（平和）

    private readonly Queue<(string title, string body, byte style)> queue
        = new Queue<(string, string, byte)>();
    private bool playing;

    private void Awake()
    {
        BuildUI();
    }

    // ================================================================
    // UI構築
    // ================================================================

    private void BuildUI()
    {
        var canvasGo = new GameObject("NightEventCanvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000; // DayTransition(10000)よりは下

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        group = canvasGo.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        // ---- 角丸パネル ----
        var panelGo = new GameObject("Banner", typeof(RectTransform));
        panelGo.transform.SetParent(canvasGo.transform, false);
        panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 1f);
        panelRt.anchorMax = new Vector2(0.5f, 1f);
        panelRt.pivot     = new Vector2(0.5f, 1f);
        panelRt.anchoredPosition = new Vector2(0, RestY);
        panelRt.sizeDelta = new Vector2(800, 150);

        var panelImg = panelGo.AddComponent<Image>();
        panelImg.sprite = UITheme.RoundedSprite;
        panelImg.type   = Image.Type.Sliced;
        panelImg.color  = new Color(0.07f, 0.08f, 0.14f, 0.94f);

        // ドロップシャドウ（下方向にふわっと）
        var shadow = panelGo.AddComponent<Shadow>();
        shadow.effectColor    = new Color(0f, 0f, 0f, 0.45f);
        shadow.effectDistance = new Vector2(0f, -6f);

        // ---- 上端のアクセントライン ----
        var lineGo = new GameObject("AccentLine", typeof(RectTransform));
        lineGo.transform.SetParent(panelGo.transform, false);
        var lineRt = lineGo.GetComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0f, 1f);
        lineRt.anchorMax = new Vector2(1f, 1f);
        lineRt.pivot     = new Vector2(0.5f, 1f);
        lineRt.anchoredPosition = new Vector2(0, -3);
        lineRt.sizeDelta = new Vector2(-24, 6);
        accentLine = lineGo.AddComponent<Image>();
        accentLine.sprite = UITheme.RoundedSprite;
        accentLine.type   = Image.Type.Sliced;
        // 高さ6pxに対して角丸半径が大きすぎるので、9-sliceの縮尺を上げて相対的に小さくする
        accentLine.pixelsPerUnitMultiplier = 8f;

        // ---- アイコンバッジ（角丸スクエア＋記号） ----
        var badgeGo = new GameObject("IconBadge", typeof(RectTransform));
        badgeGo.transform.SetParent(panelGo.transform, false);
        var badgeRt = badgeGo.GetComponent<RectTransform>();
        badgeRt.anchorMin = new Vector2(0f, 0.5f);
        badgeRt.anchorMax = new Vector2(0f, 0.5f);
        badgeRt.pivot     = new Vector2(0.5f, 0.5f);
        badgeRt.anchoredPosition = new Vector2(66, -2);
        badgeRt.sizeDelta = new Vector2(76, 76);
        iconBadge = badgeGo.AddComponent<Image>();
        iconBadge.sprite = UITheme.RoundedSprite;
        iconBadge.type   = Image.Type.Sliced;
        iconBadge.pixelsPerUnitMultiplier = 1.6f;

        var iconTextGo = new GameObject("Glyph", typeof(RectTransform));
        iconTextGo.transform.SetParent(badgeGo.transform, false);
        var iconTextRt = iconTextGo.GetComponent<RectTransform>();
        iconTextRt.anchorMin = Vector2.zero;
        iconTextRt.anchorMax = Vector2.one;
        iconTextRt.offsetMin = Vector2.zero;
        iconTextRt.offsetMax = Vector2.zero;
        iconLabel = iconTextGo.AddComponent<TextMeshProUGUI>();
        iconLabel.fontSize  = 44f;
        iconLabel.fontStyle = FontStyles.Bold;
        iconLabel.color     = Color.white;
        iconLabel.alignment = TextAlignmentOptions.Center;

        // ---- タイトル ----
        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(panelGo.transform, false);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot     = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(42, -26);
        titleRt.sizeDelta = new Vector2(-200, 52);
        titleLabel = titleGo.AddComponent<TextMeshProUGUI>();
        titleLabel.fontSize  = 38f;
        titleLabel.fontStyle = FontStyles.Bold;
        titleLabel.color     = Color.white;
        titleLabel.alignment = TextAlignmentOptions.Left;
        titleLabel.textWrappingMode = TextWrappingModes.NoWrap;
        titleLabel.overflowMode = TextOverflowModes.Ellipsis;

        // ---- 本文 ----
        var bodyGo = new GameObject("Body", typeof(RectTransform));
        bodyGo.transform.SetParent(panelGo.transform, false);
        var bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0f, 0f);
        bodyRt.anchorMax = new Vector2(1f, 0f);
        bodyRt.pivot     = new Vector2(0.5f, 0f);
        bodyRt.anchoredPosition = new Vector2(42, 16);
        bodyRt.sizeDelta = new Vector2(-200, 60);
        bodyLabel = bodyGo.AddComponent<TextMeshProUGUI>();
        bodyLabel.fontSize  = 26f;
        bodyLabel.color     = new Color(0.78f, 0.81f, 0.90f);
        bodyLabel.alignment = TextAlignmentOptions.TopLeft;
    }

    // ================================================================
    // 表示
    // ================================================================

    /// <summary>バナーを表示する（表示中なら順番待ちに積む）。styleはNightEventManager.Style～。</summary>
    public void Show(string title, string body, byte style)
    {
        queue.Enqueue((title, body, style));
        if (!playing) StartCoroutine(PlayQueue());
    }

    private static Color ColorFor(byte style)
    {
        switch (style)
        {
            case NightEventManager.StyleDanger: return ColorDanger;
            case NightEventManager.StyleFun:    return ColorFun;
            case NightEventManager.StylePeace:  return ColorPeace;
            default:                            return ColorInfo;
        }
    }

    private static string GlyphFor(byte style)
    {
        switch (style)
        {
            case NightEventManager.StyleDanger: return "！";
            case NightEventManager.StyleFun:    return "☆";
            case NightEventManager.StylePeace:  return "♪";
            default:                            return "？";
        }
    }

    private void ApplyStyle(string title, string body, byte style)
    {
        Color accent = ColorFor(style);

        if (titleLabel != null)
        {
            titleLabel.text  = title;
            titleLabel.color = accent;
        }
        if (bodyLabel != null)   bodyLabel.text = body;
        if (accentLine != null)  accentLine.color = accent;
        if (iconBadge != null)   iconBadge.color = new Color(accent.r, accent.g, accent.b, 0.92f);
        if (iconLabel != null)   iconLabel.text = GlyphFor(style);
    }

    private IEnumerator PlayQueue()
    {
        playing = true;

        while (queue.Count > 0)
        {
            var (title, body, style) = queue.Dequeue();
            ApplyStyle(title, body, style);

            // ---- ポップイン（上から、少しオーバーシュート） ----
            float t = 0f;
            Vector2 fromPos = new Vector2(0, RestY + 90f);
            Vector2 toPos   = new Vector2(0, RestY);
            while (t < InTime)
            {
                t += Time.deltaTime;
                float k = EaseOutBack(Mathf.Clamp01(t / InTime));
                if (panelRt != null)
                {
                    panelRt.anchoredPosition = Vector2.LerpUnclamped(fromPos, toPos, k);
                    panelRt.localScale = Vector3.one * Mathf.LerpUnclamped(0.92f, 1f, k);
                }
                if (group != null) group.alpha = Mathf.Clamp01(t / (InTime * 0.5f));
                yield return null;
            }
            if (panelRt != null)
            {
                panelRt.anchoredPosition = toPos;
                panelRt.localScale = Vector3.one;
            }
            if (group != null) group.alpha = 1f;

            yield return new WaitForSeconds(HoldTime);

            // ---- 上へ抜けながらフェードアウト ----
            t = 0f;
            while (t < OutTime)
            {
                t += Time.deltaTime;
                float k = t / OutTime;
                if (panelRt != null)
                    panelRt.anchoredPosition = Vector2.Lerp(toPos, fromPos + new Vector2(0, -50f), k * k);
                if (group != null) group.alpha = 1f - k;
                yield return null;
            }
            if (group != null) group.alpha = 0f;
        }

        playing = false;
    }

    // 弾むような到着（overshoot付きイージング）
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}
