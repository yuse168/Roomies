using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 夜→朝の切り替え時に表示する全画面の暗転＋「○日目」演出。
/// シーンに配置不要：DayManagerがランタイムで生成する。
/// </summary>
public class DayTransitionUI : MonoBehaviour
{
    private CanvasGroup    group;
    private TextMeshProUGUI dayLabel;
    private TextMeshProUGUI subLabel;

    // 演出のタイミング
    private const float FadeInTime  = 0.6f;
    private const float HoldTime    = 2.0f;
    private const float FadeOutTime = 0.8f;

    /// <summary>暗転が最も濃くなる（リスポーンに適した）までのおおよその時間。</summary>
    public static float BlackoutDelay => FadeInTime + 0.3f;

    private void Awake()
    {
        BuildUI();
    }

    private void BuildUI()
    {
        // 最前面に出すためのCanvas
        var canvasGo = new GameObject("DayTransitionCanvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10000;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        group = canvasGo.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        // 背景。この演出中にプレイヤーをリスポーンさせるため、
        // 他の全画面演出と違い透過させず完全遮光にする（ベタ塗りではなくグラデーション）。
        UITheme.StageBackdrop(canvasGo.transform, UITheme.Sun, 1f);

        // 「○日目」テキスト
        var dayGo = new GameObject("DayLabel", typeof(RectTransform));
        dayGo.transform.SetParent(canvasGo.transform, false);
        var dayRt = dayGo.GetComponent<RectTransform>();
        dayRt.anchorMin = new Vector2(0.5f, 0.5f);
        dayRt.anchorMax = new Vector2(0.5f, 0.5f);
        dayRt.anchoredPosition = new Vector2(0, 30);
        dayRt.sizeDelta = new Vector2(1400, 200);
        dayLabel = dayGo.AddComponent<TextMeshProUGUI>();
        dayLabel.text = "";
        dayLabel.fontSize = 96f;
        dayLabel.fontStyle = FontStyles.Bold;
        dayLabel.color = Color.white;
        dayLabel.alignment = TextAlignmentOptions.Center;
        dayLabel.characterSpacing = -2f;
        dayLabel.enableAutoSizing = true;
        dayLabel.fontSizeMin = 56f;
        dayLabel.fontSizeMax = 96f;
        dayLabel.textWrappingMode = TextWrappingModes.NoWrap;

        // サブテキストは板の上の説明文ではなく、色付きのチップに入れる
        Image chip = UITheme.Chip(canvasGo.transform, "SubChip", UITheme.Sun, 30f);
        var chipRt = chip.rectTransform;
        chipRt.anchorMin = chipRt.anchorMax = chipRt.pivot = new Vector2(0.5f, 0.5f);
        chipRt.anchoredPosition = new Vector2(0, -92);
        chipRt.sizeDelta = new Vector2(360, 68);

        subLabel = UITheme.Label(chip.transform, "SubLabel", "あさ になりました",
            32f, new Color(0.18f, 0.10f, 0.02f), TextAlignmentOptions.Center, bold: true);
        subLabel.enableAutoSizing = true;
        subLabel.fontSizeMin = 20f;
        subLabel.fontSizeMax = 32f;
        subLabel.textWrappingMode = TextWrappingModes.NoWrap;
        StretchFull(subLabel.rectTransform);
        subLabel.rectTransform.offsetMin = new Vector2(18f, 4f);
        subLabel.rectTransform.offsetMax = new Vector2(-18f, -4f);
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>「○日目」演出を再生する。</summary>
    public void Play(int day)
    {
        if (dayLabel != null) dayLabel.text = $"{day}日目";
        StopAllCoroutines();
        StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        yield return Fade(0f, 1f, FadeInTime);
        yield return new WaitForSeconds(HoldTime);
        yield return Fade(1f, 0f, FadeOutTime);
    }

    private IEnumerator Fade(float from, float to, float dur)
    {
        if (group == null) yield break;
        float t = 0f;
        group.alpha = from;
        if (to > from && dayLabel != null)
            dayLabel.rectTransform.localScale = Vector3.one * 1.35f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / dur);
            group.alpha = Mathf.Lerp(from, to, p);
            if (to > from && dayLabel != null)
            {
                float eased = 1f - Mathf.Pow(1f - p, 3f);
                dayLabel.rectTransform.localScale =
                    Vector3.one * Mathf.Lerp(1.35f, 1f, eased);
            }
            yield return null;
        }
        group.alpha = to;
        if (dayLabel != null) dayLabel.rectTransform.localScale = Vector3.one;
    }
}
