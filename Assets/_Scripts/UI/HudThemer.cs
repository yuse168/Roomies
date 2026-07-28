using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GameRoomのHUD着せ替え。UIThemeBootstrapが自動生成する。
/// シーンは編集せず、既存のテキスト（DayText / Timer Text / SharedMoneyText）を
/// ランタイム生成した角丸カードへ移して整える。
///
///  ・左上：DAYカード（日数・朝夜 ＋ 残り時間 ＋ タイマー進捗バー）
///  ・その下：共同金庫カード
///  ・画面中央下：InteractText（シーンに存在しないため生成 → PlayerInteractが拾う）
///  ・右下：操作ヒント
/// </summary>
public class HudThemer : MonoBehaviour
{
    private Image timerFillBar;
    private TMP_Text moneyValueLabel;
    private RectTransform moneyCardRect;

    private void Awake()
    {
        var canvas = UITheme.CreateCanvas(transform, "HudCanvas", 100);

        BuildDayCard(canvas.transform);
        BuildMoneyCard(canvas.transform);
        BuildHintBar(canvas.transform);
    }

    private void OnEnable()
    {
        SharedMoneyManager.SharedMoneyChanged += OnSharedMoneyChanged;
    }

    private void OnDisable()
    {
        SharedMoneyManager.SharedMoneyChanged -= OnSharedMoneyChanged;
    }

    // ================================================================
    // 左上：DAYカード
    // ================================================================

    private void BuildDayCard(Transform parent)
    {
        var card = UITheme.Card(parent, "DayCard");
        var rt = card.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(24, -24);
        rt.sizeDelta = new Vector2(300, 128);

        // 既存のDayTextを移設して整える
        var dayGo = UITheme.FindDeep("DayText");
        if (dayGo != null && dayGo.TryGetComponent(out TMP_Text dayText))
        {
            MoveInto(dayText.rectTransform, card.transform,
                new Vector2(22, -14), new Vector2(256, 44));
            dayText.fontSize  = 34f;
            dayText.fontStyle = FontStyles.Bold;
            dayText.color     = UITheme.TextMain;
            dayText.alignment = TextAlignmentOptions.Left;
            dayText.enableAutoSizing = false;
        }

        // 既存のTimer Textを移設
        var timerGo = UITheme.FindDeep("Timer Text");
        if (timerGo != null && timerGo.TryGetComponent(out TMP_Text timerText))
        {
            MoveInto(timerText.rectTransform, card.transform,
                new Vector2(22, -60), new Vector2(256, 34));
            timerText.fontSize  = 26f;
            timerText.fontStyle = FontStyles.Bold;
            timerText.color     = new Color(1f, 0.76f, 0.29f);
            timerText.alignment = TextAlignmentOptions.Left;
            timerText.enableAutoSizing = false;
        }

        // タイマー進捗バー
        var barBg = new GameObject("TimerBar", typeof(RectTransform));
        barBg.transform.SetParent(card.transform, false);
        var barBgRt = barBg.GetComponent<RectTransform>();
        barBgRt.anchorMin = new Vector2(0f, 0f);
        barBgRt.anchorMax = new Vector2(1f, 0f);
        barBgRt.pivot     = new Vector2(0.5f, 0f);
        barBgRt.anchoredPosition = new Vector2(0, 16);
        barBgRt.sizeDelta = new Vector2(-44, 8);
        var barBgImg = barBg.AddComponent<Image>();
        barBgImg.sprite = UITheme.RoundedSprite;
        barBgImg.type   = Image.Type.Sliced;
        barBgImg.pixelsPerUnitMultiplier = 8f;
        barBgImg.color  = new Color(1f, 1f, 1f, 0.10f);

        var fill = new GameObject("Fill", typeof(RectTransform));
        fill.transform.SetParent(barBg.transform, false);
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        timerFillBar = fill.AddComponent<Image>();
        timerFillBar.sprite = UITheme.RoundedSprite;
        timerFillBar.type   = Image.Type.Sliced;
        timerFillBar.pixelsPerUnitMultiplier = 8f;
        timerFillBar.color  = UITheme.Accent;
    }

    // ================================================================
    // 左上その2：共同金庫カード
    // ================================================================

    private void BuildMoneyCard(Transform parent)
    {
        var card = UITheme.Card(parent, "MoneyCard");
        var rt = card.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(24, -164);
        rt.sizeDelta = new Vector2(300, 86);
        moneyCardRect = rt;

        var caption = UITheme.Label(card.transform, "Caption", "共同金庫",
            18f, UITheme.TextSub, TextAlignmentOptions.Left, bold: true);
        var capRt = caption.rectTransform;
        capRt.anchorMin = new Vector2(0f, 1f);
        capRt.anchorMax = new Vector2(0f, 1f);
        capRt.pivot     = new Vector2(0f, 1f);
        capRt.anchoredPosition = new Vector2(22, -10);
        capRt.sizeDelta = new Vector2(256, 24);

        // 金額はカード内の自前ラベルに表示する。
        // ※シーンのSharedMoneyTextにはNetworkObjectが付いており、
        //   spawn済みNetworkObjectはNGOが親子関係を管理するため移設できない。
        //   そのため元のテキストは非表示にし、Updateで値をミラーする。
        moneyValueLabel = UITheme.Label(card.transform, "MoneyValue", "¥0",
            32f, UITheme.Gold, TextAlignmentOptions.Left, bold: true);
        var valueRt = moneyValueLabel.rectTransform;
        valueRt.anchorMin = new Vector2(0f, 1f);
        valueRt.anchorMax = new Vector2(0f, 1f);
        valueRt.pivot     = new Vector2(0f, 1f);
        valueRt.anchoredPosition = new Vector2(22, -34);
        valueRt.sizeDelta = new Vector2(256, 42);

        // 元のSharedMoneyTextはコンポーネントだけ無効化（GameObjectは触らない）
        var moneyGo = UITheme.FindDeep("SharedMoneyText");
        if (moneyGo != null && moneyGo.TryGetComponent(out TMP_Text moneyText))
        {
            moneyText.enabled = false;
        }
    }

    private void OnSharedMoneyChanged(int oldValue, int newValue)
    {
        int delta = newValue - oldValue;
        ShowMoneyDelta(delta);
    }

    /// <summary>共同金庫カードの横へ増減額をポップ表示する。</summary>
    public void ShowMoneyDelta(int delta)
    {
        if (delta == 0 || moneyCardRect == null) return;
        StartCoroutine(AnimateMoneyDelta(delta));
    }

    private IEnumerator AnimateMoneyDelta(int delta)
    {
        bool gained = delta > 0;

        var pill = new GameObject("MoneyDelta", typeof(RectTransform));
        pill.transform.SetParent(moneyCardRect, false);

        var rt = pill.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(310f, -52f);
        rt.sizeDelta = new Vector2(174f, 44f);

        var image = pill.AddComponent<Image>();
        image.sprite = UITheme.RoundedSprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 2f;
        image.color = gained
            ? new Color(0.18f, 0.76f, 0.38f, 0.96f)
            : new Color(0.92f, 0.28f, 0.34f, 0.96f);
        image.raycastTarget = false;
        UITheme.AddShadow(pill);

        string sign = gained ? "+" : "-";
        string amount = sign + "¥" + Mathf.Abs(delta).ToString("N0");
        var label = UITheme.Label(pill.transform, "Amount", amount,
            26f, Color.white, TextAlignmentOptions.Center, bold: true);
        label.raycastTarget = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = 18f;
        label.fontSizeMax = 26f;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(10f, 4f);
        label.rectTransform.offsetMax = new Vector2(-10f, -4f);

        var group = pill.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        const float duration = 1.65f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // 最初にポンと出て、少し上へ浮きながら最後に消える。
            group.alpha = t < 0.12f
                ? t / 0.12f
                : 1f - Mathf.Clamp01((t - 0.62f) / 0.38f);

            float pop = t < 0.18f
                ? Mathf.Lerp(0.72f, 1.08f, t / 0.18f)
                : Mathf.Lerp(1.08f, 1f, Mathf.Clamp01((t - 0.18f) / 0.18f));
            rt.localScale = Vector3.one * pop;

            float rise = 42f * (1f - Mathf.Pow(1f - t, 2f));
            rt.anchoredPosition = new Vector2(310f, -52f + rise);

            yield return null;
        }

        Destroy(pill);
    }

    // ※インタラクト表示（InteractText）はプレイヤープレハブ内にあり、
    //   PlayerInteractがUITheme.BuildInteractChipでチップ化して使う。

    // ================================================================
    // 右下：操作ヒント
    // ================================================================

    private void BuildHintBar(Transform parent)
    {
        var label = UITheme.Label(parent, "HintBar",
            "TAB みんなの稼ぎ　　N 時間を進める　　P 家具編集（夜）",
            18f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.Right);
        var rt = label.rectTransform;
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot     = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-24, 16);
        rt.sizeDelta = new Vector2(700, 26);
        UITheme.AddTextOutline(label, 0.15f);
    }

    // ================================================================
    // 共通
    // ================================================================

    /// <summary>既存UIを新しい親に移して位置・サイズを整える。</summary>
    private static void MoveInto(RectTransform target, Transform newParent,
                                 Vector2 anchoredPos, Vector2 size)
    {
        target.SetParent(newParent, false);
        target.anchorMin = new Vector2(0f, 1f);
        target.anchorMax = new Vector2(0f, 1f);
        target.pivot     = new Vector2(0f, 1f);
        target.anchoredPosition = anchoredPos;
        target.sizeDelta = size;
        target.localScale = Vector3.one;
    }

    private void Update()
    {
        // 共同金庫の金額ミラー（SharedMoneyTextは移設できないため値だけ写す）
        if (moneyValueLabel != null && SharedMoneyManager.Instance != null)
        {
            moneyValueLabel.text = "¥" + SharedMoneyManager.Instance.CurrentMoney.ToString("N0");
        }

        // タイマー進捗バー（残り時間が少ないと赤く）
        if (timerFillBar == null) return;

        var dm = DayManager.Instance;
        if (dm == null || dm.TurnDurationSeconds <= 0f) return;

        float pct = Mathf.Clamp01(dm.RemainingSeconds / dm.TurnDurationSeconds);

        var rt = timerFillBar.rectTransform;
        rt.anchorMax = new Vector2(Mathf.Max(pct, 0.02f), 1f);

        timerFillBar.color = pct < 0.2f
            ? Color.Lerp(UITheme.Red, UITheme.Accent, pct / 0.2f)
            : UITheme.Accent;
    }
}
