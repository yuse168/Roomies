using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 夜イベント＆ランダムトラブルの管理。
/// シーンに配置不要：DayManagerがランタイムで生成する。
/// ネットワーク送信はDayManagerのRPC中継（ServerSend～）を使うので
/// このクラス自体はNetworkBehaviourではない。
///
/// 夜になった瞬間にサーバーが抽選し、全クライアントへ演出を同期する：
///  ・平和な夜     … 何も起きない
///  ・電気代請求   … 共同口座からランダム額を徴収
///  ・食費請求     … 各プレイヤーの個人所持金からランダム額を徴収
///  ・停電         … 朝まで照明が消えて暗くなる（見た目のみ）
///  ・地震         … 部屋の荷物が吹っ飛ぶ＋画面が揺れる
///  ・家賃値上げ   … 次回の家賃徴収額に上乗せ
///  ・空き巣       … 共同口座から盗まれる＋部屋が荒らされる
///  ・冷蔵庫の故障 … 次の夜の食費が2倍になる
///  ・隣人の騒音   … 翌朝だけ全員の移動が重くなる（寝不足）
///  ・宝くじ当選   … 共同口座に加算
///  ・大家の気まぐれ… 次回の家賃が値引き（基本家賃の半額までが下限）
///  ・落とし物     … 各プレイヤーの個人所持金に加算
///  ・謎の小包     … 中身は半々。当たりなら加算、ハズレなら出費
///
/// Dayが進むほど金額が上がり、平和な夜の確率が下がる。Day2以降は
/// 低確率で2件同時に起きる（出費イベントは重ならないようにしている）。
///
/// 他スクリプトから使える告知API（ServerAnnounce / ServerAnnounceTo）も持つ。
/// </summary>
public class NightEventManager : MonoBehaviour
{
    public static NightEventManager Instance { get; private set; }

    // RPCでbyteとして送るので、既存の値の番号は変えない（末尾に足すだけにする）
    private enum NightEventType : byte
    {
        Peace = 0,          // 平和な夜
        UtilityBill,        // 電気代請求
        FoodBill,           // 食費請求
        Blackout,           // 停電
        Earthquake,         // 地震
        RentHike,           // 家賃値上げ
        Burglar,            // 空き巣
        FridgeBreak,        // 冷蔵庫の故障（次の食費が2倍）
        NoisyNeighbor,      // 隣人の騒音（翌朝だけ移動が重い）
        Lottery,            // 宝くじ当選
        LandlordMood,       // 大家の気まぐれ（家賃値引き）
        FoundMoney,         // 落とし物
        MysteryPackage,     // 謎の小包（当たり）
        MysteryPackageBad,  // 謎の小包（ハズレ）
        BlackoutFixed,      // 停電がブレーカーで直った
        FoodBillCovered,    // 食費を冷蔵庫の食材で肩代わりした
    }

    // バナー色の種類（RPCで送るためbyte化）
    public const byte StyleInfo   = 0;
    public const byte StyleDanger = 1;
    public const byte StyleFun    = 2;
    public const byte StylePeace  = 3;

    [Header("UI")]
    [SerializeField] private NightEventUI eventUI;

    [Header("金額設定")]
    [SerializeField] private int utilityBillMin = 100;
    [SerializeField] private int utilityBillMax = 300;
    [SerializeField] private int foodBillMin = 50;
    [SerializeField] private int foodBillMax = 150;
    [SerializeField] private int rentHikeMin = 100;
    [SerializeField] private int rentHikeMax = 300;
    [SerializeField] private int burglarMin = 150;
    [SerializeField] private int burglarMax = 400;
    [SerializeField] private int lotteryMin = 200;
    [SerializeField] private int lotteryMax = 600;
    [SerializeField] private int foundMoneyMin = 40;
    [SerializeField] private int foundMoneyMax = 120;
    [SerializeField] private int packageMin = 80;
    [SerializeField] private int packageMax = 350;
    [SerializeField] private int rentDiscountMin = 100;
    [SerializeField] private int rentDiscountMax = 250;

    [Header("寝不足（隣人の騒音）")]
    [SerializeField, Range(0.5f, 1f)] private float sleepySpeedScale = 0.85f;

    // 停電の復旧用（ローカル）
    private readonly List<Light> disabledLights = new List<Light>();
    private float prevAmbientIntensity = 1f;
    private Color prevAmbientLight;
    private bool blackoutActive;

    private Coroutine shakeRoutine;

    // サーバー側の持ち越し状態
    private NightEventType lastEvent = NightEventType.Peace;
    private bool fridgeBroken;      // 次の食費請求が2倍になる
    private bool serverBlackoutActive;

    // 各クライアントの持ち越し状態
    private bool sleepyMorning;     // 次の朝だけ移動が重くなる
    private bool sleepyApplied;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // 告知バナーはシーンに実体で置いてある。無いときは作らず知らせる
        if (eventUI == null)
        {
            Debug.LogError(
                "[UI] 夜イベントのバナーがシーンにありません。" +
                "メニュー『Roomies/UI/Build Night Banner』で置いてください。", this);
        }

        DayManager.OnNightArrived += OnNight;
        DayManager.OnMorningArrived += OnMorning;
    }

    private void OnDestroy()
    {
        DayManager.OnNightArrived -= OnNight;
        DayManager.OnMorningArrived -= OnMorning;
        if (Instance == this) Instance = null;
    }

    private static bool IsServer
    {
        get
        {
            var nm = NetworkManager.Singleton;
            return nm != null && nm.IsServer;
        }
    }

    // ---- 夜イベント抽選（サーバー） ----

    private void OnNight()
    {
        // 寝不足は朝のあいだだけ。夜になったら全クライアントで元に戻す。
        ClearSleepPenalty();

        if (!IsServer) return;

        var dm = DayManager.Instance;
        if (dm == null) return;

        int day = dm.CurrentDay;

        // Day2以降はまれに2件同時。ただし出費が2件重なると理不尽なので片方までにする。
        int draws = day >= 2 && Random.value < 0.35f ? 2 : 1;
        bool moneyLossDrawn = false;

        for (int i = 0; i < draws; i++)
        {
            var type = PickRandomEvent(day, moneyLossDrawn);
            if (IsMoneyLoss(type)) moneyLossDrawn = true;
            lastEvent = type;
            RunEvent(dm, type, day);
        }
    }

    // 抽選したイベントをサーバーで実行し、全クライアントへ演出を送る
    private void RunEvent(DayManager dm, NightEventType type, int day)
    {
        switch (type)
        {
            case NightEventType.Peace:
            case NightEventType.Blackout:
            case NightEventType.FridgeBreak:
            case NightEventType.NoisyNeighbor:
                if (type == NightEventType.FridgeBreak)
                {
                    fridgeBroken = true;
                    // 家に置いてある冷蔵庫も壊す（夜のうちに直せる）
                    if (NightFridge.Instance != null) NightFridge.Instance.ServerBreak();
                }
                if (type == NightEventType.Blackout)
                {
                    serverBlackoutActive = true;
                    // ブレーカーを落とす（夜のうちに上げ直せる）
                    if (HouseBreaker.Instance != null) HouseBreaker.Instance.ServerTrip();
                }
                dm.ServerSendNightEvent((byte)type, 0);
                break;

            case NightEventType.UtilityBill:
            {
                int amount = TakeFromSharedAccount(
                    ScaledAmount(utilityBillMin, utilityBillMax, day),
                    SharedMoneyReason.UtilityBill, "Night event");
                dm.ServerSendNightEvent((byte)type, amount);
                break;
            }

            case NightEventType.FoodBill:
            {
                int amount = ScaledAmount(foodBillMin, foodBillMax, day);

                // 冷蔵庫に食材が入っていれば、今回の食費はそれでしのげる
                if (NightFridge.Instance != null && NightFridge.Instance.ServerConsumeStock())
                {
                    dm.ServerSendNightEvent((byte)NightEventType.FoodBillCovered, amount);
                    break;
                }

                if (fridgeBroken)
                {
                    // 冷蔵庫が壊れた次の夜は食材が傷んでいる
                    amount *= 2;
                    fridgeBroken = false;
                }
                TakeFromEveryone(amount);
                dm.ServerSendNightEvent((byte)type, amount);
                break;
            }

            case NightEventType.Earthquake:
                StartCoroutine(EarthquakeRoutine());
                dm.ServerSendNightEvent((byte)type, 0);
                break;

            case NightEventType.RentHike:
            {
                int amount = ScaledAmount(rentHikeMin, rentHikeMax, day);
                dm.ServerAddRentSurcharge(amount);
                dm.ServerSendNightEvent((byte)type, amount);
                break;
            }

            case NightEventType.Burglar:
            {
                int amount = TakeFromSharedAccount(
                    ScaledAmount(burglarMin, burglarMax, day),
                    SharedMoneyReason.NightTrouble, "Burglar");
                // 荒らされた痕跡として部屋の荷物を1回だけ散らす
                ScatterCarryables(1.5f, 3.5f, 2f);
                dm.ServerSendNightEvent((byte)type, amount);
                break;
            }

            case NightEventType.Lottery:
            {
                int amount = ScaledAmount(lotteryMin, lotteryMax, day);
                var money = SharedMoneyManager.Instance;
                if (money == null ||
                    !money.TryAdd(amount, SharedMoneyReason.NightLuck, "Lottery")) amount = 0;
                dm.ServerSendNightEvent((byte)type, amount);
                break;
            }

            case NightEventType.LandlordMood:
            {
                int amount = RandomAmount(rentDiscountMin, rentDiscountMax);
                dm.ServerAddRentSurcharge(-amount);
                dm.ServerSendNightEvent((byte)type, amount);
                break;
            }

            case NightEventType.FoundMoney:
            {
                int amount = RandomAmount(foundMoneyMin, foundMoneyMax);
                foreach (var pe in FindObjectsByType<PlayerEarning>()) pe.AddEarning(amount);
                dm.ServerSendNightEvent((byte)type, amount);
                break;
            }

            case NightEventType.MysteryPackage:
            {
                int amount = ScaledAmount(packageMin, packageMax, day);
                var money = SharedMoneyManager.Instance;

                if (Random.value < 0.5f)
                {
                    if (money == null ||
                        !money.TryAdd(amount, SharedMoneyReason.NightLuck, "Mystery package")) amount = 0;
                    dm.ServerSendNightEvent((byte)NightEventType.MysteryPackage, amount);
                }
                else
                {
                    amount = TakeFromSharedAccount(
                        amount, SharedMoneyReason.NightTrouble, "Mystery package");
                    dm.ServerSendNightEvent((byte)NightEventType.MysteryPackageBad, amount);
                }
                break;
            }
        }
    }

    private static int TakeFromSharedAccount(int amount, SharedMoneyReason reason, string note)
    {
        var money = SharedMoneyManager.Instance;
        return money != null ? money.SpendUpTo(amount, reason, note) : 0;
    }

    private static void TakeFromEveryone(int amount)
    {
        foreach (var pe in FindObjectsByType<PlayerEarning>())
            pe.SpendEarning(Mathf.Min(amount, Mathf.Max(0, pe.GetEarning())));
    }

    // 共同口座・個人所持金が減るイベント（同じ夜に2件重ねない判定に使う）
    private static bool IsMoneyLoss(NightEventType type) =>
        type == NightEventType.UtilityBill ||
        type == NightEventType.FoodBill ||
        type == NightEventType.Burglar ||
        type == NightEventType.RentHike ||
        type == NightEventType.MysteryPackage;   // 中身がハズレだと出費になる

    private NightEventType PickRandomEvent(int day, bool skipMoneyLoss)
    {
        // Dayが進むほど「何も起きない夜」が減る（値は GameBalance.asset で調整）
        int peaceWeight = GameBalance.Current.PeaceWeight(day);

        (NightEventType type, int weight)[] table =
        {
            (NightEventType.Peace,          peaceWeight),
            (NightEventType.UtilityBill,    14),
            (NightEventType.FoodBill,       14),
            (NightEventType.Blackout,       10),
            (NightEventType.Earthquake,     10),
            (NightEventType.RentHike,        8),
            (NightEventType.Burglar,         9),
            (NightEventType.FridgeBreak,     7),
            (NightEventType.NoisyNeighbor,   8),
            (NightEventType.Lottery,         6),
            (NightEventType.LandlordMood,    6),
            (NightEventType.FoundMoney,      7),
            (NightEventType.MysteryPackage,  8),
        };

        int total = 0;
        foreach (var e in table) total += Weight(e, day, skipMoneyLoss);

        // 全部除外されることはないが、保険として平和な夜を返す
        if (total <= 0) return NightEventType.Peace;

        int roll = Random.Range(0, total);
        foreach (var e in table)
        {
            int weight = Weight(e, day, skipMoneyLoss);
            if (roll < weight) return e.type;
            roll -= weight;
        }
        return NightEventType.Peace;
    }

    private int Weight((NightEventType type, int weight) entry, int day, bool skipMoneyLoss)
    {
        // 同じ夜の2件目で出費が重なるのは避ける
        if (skipMoneyLoss && IsMoneyLoss(entry.type)) return 0;
        // 2件目に「何も起きない」が来ても告知が無駄になるだけなので外す
        if (skipMoneyLoss && entry.type == NightEventType.Peace) return 0;
        // 直前の夜と同じイベントは出さない（同じ告知が2晩続くと飽きる）
        if (entry.type == lastEvent) return 0;
        // Day1で家賃値上げが来ると立て直せないので出さない
        if (day <= 1 && entry.type == NightEventType.RentHike) return 0;

        return entry.weight;
    }

    private static int RandomAmount(int min, int max)
    {
        // 10円単位のランダム額
        return Random.Range(min / 10, max / 10 + 1) * 10;
    }

    // Dayが進むほど請求も当たりも大きくなる（倍率は GameBalance.asset で調整）
    private static int ScaledAmount(int min, int max, int day)
    {
        float scale = GameBalance.Current.NightScale(day);
        int amount = Mathf.RoundToInt(RandomAmount(min, max) * scale);
        return Mathf.Max(10, amount / 10 * 10);
    }

    // ---- 全クライアント演出（DayManagerのRPC中継から呼ばれる） ----

    /// <summary>夜イベントの見た目・バナーを再生する（各クライアントローカル）。</summary>
    public void PlayEventVisual(byte typeByte, int value)
    {
        var type = (NightEventType)typeByte;

        switch (type)
        {
            case NightEventType.Peace:
                ShowBanner("しずかな夜", "今夜は何も起きなさそうだ……", StylePeace);
                break;

            case NightEventType.UtilityBill:
                ShowBanner("電気代の請求！", $"共同口座から ¥{value:N0} 引き落とされた！", StyleDanger);
                break;

            case NightEventType.FoodBill:
                ShowBanner("食費の請求！", $"ひとり ¥{value:N0} ずつ支払った……腹は減る。", StyleDanger);
                break;

            case NightEventType.Blackout:
                ShowBanner("停電発生！", "ブレーカーが落ちた！朝まで真っ暗……", StyleDanger);
                ApplyBlackout();
                break;

            case NightEventType.Earthquake:
                ShowBanner("地震だー！！", "部屋のものが吹っ飛んだ！", StyleDanger);
                break;

            case NightEventType.RentHike:
                ShowBanner("家賃値上げのお知らせ", $"大家さん「次の家賃、¥{value:N0} 上乗せね」", StyleDanger);
                break;

            case NightEventType.Burglar:
                ShowBanner("空き巣に入られた！",
                    value > 0
                        ? $"共同口座から ¥{value:N0} 盗まれて、部屋も荒らされた！"
                        : "部屋は荒らされたが、盗られる金すら無かった……",
                    StyleDanger);
                ShakeCamera(0.5f, 0.35f);
                break;

            case NightEventType.FridgeBreak:
                ShowBanner("冷蔵庫が壊れた", "中身が傷みはじめた……次の食費は高くつきそうだ。", StyleDanger);
                break;

            case NightEventType.NoisyNeighbor:
                ShowBanner("隣の部屋がうるさい！", "ドンドン響いて眠れない……明日の朝は体が重いぞ。", StyleInfo);
                sleepyMorning = true;
                break;

            case NightEventType.Lottery:
                ShowBanner("宝くじが当たった！", $"共同口座に ¥{value:N0} 入った！今夜は勝ちだ！", StyleFun);
                break;

            case NightEventType.LandlordMood:
                ShowBanner("大家さんの気まぐれ", $"「今回はまあ……¥{value:N0} 引いといてあげる」", StyleFun);
                break;

            case NightEventType.FoundMoney:
                ShowBanner("道端の落とし物", $"全員が ¥{value:N0} ずつ拾った。ちょっと得した。", StyleFun);
                break;

            case NightEventType.MysteryPackage:
                ShowBanner("謎の小包が届いた", $"開けたら現金 ¥{value:N0}！差出人は不明……", StyleFun);
                break;

            case NightEventType.BlackoutFixed:
                ShowBanner("ブレーカー復旧！", "明かりが戻った。よくやった！", StyleFun);
                RestoreBlackout();
                break;

            case NightEventType.FoodBillCovered:
                ShowBanner("食費はかからなかった",
                    $"冷蔵庫の食材でしのいだ（浮いた額 ¥{value:N0}）。", StyleFun);
                break;

            case NightEventType.MysteryPackageBad:
                ShowBanner("謎の小包が届いた",
                    $"中身は請求書だった。¥{value:N0} 払わされた……",
                    StyleDanger);
                break;
        }

        PlayEventSound(type);
    }

    // 効果音。クリップが未入荷のキーはGameAudio側で黙って無視される。
    private static void PlayEventSound(NightEventType type)
    {
        switch (type)
        {
            case NightEventType.Peace:
                GameAudio.Play("night/peace");
                break;
            case NightEventType.Lottery:
            case NightEventType.LandlordMood:
            case NightEventType.FoundMoney:
            case NightEventType.MysteryPackage:
            case NightEventType.BlackoutFixed:
            case NightEventType.FoodBillCovered:
                GameAudio.Play("night/lucky");
                break;
            default:
                GameAudio.Play("night/trouble");
                break;
        }
    }

    private void OnMorning()
    {
        // 停電の復旧（各クライアントがローカルで戻す）
        RestoreBlackout();

        if (IsServer)
        {
            serverBlackoutActive = false;
            if (HouseBreaker.Instance != null) HouseBreaker.Instance.ServerReset();
        }

        // 隣人の騒音があった夜の翌朝だけ、寝不足で体が重い
        if (sleepyMorning)
        {
            sleepyMorning = false;
            ApplySleepPenalty();
        }
    }

    // ---- 寝不足（ローカル：自分のプレイヤーだけ） ----

    private void ApplySleepPenalty()
    {
        var movement = LocalMovement();
        if (movement == null) return;

        movement.speedScale = Mathf.Clamp(sleepySpeedScale, 0.5f, 1f);
        sleepyApplied = true;
        ShowBanner("寝不足の朝", "ゆうべの騒音で体が重い……今日は走りが鈍る。", StyleInfo);
    }

    private void ClearSleepPenalty()
    {
        if (!sleepyApplied) return;
        sleepyApplied = false;

        var movement = LocalMovement();
        if (movement != null) movement.speedScale = 1f;
    }

    private static PlayerMovement LocalMovement()
    {
        var nm = NetworkManager.Singleton;
        var player = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
        return player != null ? player.GetComponent<PlayerMovement>() : null;
    }

    // ---- 停電（ローカル演出） ----

    private void ApplyBlackout()
    {
        if (blackoutActive) return;
        blackoutActive = true;

        prevAmbientIntensity = RenderSettings.ambientIntensity;
        prevAmbientLight = RenderSettings.ambientLight;

        disabledLights.Clear();
        foreach (var light in FindObjectsByType<Light>())
        {
            if (light != null && light.enabled)
            {
                light.enabled = false;
                disabledLights.Add(light);
            }
        }

        RenderSettings.ambientIntensity = 0.15f;
        RenderSettings.ambientLight = new Color(0.05f, 0.05f, 0.10f);
    }

    private void RestoreBlackout()
    {
        if (!blackoutActive) return;
        blackoutActive = false;

        foreach (var light in disabledLights)
        {
            if (light != null) light.enabled = true;
        }
        disabledLights.Clear();

        RenderSettings.ambientIntensity = prevAmbientIntensity;
        RenderSettings.ambientLight = prevAmbientLight;
    }

    // ---- 地震（サーバー物理＋全員の画面揺れ） ----

    private IEnumerator EarthquakeRoutine()
    {
        const int pulses = 3;

        for (int i = 0; i < pulses; i++)
        {
            ScatterCarryables(2f, 5f, 3f);

            var dm = DayManager.Instance;
            if (dm != null) dm.ServerSendCameraShake(0.8f, 0.25f);

            yield return new WaitForSeconds(1.2f);
        }
    }

    /// <summary>
    /// 部屋の持てる物を散らす（サーバー物理）。地震と空き巣で強さを変えて使う。
    /// 持たれている物（kinematic）は手から飛ばさないので除外する。
    /// </summary>
    private static void ScatterCarryables(float minForce, float maxForce, float torque)
    {
        foreach (var obj in FindObjectsByType<CarryableObject>())
        {
            if (obj == null || obj.rb == null || obj.rb.isKinematic) continue;

            Vector3 dir = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(0.6f, 1f),
                Random.Range(-1f, 1f));

            obj.rb.AddForce(dir.normalized * Random.Range(minForce, maxForce), ForceMode.Impulse);
            obj.rb.AddTorque(Random.insideUnitSphere * torque, ForceMode.Impulse);
        }
    }

    /// <summary>画面揺れ（各クライアントローカル）。DayManagerのRPC中継から呼ばれる。</summary>
    public void ShakeCamera(float duration, float magnitude)
    {
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        var cam = Camera.main;
        if (cam == null) yield break;

        Transform t = cam.transform;
        Vector3 basePos = t.localPosition;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float damper = 1f - elapsed / duration;
            t.localPosition = basePos + Random.insideUnitSphere * magnitude * damper;
            yield return null;
        }

        t.localPosition = basePos;
        shakeRoutine = null;
    }

    // ---- 他スクリプト向けの告知API（サーバーから呼ぶ） ----

    /// <summary>
    /// ブレーカーを上げて停電を直す（サーバーのみ）。全員の画面の明かりを戻す。
    /// </summary>
    public void ServerFixBlackout()
    {
        if (!IsServer || !serverBlackoutActive) return;

        serverBlackoutActive = false;
        var dm = DayManager.Instance;
        if (dm != null) dm.ServerSendNightEvent((byte)NightEventType.BlackoutFixed, 0);
    }

    /// <summary>全員にバナー告知（サーバーのみ有効）。</summary>
    public void ServerAnnounce(string title, string body, byte style = StyleFun)
    {
        if (!IsServer) return;
        var dm = DayManager.Instance;
        if (dm != null) dm.ServerSendAnnounce(title, body, style);
    }

    /// <summary>特定プレイヤーだけにバナー告知（サーバーのみ有効）。</summary>
    public void ServerAnnounceTo(ulong clientId, string title, string body, byte style = StyleInfo)
    {
        if (!IsServer) return;
        var dm = DayManager.Instance;
        if (dm != null) dm.ServerSendAnnounceTo(clientId, title, body, style);
    }

    /// <summary>バナー表示（ローカル）。色・アイコンはNightEventUI側がstyleから決める。</summary>
    public void ShowBanner(string title, string body, byte style)
    {
        if (eventUI == null) return;

        eventUI.Show(title, body, style);
    }
}
