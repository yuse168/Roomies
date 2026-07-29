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

        // 黒背景（全画面ストレッチ）
        var bgGo = new GameObject("Black", typeof(RectTransform));
        bgGo.transform.SetParent(canvasGo.transform, false);
        StretchFull(bgGo.GetComponent<RectTransform>());
        var bg = bgGo.AddComponent<Image>();
        bg.color = UITheme.WarmBottom;

        // 「○日目」テキスト
        var dayGo = new GameObject("DayLabel", typeof(RectTransform));
        dayGo.transform.SetParent(canvasGo.transform, false);
        var dayRt = dayGo.GetComponent<RectTransform>();
        dayRt.anchorMin = new Vector2(0.5f, 0.5f);
        dayRt.anchorMax = new Vector2(0.5f, 0.5f);
        dayRt.anchoredPosition = new Vector2(0, 20);
        dayRt.sizeDelta = new Vector2(1200, 200);
        dayLabel = dayGo.AddComponent<TextMeshProUGUI>();
        dayLabel.text = "";
        dayLabel.fontSize = 120f;
        dayLabel.fontStyle = FontStyles.Bold;
        dayLabel.color = UITheme.Accent;
        dayLabel.alignment = TextAlignmentOptions.Center;

        // サブテキスト（朝です）
        var subGo = new GameObject("SubLabel", typeof(RectTransform));
        subGo.transform.SetParent(canvasGo.transform, false);
        var subRt = subGo.GetComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0.5f, 0.5f);
        subRt.anchorMax = new Vector2(0.5f, 0.5f);
        subRt.anchoredPosition = new Vector2(0, -90);
        subRt.sizeDelta = new Vector2(1200, 80);
        subLabel = subGo.AddComponent<TextMeshProUGUI>();
        subLabel.text = "あさ になりました";
        subLabel.fontSize = 44f;
        subLabel.color = UITheme.TextMain;
        subLabel.alignment = TextAlignmentOptions.Center;
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
        while (t < dur)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, t / dur);
            yield return null;
        }
        group.alpha = to;
    }
}
