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
    // 濃色パネルの上に乗るので、暗い色ではなく明るい色を使う
    public static readonly Color ColorInfo   = UITheme.Cyan;
    public static readonly Color ColorDanger = UITheme.Red;
    public static readonly Color ColorFun    = UITheme.Pink;
    public static readonly Color ColorPeace  = UITheme.Lime;

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

        // ---- バナー（濃色の面＋太い白フチ。白いトースト通知にはしない） ----
        Image panel = UITheme.Surface(canvasGo.transform, "Banner", 30f, 5f);
        panelRt = panel.rectTransform;
        panelRt.anchorMin = new Vector2(0.5f, 1f);
        panelRt.anchorMax = new Vector2(0.5f, 1f);
        panelRt.pivot     = new Vector2(0.5f, 1f);
        panelRt.anchoredPosition = new Vector2(0, RestY);
        panelRt.sizeDelta = new Vector2(720, 128);

        // 種類を示す色は上端の細いラインではなく、下端の太いキャップで示す
        accentLine = UITheme.Chip(panelRt, "StyleCap", ColorInfo, 5f);
        var lineRt = accentLine.rectTransform;
        lineRt.anchorMin = new Vector2(0f, 0f);
        lineRt.anchorMax = new Vector2(1f, 0f);
        lineRt.pivot     = new Vector2(0.5f, 0f);
        lineRt.anchoredPosition = new Vector2(0f, 12f);
        lineRt.sizeDelta = new Vector2(-40f, 10f);

        // ---- アイコンバッジ（丸＋記号） ----
        iconBadge = UITheme.Chip(panelRt, "IconBadge", ColorInfo, 29f);
        var badgeRt = iconBadge.rectTransform;
        badgeRt.anchorMin = new Vector2(0f, 0.5f);
        badgeRt.anchorMax = new Vector2(0f, 0.5f);
        badgeRt.pivot     = new Vector2(0f, 0.5f);
        badgeRt.anchoredPosition = new Vector2(22f, 4f);
        badgeRt.sizeDelta = new Vector2(58f, 58f);

        iconLabel = UITheme.Label(badgeRt, "Glyph", "",
            32f, new Color(0.14f, 0.08f, 0.02f), TextAlignmentOptions.Center, bold: true);
        StretchFull(iconLabel.rectTransform);

        // ---- タイトル ----
        titleLabel = UITheme.Label(panelRt, "Title", "",
            30f, Color.white, TextAlignmentOptions.Left, bold: true);
        var titleRt = titleLabel.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot     = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(48, -18);
        titleRt.sizeDelta = new Vector2(-186, 40);
        titleLabel.textWrappingMode = TextWrappingModes.NoWrap;
        titleLabel.overflowMode = TextOverflowModes.Ellipsis;

        // ---- 本文 ----
        bodyLabel = UITheme.Label(panelRt, "Body", "",
            21f, new Color(1f, 1f, 1f, 0.80f), TextAlignmentOptions.TopLeft);
        var bodyRt = bodyLabel.rectTransform;
        bodyRt.anchorMin = new Vector2(0f, 0f);
        bodyRt.anchorMax = new Vector2(1f, 0f);
        bodyRt.pivot     = new Vector2(0.5f, 0f);
        bodyRt.anchoredPosition = new Vector2(48, 30);
        bodyRt.sizeDelta = new Vector2(-186, 46);
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
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

        // 種類は「下端のキャップ」と「丸バッジ」の色で示す。
        // タイトルは常に白にして、どの種類でも同じ読みやすさを保つ。
        if (titleLabel != null)
        {
            titleLabel.text  = title;
            titleLabel.color = Color.white;
        }
        if (bodyLabel != null)   bodyLabel.text = body;
        if (accentLine != null)  accentLine.color = accent;
        if (iconBadge != null)   iconBadge.color = accent;
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
