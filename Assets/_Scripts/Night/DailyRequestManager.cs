using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 「今日のおねがい」。朝に1つ出て、その日のうちに達成すると共同口座にボーナスが入る。
///
/// 3日ごとの家賃だけだと、日によってやることが変わらないので、
/// 毎朝ちがう目標を出して「今日は何をするか」を決めやすくする。
///
/// ・朝に1つ抽選して告知する
/// ・進み具合はサーバーが数える（お金の理由とベッドの報告だけを見る）
/// ・達成したら共同口座にボーナスを入れて告知する
/// ・達成できなくても罰は無い（きつくしすぎない）
///
/// 値（報酬・必要数）はインスペクターで変えられる（AGENTS.mdの規約）。
/// </summary>
public class DailyRequestManager : NetworkBehaviour
{
    public static DailyRequestManager Instance { get; private set; }

    /// <summary>おねがいの種類。</summary>
    public enum RequestKind : byte
    {
        None = 0,
        EarnMoney,      // 合計 いくら稼ぐ
        DeliverCount,   // 配達を何件
        GambleWins,     // ギャンブルで何回勝つ
        BuyFurniture,   // 家具を何個置く
        EveryoneSleeps, // 全員で寝て朝にする
    }

    [Header("報酬")]
    [Tooltip("達成したときに共同口座へ入る基本額。日が進むと GameBalance の倍率が掛かる。")]
    [SerializeField, Min(0)] private int baseReward = 300;

    [Header("必要数（Day1のとき）")]
    [SerializeField, Min(100)] private int earnTarget = 600;
    [SerializeField, Min(1)] private int deliverTarget = 3;
    [SerializeField, Min(1)] private int gambleTarget = 2;
    [SerializeField, Min(1)] private int furnitureTarget = 1;

    [Tooltip("日が進むごとに必要数へ掛ける倍率（1日目は1倍）。")]
    [SerializeField, Range(1f, 1.5f)] private float targetGrowth = 1.15f;

    [Header("告知")]
    [Tooltip("半分まで進んだときにも知らせる。")]
    [SerializeField] private bool announceHalfway = true;

    // 同期する状態（表示にも使えるように公開する）
    private readonly NetworkVariable<byte> kind = new((byte)RequestKind.None);
    private readonly NetworkVariable<int> progress = new(0);
    private readonly NetworkVariable<int> target = new(0);
    private readonly NetworkVariable<bool> done = new(false);
    private readonly NetworkVariable<FixedString128Bytes> label = new();

    private bool halfwayAnnounced;

    public RequestKind Kind => (RequestKind)kind.Value;
    public int Progress => progress.Value;
    public int Target => target.Value;
    public bool IsDone => done.Value;

    /// <summary>HUDに出す1行（「配達を 2/3 件」）。おねがいが無いときは空。</summary>
    public string StatusText
    {
        get
        {
            if (Kind == RequestKind.None) return "";
            string body = label.Value.ToString();
            return done.Value ? $"{body}　達成！" : $"{body}　{progress.Value}/{target.Value}";
        }
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;
        DayManager.OnMorningArrived += OnMorning;

        // 初日の朝はイベントが来ないことがあるので、サーバーならその場で決める
        if (IsServer && Kind == RequestKind.None) ServerPickRequest();
    }

    public override void OnNetworkDespawn()
    {
        DayManager.OnMorningArrived -= OnMorning;
        if (Instance == this) Instance = null;
    }

    private void OnMorning()
    {
        if (IsServer) ServerPickRequest();
    }

    // ---- 抽選 ----

    private void ServerPickRequest()
    {
        if (!IsServer || !IsSpawned) return;

        DayManager day = DayManager.Instance;
        int currentDay = day != null ? day.CurrentDay : 1;
        float scale = Mathf.Pow(targetGrowth, Mathf.Max(0, currentDay - 1));

        RequestKind picked = RollKind(currentDay);
        int amount = TargetFor(picked, scale);

        kind.Value = (byte)picked;
        target.Value = amount;
        progress.Value = 0;
        done.Value = false;
        halfwayAnnounced = false;
        label.Value = LabelFor(picked, amount);

        Announce("今日のおねがい",
            $"{label.Value}（達成で ¥{RewardAmount():N0}）",
            NightEventManager.StyleFun);
    }

    /// <summary>種類を決める。1日目は分かりやすいものだけにする。</summary>
    private static RequestKind RollKind(int currentDay)
    {
        if (currentDay <= 1)
            return Random.value < 0.5f ? RequestKind.EarnMoney : RequestKind.DeliverCount;

        int roll = Random.Range(0, 5);
        switch (roll)
        {
            case 0: return RequestKind.EarnMoney;
            case 1: return RequestKind.DeliverCount;
            case 2: return RequestKind.GambleWins;
            case 3: return RequestKind.BuyFurniture;
            default: return RequestKind.EveryoneSleeps;
        }
    }

    private int TargetFor(RequestKind picked, float scale)
    {
        switch (picked)
        {
            case RequestKind.EarnMoney:
                return Mathf.Max(100, Mathf.RoundToInt(earnTarget * scale / 50f) * 50);
            case RequestKind.DeliverCount:
                return Mathf.Max(1, Mathf.RoundToInt(deliverTarget * scale));
            case RequestKind.GambleWins:
                return Mathf.Max(1, Mathf.RoundToInt(gambleTarget * scale));
            case RequestKind.BuyFurniture:
                return Mathf.Max(1, Mathf.RoundToInt(furnitureTarget * scale));
            default:
                return 1;
        }
    }

    private static string LabelFor(RequestKind picked, int amount)
    {
        switch (picked)
        {
            case RequestKind.EarnMoney: return $"今日 ¥{amount:N0} 稼ぐ";
            case RequestKind.DeliverCount: return $"配達を {amount}件";
            case RequestKind.GambleWins: return $"ギャンブルで {amount}回 勝つ";
            case RequestKind.BuyFurniture: return $"家具を {amount}個 置く";
            case RequestKind.EveryoneSleeps: return "全員で寝て朝にする";
            default: return "";
        }
    }

    /// <summary>その日の報酬額（日が進むと上がる）。</summary>
    private int RewardAmount()
    {
        GameBalance balance = GameBalance.Current;
        DayManager day = DayManager.Instance;
        int currentDay = day != null ? day.CurrentDay : 1;

        float scale = balance != null ? balance.NightScale(currentDay) : 1f;
        return Mathf.Max(0, Mathf.RoundToInt(baseReward * scale / 10f) * 10);
    }

    // ---- 進み具合（サーバー） ----

    /// <summary>
    /// お金が動いたときに呼ばれる（<see cref="SharedMoneyManager"/> から）。
    /// 稼ぎの理由を見て、関係するおねがいだけ進める。
    /// </summary>
    public static void ServerNotifyMoney(SharedMoneyReason reason, int amount)
    {
        DailyRequestManager me = Instance;
        if (me == null || !me.IsServer || !me.IsSpawned || me.done.Value) return;

        switch (me.Kind)
        {
            case RequestKind.EarnMoney:
                if (amount > 0 && IsEarning(reason)) me.ServerAddProgress(amount);
                break;

            case RequestKind.DeliverCount:
                if (reason == SharedMoneyReason.DeliveryReward) me.ServerAddProgress(1);
                break;

            case RequestKind.GambleWins:
                if (reason == SharedMoneyReason.SlotReward ||
                    reason == SharedMoneyReason.BlackjackPayout) me.ServerAddProgress(1);
                break;

            case RequestKind.BuyFurniture:
                if (reason == SharedMoneyReason.FurniturePurchase) me.ServerAddProgress(1);
                break;
        }
    }

    /// <summary>全員が寝たときに呼ばれる（<see cref="SleepManager"/> から）。</summary>
    public static void ServerNotifyEveryoneSlept()
    {
        DailyRequestManager me = Instance;
        if (me == null || !me.IsServer || !me.IsSpawned || me.done.Value) return;
        if (me.Kind != RequestKind.EveryoneSleeps) return;

        me.ServerAddProgress(1);
    }

    /// <summary>稼ぎと言える入金かどうか（夜イベントの幸運などは数えない）。</summary>
    private static bool IsEarning(SharedMoneyReason reason)
    {
        switch (reason)
        {
            case SharedMoneyReason.DeliveryReward:
            case SharedMoneyReason.SmugglingReward:
            case SharedMoneyReason.SlotReward:
            case SharedMoneyReason.BlackjackPayout:
            case SharedMoneyReason.MiningSale:
                return true;
            default:
                return false;
        }
    }

    private void ServerAddProgress(int amount)
    {
        if (amount <= 0 || done.Value) return;

        progress.Value = Mathf.Min(target.Value, progress.Value + amount);

        if (progress.Value >= target.Value)
        {
            ServerComplete();
            return;
        }

        if (!announceHalfway || halfwayAnnounced) return;
        if (progress.Value * 2 < target.Value) return;

        halfwayAnnounced = true;
        Announce("おねがい、半分", $"{label.Value}　{progress.Value}/{target.Value}",
            NightEventManager.StyleInfo);
    }

    private void ServerComplete()
    {
        if (done.Value) return;
        done.Value = true;

        int reward = RewardAmount();
        SharedMoneyManager money = SharedMoneyManager.Instance;
        if (money != null && reward > 0)
            money.TryAdd(reward, SharedMoneyReason.DailyRequest, "Daily request");

        Announce("おねがい達成！", $"{label.Value}　ボーナス ¥{reward:N0}", NightEventManager.StyleFun);
    }

    private static void Announce(string title, string body, byte style)
    {
        if (NightEventManager.Instance != null)
            NightEventManager.Instance.ServerAnnounce(title, body, style);
    }
}
