using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 部屋の PC の画面（デスクトップ）。GameRoomUI に置く（Roomies/PC/3 Build Desktop UI。実行時には作らない）。2026/11/25。
///
/// ・部屋の PC（<see cref="RoomComputer"/>）を見て E → この画面が全体に出る。Esc か「とじる」で部屋に戻る。
/// ・開いている間は、その人の移動・視点・操作・Esc メニュー・家具ショップのキーが止まる（<see cref="IsOpen"/>。ほかの人には関係ない）。
/// ・アプリ：家具ショップ（前からの家具の注文の仕組み DayManager.BuyFurnitureServerRpc をそのまま使う）、株トレード（<see cref="StockMarket"/>）、
///   家具の効果（置いてある家具と、いま効いている効果の一覧。前の O キーの一覧から移した）。
/// ・開くと、ROOMIES OS の起動の画面が少し出てから、ホーム画（時計・口座・アイコン）になる。
/// ・ボタンの反応は Awake で、インスペクターの参照からつなぐ（名前で探さない）。
/// </summary>
public class RoomComputerUI : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    public static bool ConsumedEscapeThisFrame => escapeFrame == Time.frameCount;
    private static int escapeFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { IsOpen = false; escapeFrame = -1; }

    [Serializable]
    public class FurnitureCard
    {
        public GameObject root;
        public Image swatch;
        public TMP_Text initial;
        [Tooltip("家具の絵を出す所（絵が無ければ隠して、色の板と頭の字）")]
        public Image photo;
        public TMP_Text title;
        public TMP_Text price;
        public TMP_Text effect;
        public Button buy;
        public TMP_Text buyLabel;
    }

    [Serializable]
    public class EffectRow
    {
        public GameObject root;
        [Tooltip("家具の小さな絵（無ければ隠す）")]
        public Image photo;
        public TMP_Text title;
        public TMP_Text effect;
        public TMP_Text state;
    }

    [Serializable]
    public class StockRow
    {
        public Button button;
        public Image background;
        public TMP_Text code;
        public TMP_Text title;
        public TMP_Text price;
        public TMP_Text change;
        public TMP_Text holding;
    }

    [Header("画面全体")]
    [SerializeField] private CanvasGroup group;
    [Tooltip("開くときに大きくなる所（モニターの枠）")]
    [SerializeField] private RectTransform monitor;
    [SerializeField, Min(0.01f)] private float openSeconds = 0.18f;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text clockText;
    [SerializeField] private TMP_Text moneyText;

    [Header("起動の画面（開いたとき、少しだけ出る）")]
    [SerializeField] private CanvasGroup boot;
    [Tooltip("起動の画面の、進み具合の棒（横に伸びる）")]
    [SerializeField] private RectTransform bootBar;
    [SerializeField, Min(0f)] private float bootSeconds = 0.7f;

    [Header("壁紙（「壁紙」のボタンで順に変わる。選んだ壁紙は、その人の PC にだけ覚える）")]
    [SerializeField] private Image wallpaper;
    [SerializeField] private Sprite[] wallpapers = new Sprite[0];
    [SerializeField] private Button wallpaperButton;
    private const string WallpaperPref = "Roomies.PcWallpaper";

    [Header("ホーム画のウィジェット")]
    [SerializeField] private TMP_Text homeClock;
    [SerializeField] private TMP_Text homeDay;
    [SerializeField] private TMP_Text homeMoney;
    [SerializeField] private TMP_Text homeRent;
    [SerializeField] private TMP_Text homeHint;
    [Tooltip("アプリの窓が開くときの動きの秒数")]
    [SerializeField, Min(0.01f)] private float windowSeconds = 0.14f;

    [Header("アプリ（0 = 家具ショップ、1 = 株トレード、2 = 家具の効果）")]
    [SerializeField] private Button[] appIcons = new Button[0];
    [SerializeField] private GameObject[] appWindows = new GameObject[0];
    [SerializeField] private Button[] windowCloseButtons = new Button[0];
    [Tooltip("タスクバーの、開いているアプリの印")]
    [SerializeField] private GameObject[] taskbarMarks = new GameObject[0];

    [Header("家具ショップ")]
    [SerializeField] private FurnitureCard[] furnitureCards = new FurnitureCard[0];
    [SerializeField] private TMP_Text furnitureStatus;
    [Tooltip("家具の絵（家具の id → 絵）。Roomies/PC/3 Build Desktop UI が Assets/_Art/UI/Pc/Furniture/ から入れる")]
    [SerializeField] private FurniturePhoto[] furniturePhotos = new FurniturePhoto[0];
    [SerializeField] private Color photoBackColor = new Color(1f, 0.96f, 0.88f);

    [Serializable]
    public class FurniturePhoto
    {
        public string id;
        public Sprite sprite;
    }

    /// <summary>その家具の絵（無ければ null）。</summary>
    public Sprite PhotoFor(string id)
    {
        foreach (var p in furniturePhotos) if (p != null && p.id == id) return p.sprite;
        return null;
    }

    public int FurniturePhotosShown { get; private set; }

    [Header("家具の効果")]
    [SerializeField] private TMP_Text effectSpeed;
    [SerializeField] private TMP_Text effectIncome;
    [SerializeField] private TMP_Text effectCount;
    [SerializeField] private EffectRow[] effectRows = new EffectRow[0];
    [SerializeField] private TMP_Text effectNote;

    [Header("株トレード")]
    [SerializeField] private StockRow[] stockRows = new StockRow[0];
    [SerializeField] private TMP_Text detailTitle;
    [SerializeField] private TMP_Text detailPrice;
    [SerializeField] private TMP_Text detailChange;
    [SerializeField] private TMP_Text detailHolding;
    [SerializeField] private TMP_Text detailProfit;
    [SerializeField] private TMP_Text newsText;
    [SerializeField] private TMP_Text totalText;
    [SerializeField] private TMP_Text stockStatus;
    [SerializeField] private TMP_Text feeNote;
    [SerializeField] private UILineChart chart;
    [SerializeField] private Button buyOne, buyFive, sellOne, sellFive, sellAll;

    [Header("色")]
    [SerializeField] private Color upColor = new Color(0.86f, 0.25f, 0.36f);
    [SerializeField] private Color downColor = new Color(0.17f, 0.52f, 0.86f);
    [SerializeField] private Color flatColor = new Color(0.45f, 0.42f, 0.5f);
    [Tooltip("グラフの線で、値段が変わっていないときの色（暗い地の上でも見える色）")]
    [SerializeField] private Color chartFlatColor = new Color(0.8f, 0.76f, 0.9f);
    [SerializeField] private Color rowColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] private Color rowSelectedColor = new Color(1f, 0.86f, 0.6f, 1f);
    [SerializeField] private Color newsColor = new Color(1f, 0.70f, 0.36f);
    [SerializeField] private Color newsQuietColor = new Color(1f, 0.97f, 0.92f, 0.38f);

    private Canvas canvas;
    private int openApp = -1;
    private int selectedStock;
    private bool furniturePending, tradePending;
    private float refreshTimer, animTime, bootTime, windowTime;
    private bool closing;
    private StockMarket market;
    private bool stocksDirty = true;

    public int OpenApp => openApp;
    public int SelectedStock => selectedStock;
    public string FurnitureStatusLine => furnitureStatus != null ? furnitureStatus.text : "";
    public string StockStatusLine => stockStatus != null ? stockStatus.text : "";
    public string DetailPriceLine => detailPrice != null ? detailPrice.text : "";
    public string DetailChangeLine => detailChange != null ? detailChange.text : "";
    public string NewsLine => newsText != null ? newsText.text : "";

    // ================================================================ 起動
    private void Awake()
    {
        for (int i = 0; i < appIcons.Length; i++) { int k = i; if (appIcons[i] != null) appIcons[i].onClick.AddListener(() => ShowApp(k)); }
        foreach (var b in windowCloseButtons) if (b != null) b.onClick.AddListener(HideApp);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        for (int i = 0; i < furnitureCards.Length; i++) { int k = i; if (furnitureCards[i].buy != null) furnitureCards[i].buy.onClick.AddListener(() => BuyFurniture(k)); }
        for (int i = 0; i < stockRows.Length; i++) { int k = i; if (stockRows[i].button != null) stockRows[i].button.onClick.AddListener(() => SelectStock(k)); }
        if (buyOne != null) buyOne.onClick.AddListener(() => Trade(true, 1));
        if (buyFive != null) buyFive.onClick.AddListener(() => Trade(true, 5));
        if (sellOne != null) sellOne.onClick.AddListener(() => Trade(false, 1));
        if (sellFive != null) sellFive.onClick.AddListener(() => Trade(false, 5));
        if (sellAll != null) sellAll.onClick.AddListener(() => Trade(false, 0));
        if (wallpaperButton != null) wallpaperButton.onClick.AddListener(NextWallpaper);
        ApplyWallpaper(LoadWallpaperIndex());
        SetVisible(false);
        IsOpen = false;
    }

    private void OnEnable()
    {
        DayManager.FurniturePurchaseResult += OnFurnitureResult;
        StockMarket.TradeResult += OnTradeResult;
    }

    private void OnDisable()
    {
        DayManager.FurniturePurchaseResult -= OnFurnitureResult;
        StockMarket.TradeResult -= OnTradeResult;
        Watch(null);
        if (IsOpen) { IsOpen = false; SetVisible(false); }
    }

    // ================================================================ 開く・閉じる
    public void Open(RoomComputer pc)
    {
        if (IsOpen) return;
        IsOpen = true;
        closing = false;
        animTime = 0f;
        bootTime = 0f;
        HideApp();
        SetVisible(true);
        if (boot != null) { boot.alpha = 1f; boot.blocksRaycasts = true; boot.gameObject.SetActive(bootSeconds > 0f); }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        RefreshAll();
        GameAudio.Play("pc/open", 0.8f);
    }

    public void Close()
    {
        if (!IsOpen || closing) return;
        closing = true;
        animTime = 0f;
        GameAudio.Play("pc/close", 0.8f);
    }

    private void FinishClose()
    {
        closing = false;
        IsOpen = false;
        SetVisible(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private void SetVisible(bool on)
    {
        // 閉じている間は Canvas ごと止める（透明でも描く回数が増えないように）
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas != null) canvas.enabled = on;
        if (group == null) return;
        group.alpha = on ? 1f : 0f;
        group.interactable = on;
        group.blocksRaycasts = on;
        if (monitor != null) monitor.localScale = Vector3.one;
    }

    private void Update()
    {
        if (!IsOpen) return;
        // 開く・閉じる動き（モニターの枠から画面いっぱいに広がる）
        animTime += Time.unscaledDeltaTime;
        float u = Mathf.Clamp01(animTime / openSeconds);
        float e = 1f - (1f - u) * (1f - u);
        if (closing)
        {
            group.alpha = 1f - e;
            if (monitor != null) monitor.localScale = Vector3.one * Mathf.Lerp(1f, 0.94f, e);
            if (u >= 1f) { FinishClose(); return; }
        }
        else
        {
            group.alpha = e;
            if (monitor != null) monitor.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, e);
        }

        // 起動の画面：棒が伸びてから、ふわっと消えてホーム画へ
        if (boot != null && boot.gameObject.activeSelf)
        {
            bootTime += Time.unscaledDeltaTime;
            float b = Mathf.Clamp01(bootTime / Mathf.Max(0.01f, bootSeconds));
            if (bootBar != null) bootBar.localScale = new Vector3(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(b / 0.75f)), 1f, 1f);
            boot.alpha = b < 0.75f ? 1f : 1f - (b - 0.75f) / 0.25f;
            if (b >= 1f) { boot.gameObject.SetActive(false); boot.blocksRaycasts = false; }
        }
        // アプリの窓：少し小さい所から、ぽんと開く
        if (openApp >= 0 && openApp < appWindows.Length && appWindows[openApp] != null && windowTime < windowSeconds)
        {
            windowTime += Time.unscaledDeltaTime;
            float w = Mathf.Clamp01(windowTime / windowSeconds);
            float pop = w < 1f ? Mathf.Lerp(0.92f, 1f, 1f - (1f - w) * (1f - w)) : 1f;
            appWindows[openApp].transform.localScale = Vector3.one * pop;
        }

        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            escapeFrame = Time.frameCount;
            Close();
            return;
        }

        // 町の様子が変わる（夜になる・お金が動く）ので、ときどき表示をそろえ直す
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f || stocksDirty)
        {
            refreshTimer = 0.25f;
            RefreshAll();
        }
        // 日の切り替え・ゲームオーバーのときは部屋に戻す
        var dm = DayManager.Instance;
        if (dm != null && (dm.IsGameOver || dm.IsTransitioning)) Close();
    }

    // ================================================================ 壁紙
    public int WallpaperIndex { get; private set; }

    private static int LoadWallpaperIndex()
    {
        try { return PlayerPrefs.GetInt(WallpaperPref, 0); } catch { return 0; }
    }

    private void ApplyWallpaper(int index)
    {
        if (wallpapers.Length == 0) return;
        WallpaperIndex = ((index % wallpapers.Length) + wallpapers.Length) % wallpapers.Length;
        if (wallpaper != null && wallpapers[WallpaperIndex] != null) wallpaper.sprite = wallpapers[WallpaperIndex];
    }

    /// <summary>壁紙を次へ（ボタン・確認の道具から）。</summary>
    public void NextWallpaper()
    {
        ApplyWallpaper(WallpaperIndex + 1);
        try { PlayerPrefs.SetInt(WallpaperPref, WallpaperIndex); } catch { }
        GameAudio.Play("pc/click", 0.5f);
    }

    // ================================================================ アプリ
    public void ShowApp(int index)
    {
        openApp = index;
        windowTime = 0f;
        for (int i = 0; i < appWindows.Length; i++) if (appWindows[i] != null) { appWindows[i].SetActive(i == index); appWindows[i].transform.localScale = Vector3.one * 0.92f; }
        for (int i = 0; i < taskbarMarks.Length; i++) if (taskbarMarks[i] != null) taskbarMarks[i].SetActive(i == index);
        GameAudio.Play("pc/click", 0.5f);
        RefreshAll();
    }

    public void HideApp()
    {
        openApp = -1;
        foreach (var w in appWindows) if (w != null) w.SetActive(false);
        foreach (var m in taskbarMarks) if (m != null) m.SetActive(false);
    }

    private void RefreshAll()
    {
        var dm = DayManager.Instance;
        var money = SharedMoneyManager.Instance;
        if (clockText != null && dm != null)
        {
            int sec = Mathf.Max(0, Mathf.CeilToInt(dm.RemainingSeconds));
            clockText.text = $"DAY {dm.CurrentDay} {(dm.IsNight ? "夜" : "朝")}  {sec / 60:00}:{sec % 60:00}";
        }
        if (moneyText != null && money != null) moneyText.text = $"共同口座  ¥{money.CurrentMoney:N0}";
        // ホーム画のウィジェット（時計・日・口座・家賃）
        if (dm != null)
        {
            int sec = Mathf.Max(0, Mathf.CeilToInt(dm.RemainingSeconds));
            if (homeClock != null) homeClock.text = $"{sec / 60:00}:{sec % 60:00}";
            if (homeDay != null) homeDay.text = $"DAY {dm.CurrentDay}  {(dm.IsNight ? "夜" : "朝")}　のこり";
            if (homeRent != null) homeRent.text = $"家賃 ¥{dm.CurrentRentTotal:N0}　あと {dm.DaysUntilRent} 日";
            if (homeHint != null) homeHint.text = dm.IsNight ? "夜：家具を注文できます" : "朝：家具の注文は夜だけ";
        }
        if (homeMoney != null && money != null) homeMoney.text = $"¥{money.CurrentMoney:N0}";
        if (openApp == 0) RefreshFurniture();
        if (openApp == 1) RefreshStocks();
        if (openApp == 2) RefreshEffects();
        stocksDirty = false;
    }

    // ---------------------------------------------------------------- 家具ショップ
    private void RefreshFurniture()
    {
        var dm = DayManager.Instance;
        var money = SharedMoneyManager.Instance;
        bool night = dm != null && dm.CanBuyFurniture;
        int photos = 0;
        for (int i = 0; i < furnitureCards.Length; i++)
        {
            var card = furnitureCards[i];
            FurnitureItem item = FurnitureCatalog.Get(i);
            if (card.root != null) card.root.SetActive(item != null);
            if (item == null) continue;
            if (card.title != null) card.title.text = item.displayName;
            if (card.price != null) card.price.text = $"¥{item.cost:N0}";
            if (card.effect != null) card.effect.text = EffectText(item);
            if (card.initial != null) card.initial.text = item.displayName.Length > 0 ? item.displayName.Substring(0, 1) : "?";
            Sprite pic = PhotoFor(item.id);
            if (card.photo != null) { card.photo.sprite = pic; card.photo.gameObject.SetActive(pic != null); }
            if (card.initial != null) card.initial.gameObject.SetActive(pic == null);
            if (card.swatch != null) card.swatch.color = pic != null ? photoBackColor : item.placeholderColor;
            if (pic != null) photos++;
            bool canPay = money != null && money.CanPay(item.cost);
            if (card.buy != null) card.buy.interactable = night && canPay && !furniturePending;
            if (card.buyLabel != null) card.buyLabel.text = !night ? "夜だけ" : !canPay ? "お金が足りない" : "注文する";
        }
        FurniturePhotosShown = photos;
        if (!furniturePending && furnitureStatus != null && string.IsNullOrEmpty(furnitureStatus.text))
            furnitureStatus.text = night ? "注文すると、家具は玄関の配達の場所に届きます（効果は翌朝から）。" : "家具の注文は、夜の自由行動の間だけできます。";
    }

    public static string EffectText(FurnitureItem item)
    {
        switch (item.effect)
        {
            case FurnitureEffect.MoveSpeed: return $"移動が {item.effectValue:0}% 速くなる";
            case FurnitureEffect.PassiveIncome: return $"毎朝 ¥{item.effectValue:0} の収入";
            case FurnitureEffect.Bounce: return "上で跳ねられる";
            default: return "部屋がにぎやかになる";
        }
    }

    /// <summary>家具を注文する（ボタン・確認の道具から）。お金と配達はサーバーが前と同じ仕組みで決める。</summary>
    public void BuyFurniture(int index)
    {
        var dm = DayManager.Instance;
        FurnitureItem item = FurnitureCatalog.Get(index);
        if (dm == null || item == null || furniturePending) return;
        if (!dm.CanBuyFurniture) { SetFurnitureStatus("家具の注文は、夜の自由行動の間だけできます。"); return; }
        furniturePending = true;
        SetFurnitureStatus($"{item.displayName}を注文中…");
        dm.BuyFurnitureServerRpc(index);
        GameAudio.Play("pc/click", 0.6f);
        RefreshFurniture();
    }

    private void OnFurnitureResult(bool success, int index, string message)
    {
        furniturePending = false;
        FurnitureItem item = FurnitureCatalog.Get(index);
        SetFurnitureStatus(success
            ? $"{(item != null ? item.displayName : "家具")}が届きました。玄関の配達の場所で、F で持って置いてね。"
            : "注文できませんでした：" + message);
        if (success) GameAudio.Play("store/register", 0.8f);
        if (IsOpen) RefreshFurniture();
    }

    private void SetFurnitureStatus(string line) { if (furnitureStatus != null) furnitureStatus.text = line; }

    // ---------------------------------------------------------------- 家具の効果
    public int EffectRowsShown { get; private set; }

    private void RefreshEffects()
    {
        var fem = FurnitureEffectManager.InstanceOrNull;
        int placed = 0;
        if (effectSpeed != null) effectSpeed.text = fem != null ? $"×{fem.MoveSpeedMultiplier:0.00}" : "×1.00";
        if (effectIncome != null) effectIncome.text = fem != null ? $"¥{fem.PassiveIncomeTotal:N0}" : "¥0";
        int shown = 0;
        if (fem != null)
            foreach (var f in fem.Registered)
            {
                if (f == null) continue;
                placed++;
                if (shown >= effectRows.Length) continue;
                var row = effectRows[shown++];
                if (row.root != null) row.root.SetActive(true);
                if (row.title != null) row.title.text = string.IsNullOrEmpty(f.displayName) ? f.itemId : f.displayName;
                if (row.photo != null) { Sprite pic = PhotoFor(f.itemId); row.photo.sprite = pic; row.photo.gameObject.SetActive(pic != null); }
                if (row.effect != null) row.effect.text = EffectLine(f.effect, f.effectValue);
                if (row.state != null)
                {
                    row.state.text = !f.IsOnRoom ? "部屋の外（効かない）" : f.EffectActive ? "効いている" : "翌朝から効く";
                    row.state.color = !f.IsOnRoom ? downColor : f.EffectActive ? new Color(0.25f, 0.6f, 0.35f) : flatColor;
                }
            }
        for (int i = shown; i < effectRows.Length; i++) if (effectRows[i].root != null) effectRows[i].root.SetActive(false);
        EffectRowsShown = shown;
        if (effectCount != null) effectCount.text = placed == 0 ? "置いてある家具：まだありません" : placed > shown ? $"置いてある家具：{placed} 個（ほか {placed - shown} 個）" : $"置いてある家具：{placed} 個";
        if (effectNote != null) effectNote.text = placed == 0 ? "夜に「家具ショップ」で注文して、部屋の床に置くと、翌朝から効きます。" : "家具は部屋の床に置くと、翌朝から効きます。部屋の外に置くと効きません。";
    }

    private static string EffectLine(FurnitureEffect e, float v)
    {
        switch (e)
        {
            case FurnitureEffect.MoveSpeed: return $"移動 +{v:0}%";
            case FurnitureEffect.PassiveIncome: return $"毎朝 ¥{v:0}";
            case FurnitureEffect.Bounce: return "跳ねられる";
            default: return "効果なし（見た目）";
        }
    }

    // ---------------------------------------------------------------- 株トレード
    private void Watch(StockMarket m)
    {
        if (market == m) return;
        if (market != null) market.Changed -= OnMarketChanged;
        market = m;
        if (market != null) market.Changed += OnMarketChanged;
    }

    private void OnMarketChanged() => stocksDirty = true;

    public void SelectStock(int index)
    {
        selectedStock = index;
        GameAudio.Play("pc/click", 0.4f);
        RefreshStocks();
    }

    private Color ChangeColor(float percent) => percent > 0.05f ? upColor : percent < -0.05f ? downColor : flatColor;

    private void RefreshStocks()
    {
        Watch(StockMarket.Instance);
        if (market == null || !market.IsSpawned || market.Count == 0)
        {
            if (stockStatus != null) stockStatus.text = "市場につながっていません";
            return;
        }
        selectedStock = Mathf.Clamp(selectedStock, 0, market.Count - 1);
        int totalValue = 0, totalProfit = 0;
        for (int i = 0; i < stockRows.Length; i++)
        {
            var row = stockRows[i];
            bool has = i < market.Count;
            if (row.button != null) row.button.gameObject.SetActive(has);
            if (!has) continue;
            var def = market.Definition(i);
            float ch = market.ChangePercent(i);
            if (row.code != null) row.code.text = def.code;
            if (row.title != null) row.title.text = def.name;
            if (row.price != null) row.price.text = $"¥{market.Price(i):N0}";
            if (row.change != null) { row.change.text = $"{(ch > 0 ? "+" : "")}{ch:0.0}%"; row.change.color = ChangeColor(ch); }
            if (row.holding != null) row.holding.text = market.Holding(i) > 0 ? $"{market.Holding(i)} 株" : "";
            if (row.background != null) row.background.color = i == selectedStock ? rowSelectedColor : rowColor;
            totalValue += market.MarketValue(i);
            totalProfit += market.ProfitLoss(i);
        }

        int s = selectedStock;
        var d = market.Definition(s);
        float c = market.ChangePercent(s);
        int[] hist = market.History(s);
        float fromStart = hist.Length > 0 && hist[0] > 0 ? (market.Price(s) - hist[0]) * 100f / hist[0] : 0f;
        if (detailTitle != null) detailTitle.text = $"{d.name}  <size=70%>{d.code}</size>";
        if (detailPrice != null) detailPrice.text = $"¥{market.Price(s):N0}";
        if (detailChange != null) { detailChange.text = $"{(c > 0 ? "+" : "")}{c:0.0}%（前回から）"; detailChange.color = ChangeColor(c); }
        if (chart != null) chart.SetPoints(hist, Mathf.Abs(fromStart) <= 0.05f ? chartFlatColor : ChangeColor(fromStart));
        int hold = market.Holding(s);
        if (detailHolding != null)
            detailHolding.text = hold > 0
                ? $"持ち株 {hold} 株　評価額 ¥{market.MarketValue(s):N0}　平均 ¥{market.CostBasis(s) / Mathf.Max(1, hold):N0}"
                : "持ち株 0 株";
        if (detailProfit != null)
        {
            int pl = market.ProfitLoss(s);
            detailProfit.text = hold > 0 ? $"損益 {(pl >= 0 ? "+" : "−")}¥{Mathf.Abs(pl):N0}" : "";
            detailProfit.color = pl > 0 ? upColor : pl < 0 ? downColor : flatColor;
        }
        if (newsText != null)
        {
            // ニュースは出た回から数回だけ。過ぎたら消さずに、控えめな「いまはありません」にする（帯の位置が動かないように）
            int age = market.NewsAge;
            bool show = !string.IsNullOrEmpty(market.News) && market.NewsVisible;
            string when = age <= 0 ? "いまの値動き" : $"{age} 回前の値動き";
            newsText.text = show ? $"ニュース（{when}）：{market.News}" : "ニュース：いまはありません";
            newsText.color = show ? newsColor : newsQuietColor;
        }
        if (totalText != null)
            totalText.text = $"持ち株の評価額 ¥{totalValue:N0}　損益 <color=#{ColorUtility.ToHtmlStringRGB(totalProfit > 0 ? upColor : totalProfit < 0 ? downColor : flatColor)}>{(totalProfit >= 0 ? "+" : "−")}¥{Mathf.Abs(totalProfit):N0}</color>";
        var b = GameBalance.Current;
        if (feeNote != null) feeNote.text = $"手数料 {b.stockFeePercent:0.#}%・お金は共同口座・持ち株はみんなの共同・値段は {b.stockTickSeconds:0} 秒ごとに動く";
        var money = SharedMoneyManager.Instance;
        int price = market.Price(s);
        bool canBuy1 = !tradePending && money != null && money.CanPay(price + StockMarket.Fee(price, b.stockFeePercent)) && hold + 1 <= b.stockMaxShares;
        bool canBuy5 = !tradePending && money != null && money.CanPay(price * 5 + StockMarket.Fee(price * 5, b.stockFeePercent)) && hold + 5 <= b.stockMaxShares;
        if (buyOne != null) buyOne.interactable = canBuy1;
        if (buyFive != null) buyFive.interactable = canBuy5;
        if (sellOne != null) sellOne.interactable = !tradePending && hold >= 1;
        if (sellFive != null) sellFive.interactable = !tradePending && hold >= 5;
        if (sellAll != null) sellAll.interactable = !tradePending && hold >= 1;
        if (!tradePending && stockStatus != null && stockStatus.text == "市場につながっていません") stockStatus.text = "";
    }

    /// <summary>売り買い（ボタン・確認の道具から）。売りで quantity が 0 なら全部売る。</summary>
    public void Trade(bool buy, int quantity)
    {
        Watch(StockMarket.Instance);
        if (market == null || tradePending) return;
        int s = selectedStock;
        if (!buy && quantity <= 0) quantity = market.Holding(s);
        if (quantity <= 0) return;
        tradePending = true;
        if (stockStatus != null) stockStatus.text = buy ? "買い注文を出しています…" : "売り注文を出しています…";
        if (buy) market.RequestBuy(s, quantity); else market.RequestSell(s, quantity);
        GameAudio.Play("pc/click", 0.6f);
        RefreshStocks();
    }

    private void OnTradeResult(bool success, string message)
    {
        tradePending = false;
        if (stockStatus != null) stockStatus.text = success ? message : "できませんでした：" + message;
        if (success) GameAudio.Play("store/coins", 0.7f);
        if (IsOpen) RefreshStocks();
    }
}
