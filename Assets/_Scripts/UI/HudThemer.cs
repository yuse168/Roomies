using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GameRoomのHUD着せ替え。UIThemeBootstrapが自動生成する。
/// シーンは編集せず、既存のテキスト（DayText / Timer Text / SharedMoneyText）を
/// ランタイム生成した面へ移して整える。
///
/// 設計方針：
///  ・白いカードを使わない。明るい部屋の上でも輪郭が消えないよう濃色＋太い白フチ
///  ・遠目でも一瞬で読める文字サイズにする（DAY 34px / 残り時間 40px / 残高 46px）
///  ・残り時間は5pxの罫線ではなく、太いバーで緊張感を出す
///  ・残高は「いまいくら」を主役に、必要額は添え物として小さく置く
///
///  ・左上：ステータス（日数・朝夜 ＋ 残り時間 ＋ 残り時間バー）
///  ・その下：共同口座（残高 ＋ 家賃チップ）
///  ・画面中央下：InteractText（PlayerInteractがUITheme.BuildInteractChipで包む）
/// </summary>
public class HudThemer : MonoBehaviour
{
    // 1920x1080基準
    private const float Margin      = 28f;
    private const float StatusWidth = 430f;
    private const float StatusHeight = 136f;
    private const float MoneyWidth  = 386f;
    private const float MoneyHeight = 172f;
    private const float BarHeight   = 16f;

    private Image timerFillBar;
    private TMP_Text moneyValueLabel;
    private TMP_Text moneyRequiredLabel;
    private Image rentChip;
    private TMP_Text rentChipLabel;
    private RectTransform moneyCardRect;
    private float rentPulse;

    private void Awake()
    {
        var canvas = UITheme.CreateCanvas(transform, "HudCanvas", 100);

        BuildStatusCard(canvas.transform);
        BuildMoneyCard(canvas.transform);
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
    // 左上：日数・朝夜・残り時間
    // ================================================================

    private void BuildStatusCard(Transform parent)
    {
        Image card = UITheme.Surface(parent, "StatusCard", 26f, 4f);
        var rt = card.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(Margin, -Margin);
        rt.sizeDelta = new Vector2(StatusWidth, StatusHeight);

        // 既存のDayTextを移設して整える
        var dayGo = UITheme.FindDeep("DayText");
        if (dayGo != null && dayGo.TryGetComponent(out TMP_Text dayText))
        {
            MoveInto(dayText.rectTransform, card.transform,
                new Vector2(26f, -18f), new Vector2(250f, 46f));
            dayText.fontSize  = 34f;
            dayText.fontStyle = FontStyles.Bold;
            dayText.color     = Color.white;
            dayText.alignment = TextAlignmentOptions.Left;
            dayText.enableAutoSizing = true;
            dayText.fontSizeMin = 22f;
            dayText.fontSizeMax = 34f;
            dayText.textWrappingMode = TextWrappingModes.NoWrap;
        }

        // 既存のTimer Textを移設（右上に大きく）
        var timerGo = UITheme.FindDeep("Timer Text");
        if (timerGo != null && timerGo.TryGetComponent(out TMP_Text timerText))
        {
            timerText.transform.SetParent(card.transform, false);
            var timerRt = timerText.rectTransform;
            timerRt.anchorMin = timerRt.anchorMax = timerRt.pivot = new Vector2(1f, 1f);
            timerRt.anchoredPosition = new Vector2(-26f, -16f);
            timerRt.sizeDelta = new Vector2(150f, 50f);
            timerRt.localScale = Vector3.one;
            timerText.fontSize  = 40f;
            timerText.fontStyle = FontStyles.Bold;
            timerText.color     = UITheme.Sun;
            timerText.alignment = TextAlignmentOptions.Right;
            timerText.enableAutoSizing = false;
            timerText.characterSpacing = 2f;
        }

        // 残り時間バー（細い罫線ではなく、掴めそうな太さにする）
        Image barBg = UITheme.Chip(card.transform, "TimerBar",
            new Color(0f, 0f, 0f, 0.42f), BarHeight * 0.5f);
        var barBgRt = barBg.rectTransform;
        barBgRt.anchorMin = new Vector2(0f, 0f);
        barBgRt.anchorMax = new Vector2(1f, 0f);
        barBgRt.pivot     = new Vector2(0.5f, 0f);
        barBgRt.anchoredPosition = new Vector2(0f, 22f);
        barBgRt.sizeDelta = new Vector2(-52f, BarHeight);

        timerFillBar = UITheme.Chip(barBg.transform, "Fill",
            UITheme.Sun, BarHeight * 0.5f);
        var fillRt = timerFillBar.rectTransform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
    }

    // ================================================================
    // その下：共同口座
    // ================================================================

    private void BuildMoneyCard(Transform parent)
    {
        Image card = UITheme.Surface(parent, "MoneyCard", 26f, 4f);
        var rt = card.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(Margin, -(Margin + StatusHeight + 16f));
        rt.sizeDelta = new Vector2(MoneyWidth, MoneyHeight);
        moneyCardRect = rt;

        var caption = UITheme.Label(card.transform, "Caption", "共同口座",
            20f, new Color(1f, 1f, 1f, 0.68f), TextAlignmentOptions.Left, bold: true);
        SetTopLeft(caption.rectTransform, new Vector2(26f, -16f), new Vector2(240f, 28f));

        // 金額はカード内の自前ラベルに表示する。
        // ※シーンのSharedMoneyTextにはNetworkObjectが付いており、
        //   spawn済みNetworkObjectはNGOが親子関係を管理するため移設できない。
        //   そのため元のテキストは非表示にし、Updateで値をミラーする。
        moneyValueLabel = UITheme.Label(card.transform, "MoneyValue", "¥0",
            46f, UITheme.Lime, TextAlignmentOptions.Left, bold: true);
        moneyValueLabel.enableAutoSizing = true;
        moneyValueLabel.fontSizeMin = 26f;
        moneyValueLabel.fontSizeMax = 46f;
        moneyValueLabel.textWrappingMode = TextWrappingModes.NoWrap;
        SetTopLeft(moneyValueLabel.rectTransform, new Vector2(26f, -44f), new Vector2(240f, 58f));

        // 必要額は添え物。主役の残高と同じ大きさにはしない
        moneyRequiredLabel = UITheme.Label(card.transform, "MoneyRequired", "",
            22f, new Color(1f, 1f, 1f, 0.62f), TextAlignmentOptions.Right, bold: true);
        moneyRequiredLabel.enableAutoSizing = false;
        moneyRequiredLabel.textWrappingMode = TextWrappingModes.NoWrap;
        var requiredRt = moneyRequiredLabel.rectTransform;
        requiredRt.anchorMin = requiredRt.anchorMax = requiredRt.pivot = new Vector2(1f, 1f);
        requiredRt.anchoredPosition = new Vector2(-26f, -58f);
        requiredRt.sizeDelta = new Vector2(180f, 32f);

        // 家賃の期限はチップにして、残りが少ないと色と鼓動で警告する
        rentChip = UITheme.Chip(card.transform, "RentChip", UITheme.Grape, 20f);
        var chipRt = rentChip.rectTransform;
        chipRt.anchorMin = chipRt.anchorMax = chipRt.pivot = new Vector2(0f, 0f);
        chipRt.anchoredPosition = new Vector2(26f, 20f);
        chipRt.sizeDelta = new Vector2(MoneyWidth - 52f, 42f);

        rentChipLabel = UITheme.Label(rentChip.transform, "Label", "家賃まで あと3日",
            21f, Color.white, TextAlignmentOptions.Center, bold: true);
        rentChipLabel.enableAutoSizing = true;
        rentChipLabel.fontSizeMin = 15f;
        rentChipLabel.fontSizeMax = 21f;
        rentChipLabel.textWrappingMode = TextWrappingModes.NoWrap;
        rentChipLabel.raycastTarget = false;
        var labelRt = rentChipLabel.rectTransform;
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(14f, 2f);
        labelRt.offsetMax = new Vector2(-14f, -2f);

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

    /// <summary>共同口座カードの横へ増減額をポップ表示する。</summary>
    public void ShowMoneyDelta(int delta)
    {
        if (delta == 0 || moneyCardRect == null) return;
        StartCoroutine(AnimateMoneyDelta(delta));
    }

    private IEnumerator AnimateMoneyDelta(int delta)
    {
        bool gained = delta > 0;
        Color color = gained ? UITheme.Lime : UITheme.Red;

        var pill = new GameObject("MoneyDelta", typeof(RectTransform));
        pill.transform.SetParent(moneyCardRect, false);

        var rt = pill.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(168f, 52f);

        // 厚みのあるピルで「増えた／減った」を物として見せる
        Vector2 basePosition = new Vector2(14f, -70f);
        rt.anchoredPosition = basePosition;

        var baseImage = pill.AddComponent<Image>();
        baseImage.sprite = UITheme.PillSprite;
        baseImage.color = UITheme.Shade(color, 0.45f);
        baseImage.raycastTarget = false;
        UITheme.SetCornerRadius(baseImage, 26f);

        var cap = UITheme.Chip(pill.transform, "Cap", color, 23f);
        var capRt = cap.rectTransform;
        capRt.anchorMin = Vector2.zero;
        capRt.anchorMax = Vector2.one;
        capRt.offsetMin = new Vector2(0f, 6f);
        capRt.offsetMax = Vector2.zero;

        string sign = gained ? "+" : "-";
        string amount = sign + "¥" + Mathf.Abs(delta).ToString("N0");
        var label = UITheme.Label(cap.transform, "Amount", amount,
            28f, gained ? new Color(0.06f, 0.16f, 0.06f) : Color.white,
            TextAlignmentOptions.Center, bold: true);
        label.raycastTarget = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = 18f;
        label.fontSizeMax = 28f;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(12f, 3f);
        label.rectTransform.offsetMax = new Vector2(-12f, -3f);

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
                ? Mathf.Lerp(0.72f, 1.10f, t / 0.18f)
                : Mathf.Lerp(1.10f, 1f, Mathf.Clamp01((t - 0.18f) / 0.18f));
            rt.localScale = Vector3.one * pop;

            float rise = 38f * (1f - Mathf.Pow(1f - t, 2f));
            rt.anchoredPosition = basePosition + new Vector2(0f, rise);

            yield return null;
        }

        Destroy(pill);
    }

    // ※インタラクト表示（InteractText）はプレイヤープレハブ内にあり、
    //   PlayerInteractがUITheme.BuildInteractChipでチップ化して使う。

    // ================================================================
    // 共通
    // ================================================================

    /// <summary>既存UIを新しい親に移して位置・サイズを整える。</summary>
    private static void MoveInto(RectTransform target, Transform newParent,
                                 Vector2 anchoredPos, Vector2 size)
    {
        target.SetParent(newParent, false);
        SetTopLeft(target, anchoredPos, size);
    }

    private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private void Update()
    {
        var dm = DayManager.Instance;

        UpdateMoney(dm);
        UpdateRentChip(dm);
        UpdateTimerBar(dm);
    }

    private void UpdateMoney(DayManager dm)
    {
        if (moneyValueLabel == null || SharedMoneyManager.Instance == null) return;

        int current  = SharedMoneyManager.Instance.CurrentMoney;
        int required = dm != null ? dm.CurrentRentTotal : 0;
        bool enough  = current >= required;

        moneyValueLabel.text  = $"¥{current:N0}";
        moneyValueLabel.color = enough ? UITheme.Lime : UITheme.Red;

        if (moneyRequiredLabel == null) return;

        if (required <= 0)
        {
            moneyRequiredLabel.text = "";
            return;
        }

        // 記号は日本語フォントに無い場合があるので使わず、言葉で示す
        moneyRequiredLabel.text = enough
            ? $"必要 ¥{required:N0}"
            : $"あと ¥{required - current:N0}";
        moneyRequiredLabel.color = enough
            ? new Color(1f, 1f, 1f, 0.62f)
            : UITheme.Sun;
    }

    private void UpdateRentChip(DayManager dm)
    {
        if (rentChipLabel == null || rentChip == null) return;
        if (dm == null || dm.DebugDay <= 0) return;

        int days = dm.DaysUntilRent;
        rentChipLabel.text = days <= 0
            ? "今夜が家賃の支払い日"
            : $"家賃まで あと{days}日";

        // 残り1日を切ったら赤くして鼓動させる
        bool urgent = days <= 1;
        if (urgent)
        {
            rentPulse += Time.unscaledDeltaTime * 3.4f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(rentPulse);
            rentChip.color = Color.Lerp(UITheme.Red, UITheme.Sun, pulse * 0.45f);
        }
        else
        {
            rentPulse = 0f;
            rentChip.color = days <= 2 ? UITheme.Sun : UITheme.Grape;
        }

        rentChipLabel.color = urgent || days <= 2
            ? new Color(0.16f, 0.08f, 0.02f)
            : Color.white;
    }

    private void UpdateTimerBar(DayManager dm)
    {
        if (timerFillBar == null) return;
        if (dm == null || dm.TurnDurationSeconds <= 0f) return;

        float pct = Mathf.Clamp01(dm.RemainingSeconds / dm.TurnDurationSeconds);

        var rt = timerFillBar.rectTransform;
        // 角丸が潰れないよう、空でも少しだけ幅を残す
        rt.anchorMax = new Vector2(Mathf.Max(pct, 0.05f), 1f);

        // 残り20%を切ったら赤へ寄せて、時間切れの近さを色でも伝える
        timerFillBar.color = pct < 0.2f
            ? Color.Lerp(UITheme.Red, UITheme.Sun, pct / 0.2f)
            : UITheme.Sun;
    }
}
