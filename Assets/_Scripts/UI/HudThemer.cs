using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GameRoomのHUD。シーンに**実体として置く**（メニュー `Roomies/UI/Build HUD`）。
/// 既存のテキスト（DayText / Timer Text / SharedMoneyText）は、組み立てのときに
/// 作った面の中へ移される（そのままシーンに残るので、あとから位置も文字も直せる）。
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
    private const float Margin      = 24f;
    private const float StatusWidth = 352f;
    private const float StatusHeight = 110f;
    private const float MoneyWidth  = 336f;
    private const float MoneyHeight = 150f;
    private const float BarHeight   = 9f;

    [Header("シーンに置いた中身（配置ツールが入れる）")]
    [SerializeField] private Image timerFillBar;
    [SerializeField] private TMP_Text moneyValueLabel;
    [SerializeField] private TMP_Text moneyRequiredLabel;
    [SerializeField] private Image rentChip;
    [SerializeField] private TMP_Text rentChipLabel;
    [SerializeField] private RectTransform moneyCardRect;
    [SerializeField] private Image rentProgress;
    [SerializeField] private TMP_Text objectiveLabel;

    private float rentPulse;

    private void Awake()
    {
        if (moneyValueLabel == null)
        {
            Debug.LogError(
                "[UI] HUDの中身が空です。メニュー『Roomies/UI/Build HUD』でシーンに置き直してください。", this);
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// シーンへ実体を置くための組み立て（エディタ専用）。
    /// 配置ツール `Roomies/UI/Build HUD` から呼ぶ。実行中は動かない。
    /// </summary>
    public void EditorBuildUI()
    {
        var canvas = UITheme.CreateCanvas(transform, "HudCanvas", 100);

        BuildStatusCard(canvas.transform);
        BuildMoneyCard(canvas.transform);
        BuildObjective(canvas.transform);
        EditorApplyRestyle();
    }
#endif

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

    private void BuildStatusCard(Transform parent, TMP_Text previewDay = null, TMP_Text previewTimer = null)
    {
        Image card = UITheme.Surface(parent, "StatusCard", 20f, 2f);
        var rt = card.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(Margin, -Margin);
        rt.sizeDelta = new Vector2(StatusWidth, StatusHeight);

        // 既存のDayTextを移設して整える
        var dayGo = previewDay != null ? previewDay.gameObject : UITheme.FindDeep("DayText");
        if (dayGo != null && dayGo.TryGetComponent(out TMP_Text dayText))
        {
            MoveInto(dayText.rectTransform, card.transform,
                new Vector2(20f, -13f), new Vector2(158f, 44f));
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
        var timerGo = previewTimer != null ? previewTimer.gameObject : UITheme.FindDeep("Timer Text");
        if (timerGo != null && timerGo.TryGetComponent(out TMP_Text timerText))
        {
            timerText.transform.SetParent(card.transform, false);
            var timerRt = timerText.rectTransform;
            timerRt.anchorMin = timerRt.anchorMax = timerRt.pivot = new Vector2(1f, 1f);
            timerRt.anchoredPosition = new Vector2(-20f, -11f);
            timerRt.sizeDelta = new Vector2(144f, 50f);
            timerRt.localScale = Vector3.one;
            timerText.fontSize  = 40f;
            timerText.fontStyle = FontStyles.Bold;
            timerText.color     = UITheme.Sun;
            timerText.alignment = TextAlignmentOptions.Right;
            timerText.enableAutoSizing = true;
            timerText.fontSizeMin = 27f;
            timerText.fontSizeMax = 40f;
            timerText.textWrappingMode = TextWrappingModes.NoWrap;
            timerText.characterSpacing = 1f;
        }

        // 残り時間バー（細い罫線ではなく、掴めそうな太さにする）
        Image barBg = UITheme.Chip(card.transform, "TimerBar",
            new Color(0f, 0f, 0f, 0.42f), BarHeight * 0.5f);
        var barBgRt = barBg.rectTransform;
        barBgRt.anchorMin = new Vector2(0f, 0f);
        barBgRt.anchorMax = new Vector2(1f, 0f);
        barBgRt.pivot     = new Vector2(0.5f, 0f);
        barBgRt.anchoredPosition = new Vector2(0f, 18f);
        barBgRt.sizeDelta = new Vector2(-40f, BarHeight);

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

    private void BuildMoneyCard(Transform parent, bool hideSource = true)
    {
        Image card = UITheme.Surface(parent, "MoneyCard", 20f, 2f);
        var rt = card.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-Margin, -Margin);
        rt.sizeDelta = new Vector2(MoneyWidth, MoneyHeight);
        moneyCardRect = rt;

        var caption = UITheme.Label(card.transform, "Caption", "共同口座",
            20f, new Color(1f, 1f, 1f, 0.68f), TextAlignmentOptions.Left, bold: true);
        SetTopLeft(caption.rectTransform, new Vector2(20f, -14f), new Vector2(120f, 26f));

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
        SetTopLeft(moneyValueLabel.rectTransform, new Vector2(20f, -41f), new Vector2(296f, 55f));

        // 必要額は添え物。主役の残高と同じ大きさにはしない
        moneyRequiredLabel = UITheme.Label(card.transform, "MoneyRequired", "",
            17f, new Color(1f, 1f, 1f, 0.62f), TextAlignmentOptions.Right, bold: true);
        moneyRequiredLabel.enableAutoSizing = false;
        moneyRequiredLabel.textWrappingMode = TextWrappingModes.NoWrap;
        var requiredRt = moneyRequiredLabel.rectTransform;
        requiredRt.anchorMin = requiredRt.anchorMax = requiredRt.pivot = new Vector2(1f, 1f);
        requiredRt.anchoredPosition = new Vector2(-20f, -14f);
        requiredRt.sizeDelta = new Vector2(165f, 26f);

        // 家賃の期限はチップにして、残りが少ないと色と鼓動で警告する
        rentChip = UITheme.Chip(card.transform, "RentChip", UITheme.Grape, 20f);
        var chipRt = rentChip.rectTransform;
        chipRt.anchorMin = chipRt.anchorMax = chipRt.pivot = new Vector2(0f, 0f);
        chipRt.anchoredPosition = new Vector2(20f, 14f);
        chipRt.sizeDelta = new Vector2(MoneyWidth - 40f, 32f);

        rentChipLabel = UITheme.Label(rentChip.transform, "Label", "家賃まで あと3日",
            18f, Color.white, TextAlignmentOptions.Center, bold: true);
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
        var progressBg=UITheme.Chip(card.transform,"RentProgressTrack",new Color(1,1,1,.12f),2);
        SetTopLeft(progressBg.rectTransform,new Vector2(20,-96),new Vector2(296,4));
        rentProgress=UITheme.Chip(progressBg.transform,"RentProgress",UITheme.Lime,2);
        rentProgress.rectTransform.anchorMin=Vector2.zero;rentProgress.rectTransform.anchorMax=Vector2.one;
        rentProgress.rectTransform.offsetMin=rentProgress.rectTransform.offsetMax=Vector2.zero;

        var moneyGo = hideSource ? UITheme.FindDeep("SharedMoneyText") : null;
        if (moneyGo != null && moneyGo.TryGetComponent(out TMP_Text moneyText))
        {
            moneyText.enabled = false;
        }
    }

    private void BuildObjective(Transform parent)
    {
        var card=UITheme.Surface(parent,"ObjectiveCard",18,1.5f);
        var rt=card.rectTransform;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,0);
        rt.anchoredPosition=new Vector2(Margin,Margin);rt.sizeDelta=new Vector2(430,106);
        var title=UITheme.Label(card.transform,"Caption","いまの目標",15,new Color(.61f,.79f,.76f),TextAlignmentOptions.Left,true);
        SetTopLeft(title.rectTransform,new Vector2(18,-10),new Vector2(354,22));
        objectiveLabel=UITheme.Label(card.transform,"Objective","配達で家賃を集めよう",22,Color.white,TextAlignmentOptions.Left,true);
        SetTopLeft(objectiveLabel.rectTransform,new Vector2(18,-36),new Vector2(394,58));
        objectiveLabel.enableAutoSizing=true;objectiveLabel.fontSizeMin=15;objectiveLabel.fontSizeMax=22;
    }

#if UNITY_EDITOR
    public Canvas BuildPreview(TMP_Text day, TMP_Text timer)
    {
        var canvas=UITheme.CreateCanvas(transform,"HudCanvas",100);
        BuildStatusCard(canvas.transform,day,timer);BuildMoneyCard(canvas.transform,false);BuildObjective(canvas.transform);
        moneyValueLabel.text="¥1,250";moneyRequiredLabel.text="家賃 ¥1,500";rentChipLabel.text="家賃まで あと2日";
        rentProgress.rectTransform.anchorMax=new Vector2(.83f,1);timerFillBar.rectTransform.anchorMax=new Vector2(.63f,1);
        return canvas;
    }
#endif

    private void OnSharedMoneyChanged(int oldValue, int newValue)
    {
        int delta = newValue - oldValue;
        ShowMoneyDelta(delta);

        if (delta > 0) GameAudio.Play("money/gain");
        else if (delta < 0) GameAudio.Play("money/spend");
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
        rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(168f, 52f);

        // 厚みのあるピルで「増えた／減った」を物として見せる
        Vector2 basePosition = new Vector2(-4f, -MoneyHeight - 38f);
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
        if(objectiveLabel!=null && dm!=null)
        {
            var player=Unity.Netcode.NetworkManager.Singleton?.LocalClient?.PlayerObject;
            var job=player!=null?player.GetComponent<SmugglingPlayer>():null;
            var mining = player != null ? player.GetComponent<MiningPlayer>() : null;
            objectiveLabel.text=job!=null&&job.IsJailed
                ?(job.CanDoJailLabor?$"作業台で労働 {job.JailLaborProgress}/{SmugglingConfig.JailLaborCount}":"朝まで牢屋で待とう")
                :(mining != null && mining.HudLabel != null ? mining.HudLabel : DefaultObjective(dm));
        }
    }

    /// <summary>
    /// ふだんの目標の文。「今日のおねがい」が出ていればそれを優先して出す
    /// （達成したあとは、ふだんの文に戻る）。
    /// </summary>
    private static string DefaultObjective(DayManager dm)
    {
        var request = DailyRequestManager.Instance;
        if (request != null && request.Kind != DailyRequestManager.RequestKind.None && !request.IsDone)
            return request.StatusText;

        return dm.IsNight ? "家具を買って、夜の街を楽しもう" : "荷物をトラックに積もう（朝の終わりに発車）";
    }

    private void UpdateMoney(DayManager dm)
    {
        if (moneyValueLabel == null || SharedMoneyManager.Instance == null) return;

        int current  = SharedMoneyManager.Instance.CurrentMoney;
        int required = dm != null ? dm.CurrentRentTotal : 0;
        bool enough  = current >= required;

        moneyValueLabel.text  = $"¥{current:N0}";
        moneyValueLabel.color = enough ? UITheme.Lime : UITheme.Red;
        if(rentProgress!=null)rentProgress.rectTransform.anchorMax=new Vector2(required>0?Mathf.Clamp01((float)current/required):1,1);

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

        // ふだんは帯を出さず、白い文字に残り日数だけ赤で大きく見せる。
        // 残り1日を切ったら帯を赤くして鼓動させる（文字は白一色）
        bool urgent = days <= 1;
        if (urgent)
        {
            rentPulse += Time.unscaledDeltaTime * 3.4f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(rentPulse);
            rentChip.color = Color.Lerp(UITheme.Red, UITheme.Shade(UITheme.Red, 0.35f), pulse);
            rentChipLabel.text = days <= 0
                ? "今夜が家賃の支払い日"
                : $"家賃支払いまであと {days}日";
        }
        else
        {
            rentPulse = 0f;
            rentChip.color = Color.clear;
            rentChipLabel.text = $"家賃支払いまであと <size=135%><color=#{RentDaysHex}>{days}日</color></size>";
        }

        rentChipLabel.color = Color.white;
    }

    // 残り日数の色（UITheme.Red を少し明るくした赤。黒い面の上で読める濃さ）
    private const string RentDaysHex = "FF5A4F";

    // HUDの面は後ろの部屋が少し透ける濃さにする（メニューの面より薄い）
    private static readonly Color HudInk =
        new Color(UITheme.MenuInk.r, UITheme.MenuInk.g, UITheme.MenuInk.b, 0.82f);

#if UNITY_EDITOR
    /// <summary>
    /// 置いてあるHUDの見た目（位置・色・フォント）を整える（エディタ専用）。
    /// 配置ツール `Roomies/UI/Restyle Game UI` と `EditorBuildUI` から呼ぶ。何度呼んでも同じ結果。
    /// 参照・部品の名前・親子は変えない（「いまの目標」の紙に付ける Tape だけ足す）。
    /// </summary>
    public void EditorApplyRestyle()
    {
        var canvas = transform.Find("HudCanvas");
        if (canvas == null) return;
        var font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/MPLUSU-Black SDF.asset");

        void Font(TMP_Text text)
        {
            if (text == null) return;
            UnityEditor.Undo.RecordObject(text, "Restyle HUD");
            if (font != null) text.font = font;
            text.fontStyle = FontStyles.Normal;   // Black は元から太い
            UnityEditor.EditorUtility.SetDirty(text);
        }
        void Rect(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            UnityEditor.Undo.RecordObject(rt, "Restyle HUD");
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = position; rt.sizeDelta = size;
        }
        void Paint(Transform card, Color edge, Color fill)
        {
            if (card == null) return;
            var border = card.GetComponent<Image>();
            var inner = card.Find("Fill") != null ? card.Find("Fill").GetComponent<Image>() : null;
            if (border != null) { UnityEditor.Undo.RecordObject(border, "Restyle HUD"); border.color = edge; }
            if (inner != null) { UnityEditor.Undo.RecordObject(inner, "Restyle HUD"); inner.color = fill; }
        }

        // ---- 左上：日数と残り時間
        var status = canvas.Find("StatusCard");
        if (status != null)
        {
            Paint(status, UITheme.MenuEdge, HudInk);
            foreach (var text in status.GetComponentsInChildren<TMP_Text>(true)) Font(text);
        }

        // ---- 右上：共同口座
        var money = canvas.Find("MoneyCard") as RectTransform;
        if (money != null)
        {
            Paint(money, UITheme.MenuEdge, HudInk);
            Rect(money, new Vector2(1f, 1f), new Vector2(-Margin, -Margin), new Vector2(MoneyWidth + 44f, 232f));
            foreach (var text in money.GetComponentsInChildren<TMP_Text>(true)) Font(text);

            var caption = money.Find("Caption") as RectTransform;
            if (caption != null)
            {
                Rect(caption, new Vector2(0f, 1f), new Vector2(26f, -18f), new Vector2(200f, 32f));
                var t = caption.GetComponent<TMP_Text>(); t.fontSize = 24f; t.color = new Color(1f, 1f, 1f, 0.92f);
            }
            if (moneyValueLabel != null)
            {
                Rect(moneyValueLabel.rectTransform, new Vector2(0f, 1f), new Vector2(26f, -50f), new Vector2(328f, 70f));
                moneyValueLabel.fontSizeMax = 58f; moneyValueLabel.fontSizeMin = 30f; moneyValueLabel.fontSize = 58f;
            }
            var track = money.Find("RentProgressTrack") as RectTransform;
            if (track != null) Rect(track, new Vector2(0f, 1f), new Vector2(26f, -124f), new Vector2(328f, 6f));
            if (moneyRequiredLabel != null)
            {
                Rect(moneyRequiredLabel.rectTransform, new Vector2(0f, 1f), new Vector2(26f, -138f), new Vector2(328f, 32f));
                moneyRequiredLabel.fontSize = 23f; moneyRequiredLabel.alignment = TextAlignmentOptions.Left;
            }
            if (rentChip != null)
            {
                Rect(rentChip.rectTransform, new Vector2(0f, 0f), new Vector2(18f, 14f), new Vector2(344f, 44f));
                UnityEditor.Undo.RecordObject(rentChip, "Restyle HUD");
                rentChip.color = Color.clear;
            }
            if (rentChipLabel != null)
            {
                rentChipLabel.alignment = TextAlignmentOptions.Left;
                rentChipLabel.fontSizeMin = 16f; rentChipLabel.fontSizeMax = 23f; rentChipLabel.color = Color.white;
                rentChipLabel.rectTransform.offsetMin = new Vector2(8f, 0f);
                rentChipLabel.rectTransform.offsetMax = new Vector2(-8f, 0f);
                rentChipLabel.text = $"家賃支払いまであと <size=135%><color=#{RentDaysHex}>3日</color></size>";
            }
        }

        // ---- 左下：いまの目標（貼り紙）
        var objective = canvas.Find("ObjectiveCard") as RectTransform;
        if (objective != null)
        {
            Paint(objective, new Color(0.80f, 0.75f, 0.62f, 1f), new Color(0.95f, 0.92f, 0.84f, 1f));
            Rect(objective, new Vector2(0f, 0f), new Vector2(Margin + 6f, Margin + 10f), new Vector2(430f, 112f));
            objective.localEulerAngles = new Vector3(0f, 0f, -2f);
            foreach (var text in objective.GetComponentsInChildren<TMP_Text>(true)) Font(text);
            var caption = objective.Find("Caption") != null ? objective.Find("Caption").GetComponent<TMP_Text>() : null;
            if (caption != null) { caption.color = new Color(0.23f, 0.37f, 0.71f); caption.fontSize = 17f; }
            if (objectiveLabel != null) { objectiveLabel.color = new Color(0.16f, 0.17f, 0.2f); objectiveLabel.fontSizeMax = 24f; }

            var tape = objective.Find("Tape") as RectTransform;
            if (tape == null)
            {
                var image = UITheme.Chip(objective, "Tape", new Color(0.86f, 0.80f, 0.60f, 0.92f), 3f);
                UnityEditor.Undo.RegisterCreatedObjectUndo(image.gameObject, "Restyle HUD");
                tape = image.rectTransform;
            }
            Rect(tape, new Vector2(0.5f, 1f), new Vector2(0f, 12f), new Vector2(96f, 26f));
            tape.localEulerAngles = new Vector3(0f, 0f, 4f);
        }
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

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
