using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 3日目終了時の家賃支払いを全画面で見せる演出。
/// DayManagerが各クライアント上でランタイム生成するため、Scene設定は不要。
/// </summary>
public sealed class RentPaymentUI : MonoBehaviour
{
    public const float SuccessDuration = 4.7f;
    public const float FailureDuration = 5.8f;

    private CanvasGroup group;
    private Image background;
    private Image card;
    private Image progressFill;
    private TMP_Text rentLabel;
    private TMP_Text balanceLabel;
    private TMP_Text statusLabel;
    private TMP_Text resultLabel;
    private Vector2 cardBasePosition;

    private static readonly Color Dark = UITheme.WarmBottom;
    private static readonly Color Orange = UITheme.Accent;
    private static readonly Color Green = UITheme.Green;
    private static readonly Color Red = UITheme.Red;

    private void Awake()
    {
        BuildUI();
    }

    public void Play(int rent, int balance, bool canPay)
    {
        StopAllCoroutines();
        StartCoroutine(PlayRoutine(rent, balance, canPay));
    }

    public void Hide()
    {
        StopAllCoroutines();
        if (group != null) group.alpha = 0f;
        if (resultLabel != null) resultLabel.gameObject.SetActive(false);
    }

    private void BuildUI()
    {
        Canvas canvas = UITheme.CreateCanvas(transform, "RentPaymentCanvas", 9500);

        group = canvas.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = true;
        group.blocksRaycasts = true;

        var bgGo = new GameObject("Background", typeof(RectTransform));
        bgGo.transform.SetParent(canvas.transform, false);
        Stretch(bgGo.GetComponent<RectTransform>());
        background = bgGo.AddComponent<Image>();
        background.color = Dark;

        card = UITheme.Card(canvas.transform, "RentCard");
        var cardRt = card.rectTransform;
        cardRt.anchorMin = cardRt.anchorMax = cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = cardBasePosition = Vector2.zero;
        cardRt.sizeDelta = new Vector2(1120f, 650f);
        card.color = UITheme.Panel;

        var badge = CreateImage(card.transform, "Badge", Orange);
        SetRect(badge.rectTransform, new Vector2(0.5f, 1f),
            new Vector2(420f, 62f), new Vector2(0f, -42f));
        var badgeText = CreateLabel(badge.transform, "Text", "3日目・さいごのお会計", 27f,
            new Color(0.11f, 0.085f, 0.035f), true);
        Stretch(badgeText.rectTransform, 10f);

        var title = CreateLabel(card.transform, "Title", "家賃支払いフェーズ", 66f,
            UITheme.TextMain, true);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f),
            new Vector2(1000f, 100f), new Vector2(0f, -135f));

        rentLabel = CreateLabel(card.transform, "Rent", "", 52f, UITheme.Gold, true);
        SetRect(rentLabel.rectTransform, new Vector2(0.5f, 1f),
            new Vector2(900f, 75f), new Vector2(0f, -245f));

        balanceLabel = CreateLabel(card.transform, "Balance", "", 34f,
            UITheme.TextSub, true);
        SetRect(balanceLabel.rectTransform, new Vector2(0.5f, 1f),
            new Vector2(900f, 55f), new Vector2(0f, -323f));

        var barBg = CreateImage(card.transform, "ProgressBar", new Color(1f, 1f, 1f, 0.10f));
        SetRect(barBg.rectTransform, new Vector2(0.5f, 0.5f),
            new Vector2(820f, 24f), new Vector2(0f, -55f));

        var fill = CreateImage(barBg.transform, "Fill", Orange);
        progressFill = fill;
        var fillRt = fill.rectTransform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.pivot = new Vector2(0f, 0.5f);
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;

        statusLabel = CreateLabel(card.transform, "Status", "引き落としを確認中…", 36f,
            UITheme.TextMain, true);
        SetRect(statusLabel.rectTransform, new Vector2(0.5f, 0f),
            new Vector2(950f, 70f), new Vector2(0f, 85f));

        resultLabel = CreateLabel(canvas.transform, "Result", "", 150f, Color.white, true);
        var resultRt = resultLabel.rectTransform;
        resultRt.anchorMin = resultRt.anchorMax = resultRt.pivot = new Vector2(0.5f, 0.5f);
        resultRt.anchoredPosition = Vector2.zero;
        resultRt.sizeDelta = new Vector2(1500f, 240f);
        resultLabel.outlineWidth = 0.24f;
        resultLabel.outlineColor = new Color32(20, 0, 0, 230);
        resultLabel.gameObject.SetActive(false);
    }

    private IEnumerator PlayRoutine(int rent, int balance, bool canPay)
    {
        rentLabel.text = $"請求額　¥{rent:N0}";
        balanceLabel.text = $"共有口座　¥{balance:N0}";
        statusLabel.text = "引き落としを確認中…";
        statusLabel.color = UITheme.TextMain;
        resultLabel.gameObject.SetActive(false);
        card.rectTransform.anchoredPosition = cardBasePosition;
        card.rectTransform.localScale = Vector3.one;
        card.color = UITheme.Panel;
        background.color = Dark;

        var fillRt = progressFill.rectTransform;
        fillRt.anchorMax = new Vector2(0f, 1f);
        progressFill.color = canPay ? Orange : Red;

        yield return FadeGroup(0f, 1f, 0.35f);

        float elapsed = 0f;
        const float checkTime = 1.4f;
        while (elapsed < checkTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / checkTime);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            fillRt.anchorMax = new Vector2(eased, 1f);
            yield return null;
        }

        if (canPay)
            yield return PlaySuccess(rent, balance);
        else
            yield return PlayFailure();
    }

    private IEnumerator PlaySuccess(int rent, int balance)
    {
        statusLabel.text = "支払い完了！";
        statusLabel.color = Green;
        balanceLabel.text = $"共有口座　¥{Mathf.Max(0, balance - rent):N0}";
        card.color = new Color(0.075f, 0.16f, 0.075f, 0.98f);

        float elapsed = 0f;
        while (elapsed < 0.4f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 0.4f);
            float scale = Mathf.Lerp(1.10f, 1f, t);
            card.rectTransform.localScale = Vector3.one * scale;
            yield return null;
        }

        yield return new WaitForSecondsRealtime(1.8f);
        yield return FadeGroup(1f, 0f, 0.5f);
    }

    private IEnumerator PlayFailure()
    {
        statusLabel.text = "残高が足りない…！";
        statusLabel.color = Red;

        // 引き落としを拒否されたように、カードを横へ激しく揺らして赤く点滅。
        float elapsed = 0f;
        const float rejectTime = 0.8f;
        while (elapsed < rejectTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / rejectTime);
            float power = 1f - t;
            float x = Mathf.Sin(elapsed * 58f) * 34f * power;
            card.rectTransform.anchoredPosition = cardBasePosition + new Vector2(x, 0f);

            float flash = Mathf.PingPong(elapsed * 7f, 1f);
            background.color = Color.Lerp(Dark, new Color(0.38f, 0.015f, 0.03f, 1f), flash * 0.8f);
            yield return null;
        }
        card.rectTransform.anchoredPosition = cardBasePosition;

        yield return new WaitForSecondsRealtime(0.35f);

        resultLabel.text = "失敗！！";
        resultLabel.color = Red;
        resultLabel.gameObject.SetActive(true);
        card.gameObject.SetActive(false);

        elapsed = 0f;
        const float popTime = 0.45f;
        while (elapsed < popTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / popTime);
            float scale = Mathf.Lerp(2.0f, 1f, 1f - Mathf.Pow(1f - t, 3f));
            resultLabel.rectTransform.localScale = Vector3.one * scale;
            background.color = Color.Lerp(new Color(0.62f, 0f, 0.04f, 1f), Dark, t);
            yield return null;
        }

        resultLabel.rectTransform.localScale = Vector3.one;
        yield return new WaitForSecondsRealtime(1.7f);
        yield return FadeGroup(1f, 0f, 0.5f);

        card.gameObject.SetActive(true);
    }

    private IEnumerator FadeGroup(float from, float to, float duration)
    {
        float elapsed = 0f;
        group.alpha = from;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        group.alpha = to;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.sprite = UITheme.RoundedSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateLabel(
        Transform parent, string name, string text, float size, Color color, bool bold)
    {
        var label = UITheme.Label(parent, name, text, size, color, TextAlignmentOptions.Center, bold);
        label.raycastTarget = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Max(18f, size * 0.55f);
        label.fontSizeMax = size;
        return label;
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }
}
