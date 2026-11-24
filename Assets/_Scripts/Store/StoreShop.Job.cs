using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// コンビニのレジ打ちのバイト（2026/11/24）。朝だけ、NPC のお客さん 5 人のお会計をして稼ぐ。
///
/// 始め方：カウンターの左の端のタイムカードを見て E（レジの中へ移る）。R でいつでも終わる。
/// 1 人のお客さん：入ってきて品物を置く → E で 1 つずつ読み取る → お札を受け取り、ホイールでお釣りを選んで E → E で袋を渡す。
/// バイト代は、お客さん 1 人ごとにサーバーが共同口座へ入れる（GameBalance の storeJob〇〇。理由は StoreWage）。
/// お釣りを間違える・待たせすぎると、そのお客さんの分は 0（お金は減らない）。全員ミスなしなら、終わりにボーナス。
///
/// 動かす物（品物・読み取りの光・袋・引き出し・画面の金額）は、買い物のお会計と同じ物を使う。
/// 夜の買い物とは時間が重ならない（バイトは朝だけ、お会計は夜だけ）。レジに入れるのは 1 人だけ。お客さん役は NPC だけ。
///
/// プレハブ（Roomies/Store/2 Build Gimmick Prefabs）：
///  ・Pick_TimeCard（タイムカード）・Pick_Till（店員の側から見て押す所）：見て E・R を押す所（トリガー）
///  ・Anchor_Cashier（バイト中に立つ所）・Anchor_JobExit（終えたときに出る所）・Anchor_CustomerDoor・Anchor_CustomerCounter
///  ・Pivot_Customer（お客さん。中の Visual/Look_〇 を 1 つだけ出す。吹き出しは Pivot_Customer/Anchor_Bubble）
///  ・Anchor_JobScreen（店員の側の小さな画面の文字）
/// </summary>
public partial class StoreShop
{
    public enum JobPhase : byte { Off, Wait, Enter, Scan, Change, Bag, Leave }

    [Header("レジ打ちのバイト（朝だけ。StoreShop.Job.cs）")]
    [Tooltip("タイムカード（見て E でバイトに入る・R で終える）")]
    [SerializeField] private Collider timeCardCollider;
    [Tooltip("レジの店員の側（バイト中に見て E・R・ホイール）")]
    [SerializeField] private Collider tillCollider;
    [Tooltip("バイト中に立つ所（向きも使う）")]
    [SerializeField] private Transform anchorCashier;
    [Tooltip("バイトを終えたときに出る所（売り場の側）")]
    [SerializeField] private Transform anchorJobExit;
    [Tooltip("レジの中（ローカルの範囲）。バイト中にここから出ると終わる")]
    [SerializeField] private Vector3 jobAreaCenter = new Vector3(5.05f, 1.2f, 0.35f);
    [SerializeField] private Vector3 jobAreaSize = new Vector3(4.7f, 4f, 1.5f);

    [Header("お客さん（NPC）")]
    [SerializeField] private Transform customerPivot;
    [Tooltip("お客さんの見た目（1 人ごとに 1 つだけ出す）")]
    [SerializeField] private GameObject[] customerLooks = new GameObject[0];
    [SerializeField] private Transform anchorCustomerDoor;
    [SerializeField] private Transform anchorCustomerCounter;
    [SerializeField] private NpcSpeechBubble customerBubble;
    [Tooltip("お客さん用の値段（kindNames の順）。プレイヤーの買い物（食材 1 回分）には使わない")]
    [SerializeField] private int[] kindPrices = { 140, 480, 160, 120, 210, 150, 190, 160 };
    [Tooltip("お客さんが出すお札（小さい順）")]
    [SerializeField] private int[] bills = { 500, 1000, 2000, 5000 };
    [Tooltip("お客さんが小銭を足して、お釣りをきりのいい額にする割合")]
    [SerializeField, Range(0f, 1f)] private float addCoinsChance = 0.35f;
    [Tooltip("1 人のお客さんの品物の数の上限（レジの前に並べる所の数まで）")]
    [SerializeField, Range(1, 6)] private int maxCustomerItems = 3;

    [Header("バイトの画面と時間（秒）")]
    [Tooltip("店員の側の小さな画面（お釣りの候補など）")]
    [SerializeField] private TMP_Text jobScreenText;
    [SerializeField, Min(0.2f)] private float customerWalkSeconds = 1.8f;
    [Tooltip("読み取りを続けて押せる間隔（動きが重ならないように）")]
    [SerializeField, Min(0.05f)] private float jobScanGap = 0.35f;
    [SerializeField, Min(0.2f)] private float customerLeaveSeconds = 1.7f;
    [SerializeField, Min(0f)] private float nextCustomerDelay = 0.5f;
    [SerializeField, Min(0f)] private float firstCustomerDelay = 1.2f;
    [SerializeField] private Color wageColor = new Color(0.36f, 0.82f, 0.45f);
    [SerializeField] private Color missColor = new Color(0.95f, 0.36f, 0.3f);

    [Header("バイトのせりふ")]
    [SerializeField] private string[] jobStartLines = { "助かる！じゃあ、レジお願いね。", "ありがとう！お客さん来るよ、よろしくね。", "おはよう。今日もレジお願いします！" };
    [SerializeField] private string[] customerHelloLines = { "これ、お願いします。", "お願いしまーす。", "これください。", "あ、袋もお願いします。" };
    [SerializeField] private string[] customerPayLines = { "¥{0:N0} で。", "¥{0:N0} からお願いします。", "じゃあ ¥{0:N0} で。" };
    [SerializeField] private string[] customerThanksLines = { "どうもー。", "ありがとう！", "はやいね、ありがとう。", "またね。" };
    [SerializeField] private string[] customerWrongLines = { "あれ？お釣り、¥{0:N0} ですよね。", "えっと……お釣りは ¥{0:N0} じゃないですか？" };
    [SerializeField] private string[] clerkWrongLines = { "あっ、すみません！こちらです。", "失礼しました！（落ち着いていこう）" };
    [SerializeField] private string[] customerWaitLines = { "まだかな……。", "あのー、急いでるんですけど……。" };
    [SerializeField] private string[] customerAngryLines = { "もういいです。", "また今度にします……。" };
    [SerializeField] private string[] clerkAngryLines = { "あー、行っちゃった……。次、がんばろう！" };
    [SerializeField] private string[] jobEndLines = { "おつかれさま！助かったよ。", "おつかれさま。また朝によろしくね。" };
    [SerializeField] private string[] jobPerfectLines = { "ミスなし！すごいね、おつかれさま！", "完ぺき！また頼むね。" };
    [SerializeField] private string jobQuitLine = "おつかれさま。また朝にお願いね。";

    // ---- 全員に届く状態 ----
    private readonly NetworkVariable<ulong> cashier = new(ulong.MaxValue);
    private readonly NetworkVariable<byte> jobPhase = new((byte)JobPhase.Off);
    private readonly NetworkVariable<byte> jobCustomer = new(0);
    private readonly NetworkVariable<byte> jobLook = new(0);
    private readonly NetworkVariable<FixedString32Bytes> jobKinds = new(new FixedString32Bytes());
    private readonly NetworkVariable<byte> jobScanned = new(0);
    private readonly NetworkVariable<int> jobTotal = new(0);
    private readonly NetworkVariable<int> jobPaid = new(0);
    private readonly NetworkVariable<FixedString32Bytes> jobOptions = new(new FixedString32Bytes());
    private readonly NetworkVariable<int> jobEarned = new(0);
    private readonly NetworkVariable<byte> jobMistakes = new(0);

    // ---- サーバーだけ ----
    private double jobUntil, jobItemsAt, jobLastScan, jobAreaFrom;
    private bool jobWrong, jobWaitSaid;
    private int jobServed;

    // ---- 見た目（各画面） ----
    private int changeChoice;
    private Coroutine customerRoutine;
    private int jobScreenKey = int.MinValue;
    private int jobShownTotal;

    public JobPhase CurrentJobPhase => (JobPhase)jobPhase.Value;
    public ulong Cashier => cashier.Value;
    public int JobCustomerIndex => jobCustomer.Value;
    public static int JobCustomers => Mathf.Max(1, GameBalance.Current.storeJobCustomers);
    public int JobEarned => jobEarned.Value;
    public int JobMistakes => jobMistakes.Value;
    public int JobItemCount => jobKinds.Value.Length;
    public int JobScanned => jobScanned.Value;
    public int JobTotal => jobTotal.Value;
    public int JobPaid => jobPaid.Value;
    public int ChangeChoice => changeChoice;
    public Collider TimeCardCollider => timeCardCollider;
    public Collider TillCollider => tillCollider;
    public bool CustomerShown => customerPivot != null && customerPivot.gameObject.activeSelf;
    public NpcSpeechBubble CustomerBubble => customerBubble;
    public string JobScreenLine => jobScreenText != null ? jobScreenText.text : "";
    public int KindPrice(int kind) => kind >= 0 && kind < kindPrices.Length ? kindPrices[kind] : 100;

    /// <summary>いまのお釣りの候補（3 つ。並びは毎回ちがう）。</summary>
    public int[] ChangeOptions()
    {
        var list = new List<int>();
        foreach (string part in jobOptions.Value.ToString().Split(','))
            if (int.TryParse(part, out int v)) list.Add(v);
        return list.ToArray();
    }

    /// <summary>いま選んでいるお釣り。</summary>
    public int SelectedChange
    {
        get
        {
            int[] o = ChangeOptions();
            return o.Length == 0 ? 0 : o[Mathf.Clamp(changeChoice, 0, o.Length - 1)];
        }
    }

    // ================================================================ 決まり（テストからも使う）
    /// <summary>お客さんが出す額：合計以上のいちばん小さいお札。addCoins なら端数の小銭を足して、お釣りをきりのいい額にする。</summary>
    public static int PickPayment(int total, int[] bills, bool addCoins)
    {
        int bill = 0;
        if (bills != null)
            foreach (int b in bills)
                if (b >= total && (bill == 0 || b < bill)) bill = b;
        if (bill == 0) bill = Mathf.Max(1000, Mathf.CeilToInt(total / 1000f) * 1000);
        int paid = bill;
        int rest = total % 100;
        if (addCoins && rest > 0 && bill - total >= 100) paid += rest;
        return paid;
    }

    /// <summary>お釣りの候補：正しい額と、近いまちがい 2 つ（0 未満にはしない）を混ぜる。</summary>
    public static int[] MakeChangeOptions(int change, System.Random random)
    {
        var decoys = new List<int>();
        foreach (int d in new[] { 100, -100, 10, -10, 50, -50, 1000 })
        {
            int v = change + d;
            if (v >= 0 && v != change && !decoys.Contains(v)) decoys.Add(v);
        }
        var picked = new List<int> { change };
        while (picked.Count < 3 && decoys.Count > 0)
        {
            int i = random.Next(decoys.Count);
            picked.Add(decoys[i]);
            decoys.RemoveAt(i);
        }
        for (int i = picked.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (picked[i], picked[j]) = (picked[j], picked[i]);
        }
        return picked.ToArray();
    }

    // ================================================================ 起動
    private void JobOnSpawn()
    {
        jobOptions.OnValueChanged += OnJobOptionsChanged;
        if (customerPivot != null)
        {
            JobPhase p = (JobPhase)jobPhase.Value;
            bool atCounter = p == JobPhase.Scan || p == JobPhase.Change || p == JobPhase.Bag;
            customerPivot.gameObject.SetActive(atCounter);
            if (atCounter && anchorCustomerCounter != null)
            {
                ShowLook(jobLook.Value);
                customerPivot.SetPositionAndRotation(anchorCustomerCounter.position, anchorCustomerCounter.rotation);
            }
        }
    }

    private void JobOnDespawn()
    {
        jobOptions.OnValueChanged -= OnJobOptionsChanged;
    }

    private void OnJobOptionsChanged(FixedString32Bytes oldValue, FixedString32Bytes newValue) => changeChoice = 0;

    // ================================================================ 案内（各画面）
    private string JobLabel(Collider lookedAt, ulong clientId)
    {
        if (lookedAt == null || (lookedAt != timeCardCollider && lookedAt != tillCollider)) return null;
        bool card = lookedAt == timeCardCollider;
        if (cashier.Value == clientId)
        {
            string quit = $"   {Key(GameAction.Rotate)} バイトを終える";
            switch ((JobPhase)jobPhase.Value)
            {
                case JobPhase.Scan: return $"{Key(GameAction.Interact)} 読み取る（{jobScanned.Value}/{JobItemCount}）" + quit;
                case JobPhase.Change: return $"¥{jobPaid.Value:N0} − ¥{jobTotal.Value:N0} → お釣り ＜ ¥{SelectedChange:N0} ＞   {WheelTag} 選ぶ   {Key(GameAction.Interact)} 渡す" + quit;
                case JobPhase.Bag: return $"{Key(GameAction.Interact)} 袋を渡す" + quit;
                case JobPhase.Leave: return "ありがとうございました！" + quit;
                default: return $"お客さんを待っている（{jobCustomer.Value + 1}/{JobCustomers} 人目）" + quit;
            }
        }
        if (cashier.Value != ulong.MaxValue) return card ? "ほかの人がレジのバイト中" : null;
        if (!card) return LocalInJobArea() ? $"{Key(GameAction.Interact)} カウンターの外へ出る" : null;
        string why = JobBlockedReason();
        if (why != null) return why;
        var b = GameBalance.Current;
        return $"{Key(GameAction.Interact)} レジのバイトに入る（お客さん {JobCustomers} 人・1 人 ¥{b.storeJobPayPerCustomer}〜{b.storeJobPayPerCustomer + b.storeJobFastBonus}）";
    }

    /// <summary>いまバイトを始められない理由（始められるときは null）。</summary>
    private string JobBlockedReason()
    {
        DayManager dm = DayManager.Instance;
        if (dm != null && dm.IsNight) return "レジのバイトは朝だけ（夜は買い物の時間）";
        if (dm != null && (dm.IsTransitioning || dm.IsGameOver)) return "いまはバイトできない";
        if (phase.Value != (byte)Phase.Idle) return "お会計中（終わるまで待つ）";
        return null;
    }

    private static bool JobTimeOver()
    {
        DayManager dm = DayManager.Instance;
        return dm != null && (dm.IsNight || dm.IsTransitioning || dm.IsGameOver);
    }

    private bool LocalInJobArea()
    {
        var nm = NetworkManager.Singleton;
        var player = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
        return player != null && InJobArea(player.transform.position);
    }

    private bool InJobArea(Vector3 world)
    {
        Vector3 local = transform.InverseTransformPoint(world) - jobAreaCenter;
        Vector3 h = jobAreaSize * 0.5f;
        return Mathf.Abs(local.x) <= h.x && Mathf.Abs(local.y) <= h.y && Mathf.Abs(local.z) <= h.z;
    }

    // ================================================================ 操作（各画面から）
    private bool JobInteract(Collider lookedAt)
    {
        if (lookedAt == null) return false;
        if (lookedAt == timeCardCollider) { JobCardServerRpc(); return true; }
        if (lookedAt == tillCollider) { JobTillServerRpc(changeChoice); return true; }
        return false;
    }

    private bool JobSecondary(Collider lookedAt)
    {
        if (lookedAt == null || (lookedAt != timeCardCollider && lookedAt != tillCollider)) return false;
        QuitJobServerRpc();
        return true;
    }

    /// <summary>ホイール／←→：お釣りの候補を選ぶ（バイト中の本人の画面だけ）。</summary>
    public override void ChangeBet(int direction)
    {
        if (!IsSpawned || NetworkManager == null || cashier.Value != NetworkManager.LocalClientId || jobPhase.Value != (byte)JobPhase.Change) return;
        int n = ChangeOptions().Length;
        if (n == 0) return;
        changeChoice = ((changeChoice + direction) % n + n) % n;
        GameAudio.PlayAt("store/tick", tillCollider != null ? tillCollider.transform.position : transform.position, 0.8f, 0.05f, 6f);
    }

    // ================================================================ サーバー
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void JobCardServerRpc(RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (cashier.Value != ulong.MaxValue) return;   // バイト中の人が終えるのは R
        if (!ServerInStore(sender, out _)) return;
        string why = JobBlockedReason();
        if (why != null)
        {
            Say(sender, why.StartsWith("レジのバイトは朝だけ") ? "バイトは朝にお願いね。夜はお客さんでどうぞ。" : "いまはちょっと待ってね。");
            return;
        }
        ServerStartJob(sender);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void JobTillServerRpc(int choice, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (cashier.Value != sender)
        {
            // バイトしていないのにレジの中にいる（ふつうは起きない）：外へ出す
            if (cashier.Value == ulong.MaxValue && ServerPlayerPosition(sender, out Vector3 p) && InJobArea(p)) ServerTeleport(sender, anchorJobExit);
            return;
        }
        switch ((JobPhase)jobPhase.Value)
        {
            case JobPhase.Scan: ServerScanOne(); break;
            case JobPhase.Change: ServerGiveChange(choice); break;
            case JobPhase.Bag: ServerHandBag(); break;
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void QuitJobServerRpc(RpcParams rpcParams = default)
    {
        if (cashier.Value != rpcParams.Receive.SenderClientId) return;
        ServerEndJob(false, true);
    }

    private void ServerStartJob(ulong who)
    {
        cashier.Value = who;
        jobCustomer.Value = 0;
        jobEarned.Value = 0;
        jobMistakes.Value = 0;
        jobServed = 0;
        jobKinds.Value = new FixedString32Bytes();
        jobOptions.Value = new FixedString32Bytes();
        jobScanned.Value = 0;
        jobPhase.Value = (byte)JobPhase.Wait;
        jobUntil = ServerNow + firstCustomerDelay;
        jobAreaFrom = ServerNow + 2.0;   // 移ったばかりの位置が届くまで、レジの中にいるかは見ない
        ServerTeleport(who, anchorCashier);
        ClerkSay(Pick(jobStartLines));
        JobStartRpc();
    }

    private void ServerNextCustomer()
    {
        int lanes = Mathf.Max(1, Mathf.Min(maxCustomerItems, Mathf.Min(counterItems.Length, laneSpots.Length)));
        int count = Random.Range(1, lanes + 1);
        var kinds = new FixedString32Bytes();
        int total = 0;
        for (int i = 0; i < count; i++)
        {
            int k = Random.Range(0, kindNames.Length);
            kinds.Append((char)('a' + k));
            total += KindPrice(k);
        }
        int paid = PickPayment(total, bills, Random.value < addCoinsChance);
        int[] options = MakeChangeOptions(paid - total, new System.Random(Random.Range(0, int.MaxValue)));
        jobLook.Value = (byte)Random.Range(0, Mathf.Max(1, customerLooks.Length));
        jobKinds.Value = kinds;
        jobTotal.Value = total;
        jobPaid.Value = paid;
        jobOptions.Value = new FixedString32Bytes(string.Join(",", options));
        jobScanned.Value = 0;
        jobWrong = false;
        jobWaitSaid = false;
        jobPhase.Value = (byte)JobPhase.Enter;
        jobUntil = ServerNow + customerWalkSeconds + itemSlideSeconds * count + 0.25f;
        CustomerEnterRpc(jobLook.Value, kinds);
    }

    private void ServerScanOne()
    {
        int count = JobItemCount;
        if (jobScanned.Value >= count || ServerNow - jobLastScan < jobScanGap) return;
        jobLastScan = ServerNow;
        int i = jobScanned.Value;
        string kinds = jobKinds.Value.ToString();
        int running = 0;
        for (int k = 0; k <= i; k++) running += KindPrice(kinds[k] - 'a');
        jobScanned.Value = (byte)(i + 1);
        JobScanRpc(i, kinds[i] - 'a', running);
        if (i + 1 >= count)
        {
            jobPhase.Value = (byte)JobPhase.Change;
            if (customerBubble != null) customerBubble.ServerSay(string.Format(Pick(customerPayLines), jobPaid.Value));
            JobPayRpc(jobTotal.Value, jobPaid.Value);
        }
    }

    private void ServerGiveChange(int choice)
    {
        int[] options = ChangeOptions();
        if (choice < 0 || choice >= options.Length) return;
        int correct = jobPaid.Value - jobTotal.Value;
        bool ok = options[choice] == correct;
        if (!ok)
        {
            jobWrong = true;
            jobMistakes.Value++;
            if (customerBubble != null) customerBubble.ServerSay(string.Format(Pick(customerWrongLines), correct));
            ClerkSay(Pick(clerkWrongLines));
        }
        jobPhase.Value = (byte)JobPhase.Bag;
        JobChangeRpc(ok, options[choice], correct);
    }

    private void ServerHandBag()
    {
        var b = GameBalance.Current;
        bool fast = ServerNow - jobItemsAt <= b.storeJobFastSeconds;
        int pay = jobWrong ? 0 : b.storeJobPayPerCustomer + (fast ? b.storeJobFastBonus : 0);
        if (pay > 0 && !ServerPayWage(pay, $"customer {jobCustomer.Value + 1}")) pay = 0;
        jobServed++;
        if (!jobWrong && customerBubble != null) customerBubble.ServerSay(Pick(customerThanksLines));
        jobPhase.Value = (byte)JobPhase.Leave;
        jobUntil = ServerNow + customerLeaveSeconds;
        JobBagRpc(pay, fast && !jobWrong, jobWrong);
    }

    private void ServerCustomerGivesUp()
    {
        jobMistakes.Value++;
        jobServed++;
        if (customerBubble != null) customerBubble.ServerSay(Pick(customerAngryLines));
        ClerkSay(Pick(clerkAngryLines));
        jobPhase.Value = (byte)JobPhase.Leave;
        jobUntil = ServerNow + customerLeaveSeconds;
        JobGiveUpRpc();
    }

    /// <summary>バイト代を共同口座へ入れて、その人の稼ぎにも数える。入らなければ false。</summary>
    private bool ServerPayWage(int amount, string what)
    {
        SharedMoneyManager money = SharedMoneyManager.Instance;
        ulong who = cashier.Value;
        if (amount <= 0 || money == null || !money.TryAdd(amount, SharedMoneyReason.StoreWage, $"Store job {what} client={who}")) return false;
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.ConnectedClients.TryGetValue(who, out var client) && client.PlayerObject != null)
        {
            var earning = client.PlayerObject.GetComponent<PlayerEarning>();
            if (earning != null) earning.AddEarning(amount);
        }
        jobEarned.Value += amount;
        return true;
    }

    private void ServerEndJob(bool completed, bool teleportOut)
    {
        ulong who = cashier.Value;
        if (who == ulong.MaxValue) return;
        var b = GameBalance.Current;
        int bonus = 0;
        if (completed && jobMistakes.Value == 0 && b.storeJobPerfectBonus > 0 && ServerPayWage(b.storeJobPerfectBonus, "perfect")) bonus = b.storeJobPerfectBonus;
        string body = $"+¥{jobEarned.Value:N0}（お客さん {jobServed} 人・ミス {jobMistakes.Value}）" + (bonus > 0 ? $"　ミスなしボーナス +¥{bonus:N0} 込み" : "");
        Tell(who, completed ? "バイトおつかれさま" : "バイトを終えた", body, jobEarned.Value > 0 ? NightEventManager.StyleFun : NightEventManager.StyleInfo);
        ClerkSay(completed ? Pick(jobMistakes.Value == 0 ? jobPerfectLines : jobEndLines) : jobQuitLine);
        if (teleportOut && ConnectedNow(who)) ServerTeleport(who, anchorJobExit);
        JobEndRpc(jobEarned.Value, completed, bonus);
        cashier.Value = ulong.MaxValue;
        jobPhase.Value = (byte)JobPhase.Off;
        jobKinds.Value = new FixedString32Bytes();
        jobOptions.Value = new FixedString32Bytes();
        jobScanned.Value = 0;
    }

    private void JobServerUpdate()
    {
        ulong who = cashier.Value;
        if (who == ulong.MaxValue) return;
        if (!ConnectedNow(who)) { ServerEndJob(false, false); return; }
        if (JobTimeOver()) { ServerEndJob(false, true); return; }
        if (ServerNow > jobAreaFrom && Time.frameCount % 15 == 0 && ServerPlayerPosition(who, out Vector3 p) && !InJobArea(p)) { ServerEndJob(false, false); return; }

        double now = ServerNow;
        switch ((JobPhase)jobPhase.Value)
        {
            case JobPhase.Wait:
                if (now >= jobUntil) ServerNextCustomer();
                break;
            case JobPhase.Enter:
                if (now >= jobUntil)
                {
                    jobPhase.Value = (byte)JobPhase.Scan;
                    jobItemsAt = now;
                    jobLastScan = 0;
                    if (customerBubble != null) customerBubble.ServerSay(Pick(customerHelloLines));
                }
                break;
            case JobPhase.Scan:
            case JobPhase.Change:
            case JobPhase.Bag:
                float patience = GameBalance.Current.storeJobPatienceSeconds;
                double waited = now - jobItemsAt;
                if (!jobWaitSaid && waited > patience * 0.6f)
                {
                    jobWaitSaid = true;
                    if (customerBubble != null) customerBubble.ServerSay(Pick(customerWaitLines));
                }
                if (waited > patience) ServerCustomerGivesUp();
                break;
            case JobPhase.Leave:
                if (now >= jobUntil)
                {
                    if (jobCustomer.Value + 1 >= JobCustomers) ServerEndJob(true, true);
                    else
                    {
                        jobCustomer.Value++;
                        jobPhase.Value = (byte)JobPhase.Wait;
                        jobUntil = now + nextCustomerDelay;
                    }
                }
                break;
        }
    }

    private static bool ServerPlayerPosition(ulong clientId, out Vector3 position)
    {
        position = Vector3.zero;
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.ConnectedClients.TryGetValue(clientId, out var client) || client.PlayerObject == null) return false;
        position = client.PlayerObject.transform.position;
        return true;
    }

    private static void ServerTeleport(ulong clientId, Transform at)
    {
        if (at == null) return;
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.ConnectedClients.TryGetValue(clientId, out var client) || client.PlayerObject == null) return;
        var sync = client.PlayerObject.GetComponent<PlayerSpawnSync>();
        if (sync != null) sync.ServerTeleport(at.position, at.eulerAngles.y);
    }

    // ================================================================ 見た目（各画面）
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void JobStartRpc()
    {
        Vector3 at = timeCardCollider != null ? timeCardCollider.bounds.center : transform.position;
        GameAudio.PlayAt("store/timecard", at, 1f, 0f, 10f);
        ShowPop(at + Vector3.up * 0.2f, "バイト開始！", popColor);
        jobScreenKey = int.MinValue;
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void CustomerEnterRpc(byte look, FixedString32Bytes kinds)
    {
        if (customerRoutine != null) StopCoroutine(customerRoutine);
        customerRoutine = StartCoroutine(CustomerEnterRoutine(look, kinds.ToString()));
    }

    private void ShowLook(int look)
    {
        for (int i = 0; i < customerLooks.Length; i++)
            if (customerLooks[i] != null) customerLooks[i].SetActive(i == look);
    }

    /// <summary>お客さんがとびらから歩いてきて、カウンターに品物を並べる。</summary>
    private IEnumerator CustomerEnterRoutine(int look, string kinds)
    {
        TakeHomes();
        HideAllMoving();
        jobShownTotal = 0;
        shownTotal = 0;
        SetDisplay("いらっしゃいませ");
        if (customerPivot != null && anchorCustomerDoor != null && anchorCustomerCounter != null)
        {
            ShowLook(look);
            customerPivot.gameObject.SetActive(true);
            GameAudio.PlayAt("store/door_chime", anchorCustomerDoor.position, 0.8f, 0f, 14f);
            yield return Walk(customerPivot, anchorCustomerDoor.position, anchorCustomerCounter.position, customerWalkSeconds, anchorCustomerCounter.rotation);
        }
        if (bagPivot != null) { bagPivot.localPosition = bagHome; bagPivot.localScale = new Vector3(1f, 0.35f, 1f); bagPivot.gameObject.SetActive(true); }
        int n = Mathf.Min(kinds.Length, Mathf.Min(counterItems.Length, laneSpots.Length));
        for (int i = 0; i < n; i++)
        {
            ItemSlot slot = counterItems[i];
            if (slot.pivot == null || laneSpots[i] == null) continue;
            ShowKind(slot, kinds[i] - 'a');
            slot.pivot.localScale = Vector3.one;
            Vector3 from = customerPivot != null ? customerPivot.position + Vector3.up * 0.9f : laneSpots[i].position;
            slot.pivot.gameObject.SetActive(true);
            yield return Slide(slot.pivot, from, laneSpots[i].position, itemSlideSeconds, 0.12f);
            GameAudio.PlayAt("store/putdown", laneSpots[i].position, 1f, 0.06f, 10f);
        }
        customerRoutine = null;
    }

    /// <summary>「+¥45」などの文字を出す所：お客さんの横（読み取りの台の上）。顔と吹き出しに重ならないように。</summary>
    private Vector3 JobPopPoint() => customerPivot.position + transform.TransformDirection(Vector3.right) * 0.8f + Vector3.up * 1.25f;

    /// <summary>歩く：少し上下にはずみ、進む向きを向く。着いたら最後の向きへ。</summary>
    private static IEnumerator Walk(Transform t, Vector3 from, Vector3 to, float seconds, Quaternion endRotation)
    {
        Vector3 flat = to - from; flat.y = 0f;
        Quaternion facing = flat.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(flat) : endRotation;
        float time = 0f;
        while (time < seconds)
        {
            time += Time.deltaTime;
            float u = Mathf.Clamp01(time / seconds);
            float e = Mathf.SmoothStep(0f, 1f, u);
            Vector3 p = Vector3.Lerp(from, to, e);
            p.y += Mathf.Abs(Mathf.Sin(u * Mathf.PI * 6f)) * 0.05f;
            t.position = p;
            t.rotation = u < 0.8f ? facing : Quaternion.Slerp(facing, endRotation, (u - 0.8f) / 0.2f);
            yield return null;
        }
        t.SetPositionAndRotation(to, endRotation);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void JobScanRpc(int index, int kind, int running)
    {
        StartCoroutine(JobScanItem(index, kind, running));
    }

    /// <summary>1 つ読み取る：読み取りの台へ → 光とピッ → 金額が回る → 袋へ。</summary>
    private IEnumerator JobScanItem(int index, int kind, int running)
    {
        if (index < 0 || index >= counterItems.Length) yield break;
        ItemSlot slot = counterItems[index];
        if (slot.pivot == null) yield break;
        if (!slot.pivot.gameObject.activeSelf && index < laneSpots.Length && laneSpots[index] != null)
        {
            ShowKind(slot, kind);
            slot.pivot.position = laneSpots[index].position;
            slot.pivot.gameObject.SetActive(true);
        }
        Vector3 scan = anchorScanner != null ? anchorScanner.position + Vector3.up * 0.04f : slot.pivot.position;
        yield return Slide(slot.pivot, slot.pivot.position, scan, scanSeconds * 0.3f, 0.04f);
        if (scanGlow != null) scanGlow.SetActive(true);
        GameAudio.PlayAt("store/scan", scan, 1f, 0.02f, 10f);
        int from = jobShownTotal;
        jobShownTotal = running;
        yield return RollTotal(from, running, scanSeconds * 0.25f, kind);
        if (scanGlow != null) scanGlow.SetActive(false);
        Vector3 bag = bagPivot != null ? bagPivot.position + Vector3.up * 0.18f : scan;
        yield return Slide(slot.pivot, scan, bag, scanSeconds * 0.22f, 0.1f, true);
        slot.pivot.gameObject.SetActive(false);
        slot.pivot.localScale = Vector3.one;
        int count = Mathf.Max(1, JobItemCount);
        if (bagPivot != null) bagPivot.localScale = new Vector3(1f, Mathf.Lerp(0.35f, 1f, (index + 1f) / count), 1f);
        GameAudio.PlayAt("store/bag", bag, 0.6f, 0.08f, 10f);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void JobPayRpc(int total, int paid)
    {
        StartCoroutine(JobPayRoutine(total, paid));
    }

    private IEnumerator JobPayRoutine(int total, int paid)
    {
        yield return new WaitForSeconds(scanSeconds * 0.8f);
        SetDisplay($"合計 ¥{total:N0}\nお預かり ¥{paid:N0}", true);
        Vector3 at = customerPivot != null ? JobPopPoint() : transform.position;
        ShowPop(at, $"¥{paid:N0} お預かり", popColor);
        GameAudio.PlayAt("store/receipt", at, 0.5f, 0.05f, 10f);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void JobChangeRpc(bool ok, int given, int correct)
    {
        StartCoroutine(JobChangeRoutine(ok, given, correct));
    }

    /// <summary>お釣りを渡す：引き出しが開いてチャリン。まちがえたら赤い文字と低い音。</summary>
    private IEnumerator JobChangeRoutine(bool ok, int given, int correct)
    {
        TakeHomes();
        Vector3 at = drawerPivot != null ? drawerPivot.position : transform.position;
        GameAudio.PlayAt("store/register", at, 1f, 0f, 10f);
        GameAudio.PlayAt("store/coins", at, 0.9f, 0.05f, 10f);
        Vector3 popAt = customerPivot != null ? JobPopPoint() : at;
        if (ok)
        {
            ShowPop(popAt, $"お釣り ¥{given:N0}　OK", wageColor);
            SetDisplay($"お釣り\n¥{given:N0}", true);
        }
        else
        {
            ShowPop(popAt, $"お釣りミス（正しくは ¥{correct:N0}）", missColor);
            SetDisplay($"お釣り\n¥{correct:N0}", true);
            GameAudio.PlayAt("store/job_miss", popAt, 1f, 0f, 10f);
        }
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            if (drawerPivot != null) drawerPivot.localPosition = drawerHome + drawerOpenOffset * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.25f));
            yield return null;
        }
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void JobBagRpc(int pay, bool fast, bool wrong)
    {
        if (customerRoutine != null) StopCoroutine(customerRoutine);
        customerRoutine = StartCoroutine(JobBagRoutine(pay, fast, wrong));
    }

    /// <summary>袋を渡す：袋がお客さんの手へ、レシートが出て、引き出しが閉まる。お客さんは袋を持って出ていく。</summary>
    private IEnumerator JobBagRoutine(int pay, bool fast, bool wrong)
    {
        TakeHomes();
        foreach (var s in counterItems) if (s.pivot != null) s.pivot.gameObject.SetActive(false);
        SetDisplay("ありがとう\nございました", true);
        Vector3 hand = customerPivot != null ? customerPivot.position + customerPivot.rotation * new Vector3(0.45f, 0.62f, 0.15f) : transform.position;
        Vector3 popAt = customerPivot != null ? JobPopPoint() : hand;
        if (pay > 0)
        {
            ShowPop(popAt, fast ? $"+¥{pay:N0}　はやい！" : $"+¥{pay:N0}", wageColor);
            GameAudio.PlayAt("store/job_good", popAt, 1f, 0f, 10f);
        }
        else ShowPop(popAt, "¥0（お釣りミス）", missColor);
        if (receiptPivot != null) { receiptPivot.localScale = new Vector3(1f, 0.01f, 1f); receiptPivot.gameObject.SetActive(true); }
        GameAudio.PlayAt("store/receipt", receiptPivot != null ? receiptPivot.position : hand, 0.6f, 0.05f, 10f);
        if (bagPivot != null && bagPivot.gameObject.activeSelf)
        {
            bagPivot.localScale = Vector3.one;
            GameAudio.PlayAt("store/bag", bagPivot.position, 1f, 0.05f, 10f);
            Vector3 from = bagPivot.position;
            float t = 0f;
            while (t < bagSlideSeconds)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / bagSlideSeconds);
                Vector3 p = Vector3.Lerp(from, hand, 1f - (1f - u) * (1f - u));
                p.y += Mathf.Sin(u * Mathf.PI) * 0.2f;
                bagPivot.position = p;
                if (drawerPivot != null) drawerPivot.localPosition = Vector3.Lerp(drawerHome + drawerOpenOffset, drawerHome, u);
                if (receiptPivot != null) receiptPivot.localScale = new Vector3(1f, u, 1f);
                yield return null;
            }
        }
        if (drawerPivot != null) drawerPivot.localPosition = drawerHome;
        yield return new WaitForSeconds(0.25f);
        if (receiptPivot != null) receiptPivot.gameObject.SetActive(false);
        yield return CustomerWalkOut(true);
        customerRoutine = null;
    }

    /// <summary>お客さんがとびらへ歩いて出ていく（carryBag なら袋を手に持ったまま）。</summary>
    private IEnumerator CustomerWalkOut(bool carryBag)
    {
        if (customerPivot == null || anchorCustomerDoor == null) { HideAllMoving(); yield break; }
        Vector3 from = customerPivot.position, to = anchorCustomerDoor.position;
        Vector3 flat = to - from; flat.y = 0f;
        Quaternion facing = flat.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(flat) : customerPivot.rotation;
        float seconds = Mathf.Max(0.3f, customerLeaveSeconds - 0.7f);
        float time = 0f;
        Quaternion start = customerPivot.rotation;
        while (time < seconds)
        {
            time += Time.deltaTime;
            float u = Mathf.Clamp01(time / seconds);
            Vector3 p = Vector3.Lerp(from, to, u * u * (3f - 2f * u));
            p.y += Mathf.Abs(Mathf.Sin(u * Mathf.PI * 6f)) * 0.05f;
            customerPivot.SetPositionAndRotation(p, Quaternion.Slerp(start, facing, Mathf.Clamp01(u * 4f)));
            if (carryBag && bagPivot != null && bagPivot.gameObject.activeSelf)
                bagPivot.position = customerPivot.position + customerPivot.rotation * new Vector3(0.45f, 0.5f, 0.1f);
            yield return null;
        }
        customerPivot.gameObject.SetActive(false);
        HideAllMoving();
        ShowIdleDisplay();
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void JobGiveUpRpc()
    {
        if (customerRoutine != null) StopCoroutine(customerRoutine);
        customerRoutine = StartCoroutine(JobGiveUpRoutine());
    }

    private IEnumerator JobGiveUpRoutine()
    {
        Vector3 popAt = customerPivot != null ? JobPopPoint() : transform.position;
        ShowPop(popAt, "お客さんが帰ってしまった", missColor);
        GameAudio.PlayAt("store/job_miss", popAt, 1f, 0f, 10f);
        foreach (var s in counterItems) if (s.pivot != null) s.pivot.gameObject.SetActive(false);
        if (bagPivot != null) bagPivot.gameObject.SetActive(false);
        yield return CustomerWalkOut(false);
        customerRoutine = null;
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void JobEndRpc(int earned, bool completed, int bonus)
    {
        if (customerRoutine != null) { StopCoroutine(customerRoutine); customerRoutine = null; }
        if (customerPivot != null) customerPivot.gameObject.SetActive(false);
        HideAllMoving();
        ShowIdleDisplay();
        Vector3 at = tillCollider != null ? tillCollider.bounds.center + Vector3.up * 0.4f : transform.position;
        ShowPop(at, (completed ? "バイト終了" : "バイトを終えた") + $"　+¥{earned:N0}" + (bonus > 0 ? "（ミスなし！）" : ""), earned > 0 ? wageColor : popColor);
        GameAudio.PlayAt(completed ? "store/job_end" : "store/timecard", at, 1f, 0f, 10f);
        jobScreenKey = int.MinValue;
    }

    // ================================================================ 毎フレーム（各画面）
    private void JobClientUpdate()
    {
        // お客さんの呼吸（カウンターで待っている間）
        if (customerPivot != null && customerPivot.gameObject.activeSelf && customerRoutine == null)
        {
            int look = jobLook.Value;
            if (look < customerLooks.Length && customerLooks[look] != null)
                customerLooks[look].transform.localScale = new Vector3(1f, 1f + Mathf.Sin(Time.time * 3.1f) * 0.015f, 1f);
        }
        RefreshJobScreen();
    }

    /// <summary>店員の側の小さな画面の文字（変わったときだけ作り直す）。</summary>
    private void RefreshJobScreen()
    {
        if (jobScreenText == null) return;
        bool mine = IsSpawned && NetworkManager != null && cashier.Value == NetworkManager.LocalClientId;
        int key = jobPhase.Value * 1000003 + jobCustomer.Value * 10007 + jobScanned.Value * 101 + (mine ? changeChoice + 1 : 0) + jobEarned.Value * 7 + jobOptions.Value.Length * 13;
        if (key == jobScreenKey) return;
        jobScreenKey = key;
        string text;
        switch ((JobPhase)jobPhase.Value)
        {
            case JobPhase.Off: text = "アルバイト募集中\n朝だけ・レジ打ち"; break;
            case JobPhase.Scan:
                {
                    string kinds = jobKinds.Value.ToString();
                    int running = 0;
                    for (int k = 0; k < jobScanned.Value && k < kinds.Length; k++) running += KindPrice(kinds[k] - 'a');
                    text = $"読み取り {jobScanned.Value}/{kinds.Length}\n¥{running:N0}";
                    break;
                }
            case JobPhase.Change:
                text = $"合計 ¥{jobTotal.Value:N0}　お預かり ¥{jobPaid.Value:N0}\n" + (mine ? $"お釣り ＜ ¥{SelectedChange:N0} ＞" : "お釣りを用意中");
                break;
            case JobPhase.Bag: text = "袋を渡す\nありがとうございました"; break;
            case JobPhase.Leave: text = $"稼ぎ ¥{jobEarned.Value:N0}\nお客さん {jobCustomer.Value + 1}/{JobCustomers}"; break;
            default: text = $"お客さん {jobCustomer.Value + 1}/{JobCustomers}\nいらっしゃいませ"; break;
        }
        jobScreenText.text = text;
    }
}
