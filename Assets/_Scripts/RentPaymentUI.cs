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
    private Image background;   // 拒否演出用の赤いフラッシュ層（通常は透明）
    private Image card;         // パネルの白フチ
    private Image cardFill;     // パネルの内側の面（成功／失敗で色を変える）
    private Image progressFill;
    private TMP_Text rentLabel;
    private TMP_Text balanceLabel;
    private TMP_Text statusLabel;
    private TMP_Text resultLabel;
    private Vector2 cardBasePosition;

    private static readonly Color Clear = new Color(0.62f, 0f, 0.04f, 0f);
    private static readonly Color Orange = UITheme.Sun;
    private static readonly Color Green = UITheme.Lime;
    private static readonly Color Red = UITheme.Red;
    private static readonly Color PanelIdle    = UITheme.MenuInk;
    private static readonly Color PanelSuccess = new Color(0.07f, 0.24f, 0.13f, 0.95f);

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

        // 背景は他の全画面演出と同じ型（部屋がうっすら残る遮光）
        UITheme.StageBackdrop(canvas.transform, Orange, 0.9f);

        // 点滅演出用に、遮光の上へ重ねる赤いフラッシュ層を持つ
        var bgGo = new GameObject("Flash", typeof(RectTransform));
        bgGo.transform.SetParent(canvas.transform, false);
        Stretch(bgGo.GetComponent<RectTransform>());
        background = bgGo.AddComponent<Image>();
        background.color = Clear;
        background.raycastTarget = false;

        // 白い巨大カードをやめ、視線が一点に集まる濃色パネルにする
        card = UITheme.Surface(canvas.transform, "RentCard", 44f, 7f);
        cardFill = card.transform.Find("Fill").GetComponent<Image>();
        var cardRt = card.rectTransform;
        cardRt.anchorMin = cardRt.anchorMax = cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = cardBasePosition = Vector2.zero;
        cardRt.sizeDelta = new Vector2(940f, 560f);

        var badge = CreateChip(card.transform, "Badge", Orange, 26f);
        SetRect(badge.rectTransform, new Vector2(0.5f, 1f),
            new Vector2(400f, 54f), new Vector2(0f, -36f));
        var badgeText = CreateLabel(badge.transform, "Text", "3日目・さいごのお会計", 24f,
            new Color(0.16f, 0.09f, 0.02f), true);
        Stretch(badgeText.rectTransform, 12f);

        var title = CreateLabel(card.transform, "Title", "家賃支払いフェーズ", 46f,
            Color.white, true);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f),
            new Vector2(840f, 66f), new Vector2(0f, -104f));

        // 請求額が主役。この画面で一番大きい文字にする
        var rentCaption = CreateLabel(card.transform, "RentCaption", "請求額", 24f,
            new Color(1f, 1f, 1f, 0.66f), true);
        SetRect(rentCaption.rectTransform, new Vector2(0.5f, 1f),
            new Vector2(840f, 32f), new Vector2(0f, -176f));

        rentLabel = CreateLabel(card.transform, "Rent", "", 76f, UITheme.Sun, true);
        SetRect(rentLabel.rectTransform, new Vector2(0.5f, 1f),
            new Vector2(840f, 100f), new Vector2(0f, -208f));

        balanceLabel = CreateLabel(card.transform, "Balance", "", 30f,
            new Color(1f, 1f, 1f, 0.72f), true);
        SetRect(balanceLabel.rectTransform, new Vector2(0.5f, 1f),
            new Vector2(840f, 46f), new Vector2(0f, -292f));

        var barBg = CreateChip(card.transform, "ProgressBar",
            new Color(0f, 0f, 0f, 0.42f), 11f);
        SetRect(barBg.rectTransform, new Vector2(0.5f, 0.5f),
            new Vector2(760f, 22f), new Vector2(0f, -48f));

        progressFill = CreateChip(barBg.transform, "Fill", Orange, 11f);
        var fillRt = progressFill.rectTransform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.pivot = new Vector2(0f, 0.5f);
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;

        statusLabel = CreateLabel(card.transform, "Status", "引き落としを確認中…", 38f,
            Color.white, true);
        SetRect(statusLabel.rectTransform, new Vector2(0.5f, 0f),
            new Vector2(860f, 64f), new Vector2(0f, 58f));

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
        rentLabel.text = $"¥{rent:N0}";
        balanceLabel.text = $"共有口座　¥{balance:N0}";
        statusLabel.text = "引き落としを確認中…";
        statusLabel.color = Color.white;
        resultLabel.gameObject.SetActive(false);
        card.rectTransform.anchoredPosition = cardBasePosition;
        card.rectTransform.localScale = Vector3.one;
        if (cardFill != null) cardFill.color = PanelIdle;
        background.color = Clear;

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
        if (cardFill != null) cardFill.color = PanelSuccess;

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
            background.color = Color.Lerp(Clear, new Color(0.62f, 0f, 0.04f, 0.62f), flash);
            yield return null;
        }
        background.color = Clear;
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
            background.color = Color.Lerp(new Color(0.62f, 0f, 0.04f, 0.86f), Clear, t);
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

    private static Image CreateChip(Transform parent, string name, Color color, float radius)
    {
        return UITheme.Chip(parent, name, color, radius);
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
