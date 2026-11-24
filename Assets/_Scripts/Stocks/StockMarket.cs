using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 株トレード（部屋の PC のアプリ）の市場（2026/11/25）。GameRoom に 1 つ置く（Roomies/PC/2 Place In Open Scene）。
///
/// ・銘柄はゲームの中だけの架空の会社（GameBalance.stocks。並び順が番号）。値段はゲームの中の時間で動く
///   （stockTickSeconds ごとに 1 回。日の切り替え中とゲームオーバー中は止まる）。
/// ・値段・値動きの記録・持ち株はホスト（サーバー）が決めて、全員に同じ物を配る（NetworkList）。
/// ・持ち株は部屋の全員の共同（お金も共同口座から払い、共同口座へ戻る）。
/// ・町の出来事と連動させるときは <see cref="ServerNudge"/> を呼ぶ（次の値動きに足す）。いまはときどき出るニュースだけ。
/// ・セーブ：値段・持ち株・買うのに払った額（RunSaveManager から ServerCapture / ServerRestore）。
/// </summary>
public class StockMarket : NetworkBehaviour
{
    public static StockMarket Instance { get; private set; }

    /// <summary>値動きの記録の長さ（グラフに出す点の数）。</summary>
    public const int HistoryLength = 40;

    [Header("ニュースの文（{0} は会社の名前）")]
    [SerializeField] private string[] goodNews =
    {
        "{0}、新しい商品が大当たり", "{0}、町の人気ランキング 1 位に", "{0}の社長、テレビで大はしゃぎ", "{0}、思わぬ黒字",
    };
    [SerializeField] private string[] badNews =
    {
        "{0}、倉庫が雨もり", "{0}、社長が寝坊して会議に遅刻", "{0}、うわさの新商品が延期", "{0}、ライバルに客を取られる",
    };

    // ---- 全員に届く状態 ----
    private NetworkList<int> prices;
    private NetworkList<int> history;          // 銘柄 s の i 番目 = history[s * HistoryLength + i]（古い順の輪。head が次に書く所）
    private NetworkList<int> holdings;
    private NetworkList<int> costBasis;
    private readonly NetworkVariable<int> head = new(0);
    private readonly NetworkVariable<int> tickCount = new(0);
    private readonly NetworkVariable<FixedString128Bytes> news = new(new FixedString128Bytes());

    // ---- サーバーだけ ----
    private float tickTimer;
    private float[] pendingNudge = new float[0];
    private System.Random random;

    /// <summary>値段・持ち株などが変わった（各画面で、表示を作り直すきっかけに使う）。</summary>
    public event Action Changed;

    /// <summary>売り買いの結果（その人の画面だけ）：成功したか・メッセージ。</summary>
    public static event Action<bool, string> TradeResult;

    public int Count => prices != null ? prices.Count : 0;
    public int Price(int s) => s >= 0 && s < Count ? prices[s] : 0;
    public int Holding(int s) => holdings != null && s >= 0 && s < holdings.Count ? holdings[s] : 0;
    public int CostBasis(int s) => costBasis != null && s >= 0 && s < costBasis.Count ? costBasis[s] : 0;
    public int TickCount => tickCount.Value;
    public string News => news.Value.ToString();
    public StockDefinition Definition(int s)
    {
        var defs = GameBalance.Current.Stocks;
        return s >= 0 && s < defs.Length ? defs[s] : null;
    }

    /// <summary>その銘柄の値動きの記録（古い順）。</summary>
    public int[] History(int s)
    {
        var result = new int[HistoryLength];
        if (history == null || history.Count < (s + 1) * HistoryLength) return result;
        for (int i = 0; i < HistoryLength; i++) result[i] = history[s * HistoryLength + (head.Value + i) % HistoryLength];
        return result;
    }

    /// <summary>1 回前からの値動き（%）。</summary>
    public float ChangePercent(int s)
    {
        if (history == null || history.Count < (s + 1) * HistoryLength) return 0f;
        int prev = history[s * HistoryLength + (head.Value + HistoryLength - 2) % HistoryLength];
        int now = Price(s);
        return prev > 0 ? (now - prev) * 100f / prev : 0f;
    }

    /// <summary>持ち株の評価額（いまの値段 × 株数）。</summary>
    public int MarketValue(int s) => Price(s) * Holding(s);

    /// <summary>持ち株の損益（評価額 − 買うのに払った額）。</summary>
    public int ProfitLoss(int s) => MarketValue(s) - CostBasis(s);

    // ================================================================ 計算（テストからも使う）
    /// <summary>次の値段：price × e^((drift + volatility × z + nudge)/100)。z は標準正規の乱数（±3 で切る）。min〜max に収める。</summary>
    public static int NextPrice(int price, float drift, float volatility, float z, float nudgePercent, int min, int max)
    {
        z = Mathf.Clamp(z, -3f, 3f);
        double next = price * Math.Exp((drift + volatility * z + nudgePercent) / 100.0);
        return Mathf.Clamp((int)Math.Round(next), Mathf.Max(1, min), Mathf.Max(min, max));
    }

    /// <summary>手数料（取引額の percent %。1 円未満は切り上げ。取引額が 0 なら 0）。</summary>
    public static int Fee(int amount, float percent) => amount <= 0 || percent <= 0f ? 0 : Mathf.CeilToInt(amount * percent / 100f);

    /// <summary>quantity 株を売ったあとの「買うのに払った額」（持ち株に比例して減らす）。</summary>
    public static int BasisAfterSell(int basis, int holding, int quantity)
    {
        if (holding <= 0 || quantity >= holding) return 0;
        return basis - (int)Math.Round((double)basis * quantity / holding);
    }

    // ================================================================ 起動
    private void Awake()
    {
        prices = new NetworkList<int>();
        history = new NetworkList<int>();
        holdings = new NetworkList<int>();
        costBasis = new NetworkList<int>();
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;
        if (IsServer)
        {
            random = new System.Random(Environment.TickCount);
            var defs = GameBalance.Current.Stocks;
            if (prices.Count != defs.Length)
            {
                prices.Clear(); history.Clear(); holdings.Clear(); costBasis.Clear();
                foreach (var d in defs)
                {
                    prices.Add(Mathf.Max(1, d.startPrice));
                    holdings.Add(0);
                    costBasis.Add(0);
                    for (int i = 0; i < HistoryLength; i++) history.Add(Mathf.Max(1, d.startPrice));
                }
                head.Value = 0;
            }
            pendingNudge = new float[defs.Length];
        }
        prices.OnListChanged += OnListChanged;
        holdings.OnListChanged += OnListChanged;
        tickCount.OnValueChanged += OnTick;
    }

    public override void OnNetworkDespawn()
    {
        prices.OnListChanged -= OnListChanged;
        holdings.OnListChanged -= OnListChanged;
        tickCount.OnValueChanged -= OnTick;
        if (Instance == this) Instance = null;
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    private void OnListChanged(NetworkListEvent<int> e) => Changed?.Invoke();
    private void OnTick(int a, int b) => Changed?.Invoke();

    // ================================================================ 値動き（サーバー）
    private void Update()
    {
        if (!IsSpawned || !IsServer) return;
        DayManager dm = DayManager.Instance;
        if (dm == null || !dm.IsSpawned || dm.IsTransitioning || dm.IsGameOver) return;
        tickTimer += Time.deltaTime;
        if (tickTimer < GameBalance.Current.stockTickSeconds) return;
        tickTimer = 0f;
        ServerTick();
    }

    /// <summary>値段を 1 回動かす（確認の道具からも呼ぶ）。</summary>
    public void ServerTick()
    {
        if (!IsServer) return;
        var b = GameBalance.Current;
        var defs = b.Stocks;
        int n = Mathf.Min(defs.Length, prices.Count);
        if (pendingNudge.Length != defs.Length) Array.Resize(ref pendingNudge, defs.Length);

        // ときどきニュース：1 つの銘柄が大きく動く
        if (n > 0 && random.NextDouble() < b.stockNewsChance)
        {
            int s = random.Next(n);
            bool up = random.NextDouble() < 0.5;
            float size = Mathf.Lerp(b.stockNewsPercent.x, b.stockNewsPercent.y, (float)random.NextDouble());
            string[] lines = up ? goodNews : badNews;
            string line = lines.Length > 0 ? string.Format(lines[random.Next(lines.Length)], defs[s].name) : defs[s].name;
            ServerNudge(s, up ? size : -size, line);
        }

        int h = head.Value;
        for (int s = 0; s < n; s++)
        {
            int next = NextPrice(prices[s], defs[s].drift, defs[s].volatility, Gaussian(), pendingNudge[s], b.stockPriceRange.x, b.stockPriceRange.y);
            pendingNudge[s] = 0f;
            prices[s] = next;
            history[s * HistoryLength + h] = next;
        }
        head.Value = (h + 1) % HistoryLength;
        tickCount.Value++;
    }

    /// <summary>
    /// 町の出来事と値段を連動させる入口（サーバーのみ）：次の値動きに percent % を足す。headline があればニュースとして全員に出す。
    /// 例：配達がたくさん成功した夜に ServerNudge(配達の会社, +5, "…")。
    /// </summary>
    public void ServerNudge(int stock, float percent, string headline = null)
    {
        if (!IsServer || stock < 0 || stock >= pendingNudge.Length) return;
        pendingNudge[stock] += percent;
        if (!string.IsNullOrEmpty(headline))
        {
            string arrow = percent >= 0 ? "▲" : "▼";
            news.Value = new FixedString128Bytes(Truncate($"{arrow} {headline}", 40));
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max);

    private float Gaussian()
    {
        double u1 = 1.0 - random.NextDouble(), u2 = random.NextDouble();
        return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2));
    }

    // ================================================================ 売り買い
    /// <summary>買う（各画面から）。</summary>
    public void RequestBuy(int stock, int quantity) { if (IsSpawned) BuyServerRpc(stock, quantity); }

    /// <summary>売る（各画面から）。</summary>
    public void RequestSell(int stock, int quantity) { if (IsSpawned) SellServerRpc(stock, quantity); }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void BuyServerRpc(int stock, int quantity, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        bool ok = ServerBuy(stock, quantity, out string message);
        TradeResultRpc(ok, message, RpcTarget.Single(sender, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SellServerRpc(int stock, int quantity, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        bool ok = ServerSell(stock, quantity, out string message);
        TradeResultRpc(ok, message, RpcTarget.Single(sender, RpcTargetUse.Temp));
    }

    private bool ServerTradeAllowed(int stock, int quantity, out string message)
    {
        message = "いまは取引できません";
        DayManager dm = DayManager.Instance;
        if (dm != null && (dm.IsTransitioning || dm.IsGameOver)) return false;
        message = "銘柄が見つかりません";
        if (stock < 0 || stock >= Count) return false;
        message = "株数が正しくありません";
        return quantity > 0 && quantity <= GameBalance.Current.stockMaxShares;
    }

    /// <summary>買う（サーバー）：代金＋手数料を共同口座から払う。払えたときだけ持ち株が増える。</summary>
    public bool ServerBuy(int stock, int quantity, out string message)
    {
        if (!ServerTradeAllowed(stock, quantity, out message)) return false;
        var b = GameBalance.Current;
        var def = Definition(stock);
        message = $"{def.name}は {b.stockMaxShares} 株までしか持てません";
        if (holdings[stock] + quantity > b.stockMaxShares) return false;
        int amount = prices[stock] * quantity;
        int total = amount + Fee(amount, b.stockFeePercent);
        SharedMoneyManager money = SharedMoneyManager.Instance;
        message = $"共同口座が足りません（必要 ¥{total:N0}）";
        if (money == null || !money.TrySpend(total, SharedMoneyReason.StockBuy, $"stock buy {def.code} x{quantity}")) return false;
        holdings[stock] += quantity;
        costBasis[stock] += total;
        message = $"{def.name}を {quantity} 株買いました（¥{total:N0}）";
        return true;
    }

    /// <summary>売る（サーバー）：代金から手数料を引いて共同口座へ。</summary>
    public bool ServerSell(int stock, int quantity, out string message)
    {
        if (!ServerTradeAllowed(stock, quantity, out message)) return false;
        var b = GameBalance.Current;
        var def = Definition(stock);
        message = $"{def.name}の持ち株が足りません（{holdings[stock]} 株）";
        if (holdings[stock] < quantity) return false;
        int amount = prices[stock] * quantity;
        int total = Mathf.Max(0, amount - Fee(amount, b.stockFeePercent));
        SharedMoneyManager money = SharedMoneyManager.Instance;
        message = "共同口座に入れられませんでした";
        if (money == null || (total > 0 && !money.TryAdd(total, SharedMoneyReason.StockSell, $"stock sell {def.code} x{quantity}"))) return false;
        costBasis[stock] = BasisAfterSell(costBasis[stock], holdings[stock], quantity);
        holdings[stock] -= quantity;
        message = $"{def.name}を {quantity} 株売りました（+¥{total:N0}）";
        return true;
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    private void TradeResultRpc(bool success, string message, RpcParams rpcParams) => TradeResult?.Invoke(success, message);

    // ================================================================ セーブ
    public void ServerCapture(RoomiesSaveData data)
    {
        int n = Count;
        data.stockPrices = new int[n];
        data.stockHoldings = new int[n];
        data.stockCostBasis = new int[n];
        for (int s = 0; s < n; s++) { data.stockPrices[s] = prices[s]; data.stockHoldings[s] = holdings[s]; data.stockCostBasis[s] = costBasis[s]; }
    }

    /// <summary>セーブから戻す（サーバー）。前のセーブで項目が無ければ、最初の値段のまま。</summary>
    public void ServerRestore(int[] savedPrices, int[] savedHoldings, int[] savedBasis)
    {
        if (!IsServer) return;
        var range = GameBalance.Current.stockPriceRange;
        for (int s = 0; s < Count; s++)
        {
            if (savedPrices != null && s < savedPrices.Length && savedPrices[s] > 0)
            {
                prices[s] = Mathf.Clamp(savedPrices[s], Mathf.Max(1, range.x), range.y);
                for (int i = 0; i < HistoryLength; i++) history[s * HistoryLength + i] = prices[s];
            }
            if (savedHoldings != null && s < savedHoldings.Length) holdings[s] = Mathf.Clamp(savedHoldings[s], 0, GameBalance.Current.stockMaxShares);
            if (savedBasis != null && s < savedBasis.Length) costBasis[s] = Mathf.Max(0, savedBasis[s]);
        }
    }
}
