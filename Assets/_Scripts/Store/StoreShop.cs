using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// コンビニ（CORNER STORE）の売り場とレジ（2026/11/24）。
///
/// 買える物は「食材 1 回分」だけ。棚から商品を 3 つ選んでカゴに入れ、レジでお会計すると、家の冷蔵庫に食材が入る。
/// 値段と決まりは、冷蔵庫の前で E を押して補充するときと同じ（NightFridge.StockCost、夜だけ・壊れていない・入っていないとき）。
/// コンビニは、同じ値段の「もう 1 つの買い方」。冷蔵庫の前で補充する流れはそのまま残っている。
///
/// 流れ：棚を見て E（カゴへ。R で 1 つ戻す）→ レジを見て E（お会計：商品を読み取って金額が回る）→ E（支払う）→ E（袋を受け取る）
/// お金・カゴ・お会計の段階はサーバーだけが決め、動き（商品が飛ぶ・読み取りの光・引き出し・袋）は全員の画面に同じように出す。
///
/// プレハブ：Assets/_Prefabs/Store/StoreShop.prefab（Roomies/Store/2 Build Gimmick Prefabs）
///  ・ルート：NetworkObject・このスクリプト・売り場ごとの当たり判定（Pick_〇。見て E を押す所。棚の当たり判定も兼ねる）
///  ・Visual：棚・冷蔵ケース・レジ・商品など見た目だけ（ぶつかるだけの当たり判定は Visual/Collision）
///  ・Pivot_Fly_〇・Pivot_Item_〇・Pivot_Bag・Pivot_Drawer・Pivot_Receipt・Pivot_ScanGlow：動かす物（中の Visual が見た目）
///  ・Anchor_Shelf_〇・Anchor_Pop・Anchor_Display・Anchor_Lane_〇：位置と文字
/// </summary>
public class StoreShop : CasinoGame
{
    public enum Phase : byte { Idle, Scanning, Pay, Bag }

    [Serializable]
    public class Section
    {
        [Tooltip("見て E を押す所（棚の当たり判定も兼ねる）")]
        public Collider collider;
        [Tooltip("売っている物の番号（kindNames の順）")]
        public int kind;
        [Tooltip("手に取った商品が飛びはじめる所（Anchor_Shelf_〇）")]
        public Transform anchor;
    }

    [Serializable]
    public class ItemSlot
    {
        [Tooltip("動かす軸（Pivot_Fly_〇・Pivot_Item_〇）")]
        public Transform pivot;
        [Tooltip("種類ごとの見た目（kindNames の順。出す 1 つだけを表示する）")]
        public GameObject[] kinds = new GameObject[0];
    }

    [Header("売っている物（番号の順）")]
    [SerializeField] private string[] kindNames = { "おにぎり", "お弁当", "パン", "お菓子", "カップめん", "飲み物", "牛乳", "アイス" };
    [Tooltip("食材 1 回分になる、商品の数")]
    [SerializeField, Range(1, 6)] private int itemsPerSet = 3;

    [Header("売り場（見て E を押す所）")]
    [SerializeField] private Section[] sections = new Section[0];

    [Header("レジ")]
    [Tooltip("レジのカウンター（見て E でお会計）")]
    [SerializeField] private Collider registerCollider;
    [Tooltip("レジの前の、商品を並べる所（左から順に）と、読み取りの台・袋の位置")]
    [SerializeField] private Transform[] laneSpots = new Transform[0];
    [SerializeField] private Transform anchorScanner;
    [SerializeField] private Transform anchorBag;
    [Tooltip("袋を渡すとき、袋が滑ってくる所（カウンターの手前のふち）")]
    [SerializeField] private Transform anchorBagGive;
    [Tooltip("お会計のとき、商品が出てくる所（お客さんの側）")]
    [SerializeField] private Transform anchorItemsFrom;

    [Header("動かす物")]
    [SerializeField] private ItemSlot[] flySlots = new ItemSlot[0];
    [SerializeField] private ItemSlot[] counterItems = new ItemSlot[0];
    [SerializeField] private Transform bagPivot;
    [SerializeField] private Transform drawerPivot;
    [Tooltip("引き出しが開く向きと長さ（ローカル）")]
    [SerializeField] private Vector3 drawerOpenOffset = new Vector3(0f, 0f, -0.24f);
    [SerializeField] private Transform receiptPivot;
    [SerializeField] private GameObject scanGlow;

    [Header("文字")]
    [Tooltip("レジのお客さん向けの画面")]
    [SerializeField] private TMP_Text displayText;
    [Tooltip("手に取ったときに出る「+1 おにぎり」")]
    [SerializeField] private TMP_Text popText;
    [SerializeField] private Color popColor = new Color(0.98f, 0.47f, 0.36f);
    [SerializeField] private Color displayColor = new Color(0.55f, 0.95f, 0.9f);

    [Header("店の人（吹き出しは全員に見える）")]
    [SerializeField] private NpcSpeechBubble clerkBubble;
    [SerializeField] private string clerkName = "コンビニの店員";
    [SerializeField] private string[] greetings = { "いらっしゃいませー！", "いらっしゃいませ。ごゆっくりどうぞ。", "いらっしゃいませ！今日もおつかれさまです。", "あ、いらっしゃいませ。" };
    [SerializeField] private string[] checkoutStartLines = { "お預かりしますね。", "お会計ですね。少々お待ちください。" };
    [SerializeField] private string[] totalLines = { "¥{0:N0} になります。", "全部で ¥{0:N0} です。" };
    [SerializeField] private string[] thanksLines = { "ありがとうございましたー！", "ありがとうございました。またどうぞ！", "ありがとうございます。お気をつけて。" };
    [Tooltip("いらっしゃいませを言ったあと、次に言うまでの秒数")]
    [SerializeField, Min(0f)] private float greetCooldown = 8f;

    [Header("時間（秒）")]
    [SerializeField, Min(0.05f)] private float flySeconds = 0.42f;
    [Tooltip("飛ぶ商品の大きさ（見やすいよう、棚の商品より大きく）")]
    [SerializeField, Min(0.1f)] private float flyScale = 1.6f;
    [SerializeField, Min(0.05f)] private float itemSlideSeconds = 0.22f;
    [SerializeField, Min(0.05f)] private float scanSeconds = 0.5f;
    [SerializeField, Min(0.05f)] private float bagSlideSeconds = 0.4f;
    [Tooltip("支払いを待つ長さ（過ぎるとお会計を取り消す。お金は動かない）")]
    [SerializeField, Min(3f)] private float payTimeout = 25f;
    [Tooltip("袋を受け取るのを待つ長さ（過ぎると自動で渡す）")]
    [SerializeField, Min(2f)] private float bagTimeout = 15f;

    [Header("店の中（サーバーの確かめ。ローカルの範囲）")]
    [SerializeField] private Vector3 areaCenter = new Vector3(0f, 1.5f, 0.6f);
    [SerializeField] private Vector3 areaSize = new Vector3(15f, 4f, 11.5f);

    // ---- 全員に届く状態 ----
    // カゴ：「clientId:種類の文字の並び|…」（種類は 'a' + 番号）
    private readonly NetworkVariable<FixedString128Bytes> carts = new(new FixedString128Bytes());
    private readonly NetworkVariable<byte> phase = new((byte)Phase.Idle);
    private readonly NetworkVariable<ulong> buyer = new(ulong.MaxValue);
    private readonly NetworkVariable<FixedString32Bytes> checkoutKinds = new(new FixedString32Bytes());
    private readonly NetworkVariable<int> checkoutTotal = new(0);

    // ---- サーバーだけ ----
    private double deadline;
    private float lastGreetAt = -999f;
    private Coroutine serverScan;

    // ---- 見た目（各画面） ----
    private int nextFly;
    private Coroutine visualRoutine;
    private Vector3 drawerHome, bagHome;
    private bool homesTaken;
    private float popUntil, popStart;
    private Vector3 popFrom;
    private int shownTotal;

    public Phase CurrentPhase => (Phase)phase.Value;
    public ulong Buyer => buyer.Value;
    public int ItemsPerSet => itemsPerSet;
    public int SetPrice => NightFridge.Instance != null ? NightFridge.Instance.StockCost : 0;
    public int SectionCount => sections.Length;
    public Collider SectionCollider(int i) => sections[i].collider;
    public Collider RegisterCollider => registerCollider;
    public string DisplayLine => displayText != null ? displayText.text : "";
    public bool BagShown => bagPivot != null && bagPivot.gameObject.activeSelf;
    public NpcSpeechBubble ClerkBubble => clerkBubble;
    public string KindName(int kind) => kind >= 0 && kind < kindNames.Length ? kindNames[kind] : "?";

    /// <summary>その人のカゴの中身（種類の番号）。</summary>
    public List<int> CartOf(ulong clientId)
    {
        var list = new List<int>();
        string s = carts.Value.ToString();
        foreach (string entry in s.Split('|'))
        {
            int colon = entry.IndexOf(':');
            if (colon <= 0 || !ulong.TryParse(entry.Substring(0, colon), out ulong id) || id != clientId) continue;
            foreach (char c in entry.Substring(colon + 1)) list.Add(c - 'a');
        }
        return list;
    }

    // ================================================================ 起動
    private void TakeHomes()
    {
        if (homesTaken) return;
        homesTaken = true;
        if (drawerPivot != null) drawerHome = drawerPivot.localPosition;
        if (bagPivot != null) bagHome = bagPivot.localPosition;
    }

    private void Awake()
    {
        TakeHomes();
        HideAllMoving();
        if (popText != null) popText.gameObject.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        phase.OnValueChanged += OnPhaseChanged;
        SnapToPhase();
    }

    public override void OnNetworkDespawn()
    {
        phase.OnValueChanged -= OnPhaseChanged;
    }

    private void OnPhaseChanged(byte oldValue, byte newValue)
    {
        if ((Phase)newValue == Phase.Idle) ShowIdleDisplay();
    }

    // ================================================================ 案内（各画面）
    public override string GetLabel(Collider lookedAt, ulong clientId)
    {
        int s = SectionOf(lookedAt);
        if (s >= 0) return SectionLabel(sections[s].kind, clientId);
        if (lookedAt == registerCollider) return RegisterLabel(clientId);
        return null;
    }

    private string SectionLabel(int kind, ulong clientId)
    {
        if (phase.Value != (byte)Phase.Idle && buyer.Value == clientId) return "お会計中";
        int n = CartOf(clientId).Count;
        string back = n > 0 ? $"   {Key(GameAction.Rotate)} 1 つ戻す" : "";
        if (n >= itemsPerSet) return $"カゴがいっぱい（{n}/{itemsPerSet}）  レジへ" + back;
        return $"{Key(GameAction.Interact)} {KindName(kind)}をカゴへ（{n}/{itemsPerSet}）" + back;
    }

    private string RegisterLabel(ulong clientId)
    {
        Phase p = (Phase)phase.Value;
        if (p != Phase.Idle && buyer.Value != clientId) return "ほかの人がお会計中";
        switch (p)
        {
            case Phase.Scanning: return "お会計中…";
            case Phase.Pay: return $"{Key(GameAction.Interact)} 支払う ¥{checkoutTotal.Value:N0}（共同口座）";
            case Phase.Bag: return $"{Key(GameAction.Interact)} 袋を受け取る";
        }
        int n = CartOf(clientId).Count;
        if (n == 0) return $"棚から {itemsPerSet} つ選んで、ここでお会計（食材 1 回分）";
        string clear = $"   {Key(GameAction.Rotate)} カゴを空にする";
        if (n < itemsPerSet) return $"あと {itemsPerSet - n} つ選ぶとお会計（{n}/{itemsPerSet}）" + clear;
        string blocked = BlockedReason();
        if (blocked != null) return blocked + clear;
        return $"{Key(GameAction.Interact)} お会計 ¥{SetPrice:N0}（食材 1 回分）" + clear;
    }

    /// <summary>いまお会計できない理由（できるときは null）。冷蔵庫の前で補充するときと同じ決まり。</summary>
    private static string BlockedReason()
    {
        NightFridge fridge = NightFridge.Instance;
        if (fridge == null || !fridge.IsSpawned) return "（家の冷蔵庫が見つからない）";
        switch (fridge.CurrentStockBlock)
        {
            case NightFridge.StockBlock.NotNight: return "お会計は夜だけ（冷蔵庫の補充と同じ時間）";
            case NightFridge.StockBlock.Broken: return "家の冷蔵庫が壊れている（先に直す）";
            case NightFridge.StockBlock.AlreadyStocked: return "家の冷蔵庫は、もういっぱい";
        }
        return null;
    }

    private int SectionOf(Collider c)
    {
        if (c == null) return -1;
        for (int i = 0; i < sections.Length; i++) if (sections[i].collider == c) return i;
        return -1;
    }

    // ================================================================ 操作（各画面から）
    public override void Interact(Collider lookedAt, PlayerEarning earning)
    {
        if (!IsSpawned) return;
        int s = SectionOf(lookedAt);
        if (s >= 0) PickServerRpc(s);
        else if (lookedAt == registerCollider) RegisterServerRpc();
    }

    public override void Secondary(Collider lookedAt, PlayerEarning earning)
    {
        if (!IsSpawned) return;
        int s = SectionOf(lookedAt);
        if (s >= 0) PutBackServerRpc(s);
        else if (lookedAt == registerCollider) ClearCartServerRpc();
    }

    // ================================================================ サーバー
    /// <summary>その人が店の中にいるか（いれば胸の高さの位置を返す）。</summary>
    private bool ServerInStore(ulong clientId, out Vector3 chest)
    {
        chest = Vector3.zero;
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.ConnectedClients.TryGetValue(clientId, out var client) || client.PlayerObject == null) return false;
        Vector3 p = client.PlayerObject.transform.position;
        chest = p + Vector3.up * 1.1f;
        Vector3 local = transform.InverseTransformPoint(p) - areaCenter;
        Vector3 h = areaSize * 0.5f;
        return Mathf.Abs(local.x) <= h.x && Mathf.Abs(local.y) <= h.y && Mathf.Abs(local.z) <= h.z;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PickServerRpc(int section, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (section < 0 || section >= sections.Length) return;
        if (phase.Value != (byte)Phase.Idle && buyer.Value == sender) return;
        if (!ServerInStore(sender, out Vector3 chest)) return;
        var cart = CartOf(sender);
        if (cart.Count >= itemsPerSet) return;
        int kind = sections[section].kind;
        cart.Add(kind);
        SetCart(sender, cart);
        PickedRpc(section, kind, cart.Count, chest);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PutBackServerRpc(int section, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (section < 0 || section >= sections.Length) return;
        if (!ServerInStore(sender, out Vector3 chest)) return;
        var cart = CartOf(sender);
        if (cart.Count == 0) return;
        int kind = cart[cart.Count - 1];
        cart.RemoveAt(cart.Count - 1);
        SetCart(sender, cart);
        PutBackRpc(section, kind, cart.Count, chest);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ClearCartServerRpc(RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (phase.Value != (byte)Phase.Idle && buyer.Value == sender) return;
        if (CartOf(sender).Count == 0) return;
        SetCart(sender, new List<int>());
        ClerkSay("カゴの物は、棚に戻しておきますね。");
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RegisterServerRpc(RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!ServerInStore(sender, out Vector3 chest)) return;
        Phase p = (Phase)phase.Value;

        if (p == Phase.Idle)
        {
            var cart = CartOf(sender);
            if (cart.Count < itemsPerSet)
            {
                ClerkSay(cart.Count == 0 ? $"棚から {itemsPerSet} つ選んでくださいね。食材 1 回分になります。" : $"あと {itemsPerSet - cart.Count} つですね。");
                return;
            }
            string blocked = BlockedReason();
            if (blocked != null) { Say(sender, ClerkLineFor(blocked)); return; }
            var kinds = new FixedString32Bytes();
            foreach (int k in cart) kinds.Append((char)('a' + k));
            checkoutKinds.Value = kinds;
            checkoutTotal.Value = SetPrice;
            buyer.Value = sender;
            SetCart(sender, new List<int>());
            phase.Value = (byte)Phase.Scanning;
            ClerkSay(Pick(checkoutStartLines));
            StartCheckoutRpc(kinds, checkoutTotal.Value);
            serverScan = StartCoroutine(ServerScanRoutine(cart.Count));
            return;
        }

        if (buyer.Value != sender) return;

        if (p == Phase.Pay)
        {
            string blocked = BlockedReason();
            SharedMoneyManager money = SharedMoneyManager.Instance;
            if (blocked != null) { Say(sender, ClerkLineFor(blocked)); ServerCancel(true); return; }
            if (money == null || !money.TrySpend(checkoutTotal.Value, SharedMoneyReason.UtilityBill, $"Store groceries client={sender}"))
            {
                Say(sender, "共同口座が足りないみたいです……。またどうぞ。");
                ServerCancel(true);
                return;
            }
            if (!NightFridge.Instance.ServerStockBought())
            {
                // 同じ瞬間に冷蔵庫の側で補充された：払った分を戻す（ふつうは起きない）
                money.TryAdd(checkoutTotal.Value, SharedMoneyReason.UtilityBill, "Store groceries refund");
                Say(sender, "あ、冷蔵庫はもういっぱいみたいです。お返ししますね。");
                ServerCancel(false);
                return;
            }
            phase.Value = (byte)Phase.Bag;
            deadline = ServerNow + bagTimeout;
            ClerkSay(Pick(thanksLines));
            PaidRpc(checkoutTotal.Value);
            return;
        }

        if (p == Phase.Bag) ServerHandOver(chest);
    }

    private IEnumerator ServerScanRoutine(int count)
    {
        yield return new WaitForSeconds(itemSlideSeconds * count + scanSeconds * count + 0.35f);
        serverScan = null;
        if (phase.Value != (byte)Phase.Scanning) yield break;
        phase.Value = (byte)Phase.Pay;
        deadline = ServerNow + payTimeout;
        ClerkSay(string.Format(Pick(totalLines), checkoutTotal.Value));
    }

    private void ServerHandOver(Vector3 chest)
    {
        ulong who = buyer.Value;
        TakeBagRpc(chest);
        Tell(who, "食材を買った", "家の冷蔵庫に入れておいた。次の食費はこれでしのげる。", NightEventManager.StyleFun);
        buyer.Value = ulong.MaxValue;
        checkoutKinds.Value = new FixedString32Bytes();
        phase.Value = (byte)Phase.Idle;
    }

    /// <summary>お会計を取り消す。restoreCart なら、選んだ商品をその人のカゴに戻す（お金は動いていない）。</summary>
    private void ServerCancel(bool restoreCart)
    {
        if (serverScan != null) { StopCoroutine(serverScan); serverScan = null; }
        ulong who = buyer.Value;
        if (restoreCart && who != ulong.MaxValue)
        {
            var cart = new List<int>();
            foreach (char c in checkoutKinds.Value.ToString()) cart.Add(c - 'a');
            SetCart(who, cart);
        }
        CancelRpc();
        buyer.Value = ulong.MaxValue;
        checkoutKinds.Value = new FixedString32Bytes();
        phase.Value = (byte)Phase.Idle;
    }

    private void Update()
    {
        if (IsSpawned && IsServer)
        {
            Phase p = (Phase)phase.Value;
            if (p == Phase.Pay && ServerNow > deadline) ServerCancel(true);
            else if (p == Phase.Bag && ServerNow > deadline)
            {
                ServerInStore(buyer.Value, out Vector3 chest);
                ServerHandOver(chest);
            }
            // お会計中の人がいなくなった
            if (p != Phase.Idle && !ConnectedNow(buyer.Value)) ServerCancel(false);
        }
        UpdatePop();
    }

    private static bool ConnectedNow(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.ConnectedClients.ContainsKey(clientId);
    }

    private void SetCart(ulong clientId, List<int> cart)
    {
        var nm = NetworkManager.Singleton;
        var sb = new System.Text.StringBuilder();
        foreach (string entry in carts.Value.ToString().Split('|'))
        {
            int colon = entry.IndexOf(':');
            if (colon <= 0 || !ulong.TryParse(entry.Substring(0, colon), out ulong id) || id == clientId) continue;
            if (nm != null && !nm.ConnectedClients.ContainsKey(id)) continue;   // もういない人のカゴは捨てる
            if (sb.Length > 0) sb.Append('|');
            sb.Append(entry);
        }
        if (cart.Count > 0)
        {
            if (sb.Length > 0) sb.Append('|');
            sb.Append(clientId).Append(':');
            foreach (int k in cart) sb.Append((char)('a' + k));
        }
        carts.Value = new FixedString128Bytes(sb.ToString());
    }

    /// <summary>自動ドアが開いたとき（外から入ってきた人がいるとき）に呼ぶ（サーバーのみ）。</summary>
    public void ServerGreet()
    {
        if (!IsServer || Time.time - lastGreetAt < greetCooldown) return;
        lastGreetAt = Time.time;
        ClerkSay(Pick(greetings));
    }

    private void ClerkSay(string line)
    {
        if (clerkBubble != null) clerkBubble.ServerSay(line);
    }

    /// <summary>吹き出し（全員）と、その人の知らせの帯の両方に出す。</summary>
    private void Say(ulong clientId, string line)
    {
        ClerkSay(line);
        if (NightEventManager.Instance != null)
            NightEventManager.Instance.ServerAnnounceTo(clientId, clerkName, line, NightEventManager.StyleInfo);
    }

    private static string ClerkLineFor(string blocked)
    {
        if (blocked.StartsWith("お会計は夜だけ")) return "すみません、食材は夜にお願いしますね。";
        if (blocked.StartsWith("家の冷蔵庫が壊れている")) return "お家の冷蔵庫、壊れているみたいですよ。先に直してからどうぞ。";
        if (blocked.StartsWith("家の冷蔵庫は、もういっぱい")) return "お家の冷蔵庫、もういっぱいみたいですよ。";
        return "すみません、いまはお会計できないみたいです。";
    }

    private static string Pick(string[] lines) => lines == null || lines.Length == 0 ? "" : lines[UnityEngine.Random.Range(0, lines.Length)];

    // ================================================================ 見た目（各画面）
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void PickedRpc(int section, int kind, int count, Vector3 chest)
    {
        if (section < 0 || section >= sections.Length) return;
        Vector3 from = sections[section].anchor != null ? sections[section].anchor.position : transform.position;
        StartCoroutine(FlyRoutine(kind, from, chest, false));
        ShowPop(from, $"+1 {KindName(kind)}  {count}/{itemsPerSet}");
        GameAudio.PlayAt("store/pick", from, 1f, 0.06f, 10f);
        if (count >= itemsPerSet) GameAudio.PlayAt("store/basket_full", from, 1f, 0f, 10f);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void PutBackRpc(int section, int kind, int count, Vector3 chest)
    {
        if (section < 0 || section >= sections.Length) return;
        Vector3 to = sections[section].anchor != null ? sections[section].anchor.position : transform.position;
        StartCoroutine(FlyRoutine(kind, chest, to, true));
        ShowPop(to, $"-1 {KindName(kind)}  {count}/{itemsPerSet}");
        GameAudio.PlayAt("store/putback", to, 1f, 0.05f, 10f);
    }

    /// <summary>商品が弧を描いて飛ぶ（取る：棚 → 人、戻す：人 → 棚）。少し回って、着く前に小さくなる。</summary>
    private IEnumerator FlyRoutine(int kind, Vector3 from, Vector3 to, bool back)
    {
        if (flySlots.Length == 0) yield break;
        ItemSlot slot = flySlots[nextFly % flySlots.Length];
        nextFly++;
        if (slot.pivot == null) yield break;
        ShowKind(slot, kind);
        slot.pivot.gameObject.SetActive(true);
        float t = 0f;
        float spin = UnityEngine.Random.Range(-1f, 1f) > 0 ? 1f : -1f;
        while (t < flySeconds)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / flySeconds);
            float e = back ? u * u : 1f - (1f - u) * (1f - u);
            Vector3 p = Vector3.Lerp(from, to, e);
            p.y += Mathf.Sin(u * Mathf.PI) * 0.35f;
            slot.pivot.position = p;
            slot.pivot.rotation = Quaternion.Euler(0f, spin * u * 220f, Mathf.Sin(u * Mathf.PI) * 18f);
            // 取るとき：はじめに少しふくらみ、最後に小さくなって人に吸いこまれる。戻すとき：はじめ小さく、棚で元の大きさ
            float s = back ? Mathf.Lerp(0.3f, flyScale, u) : (u < 0.25f ? Mathf.Lerp(flyScale, flyScale * 1.3f, u / 0.25f) : Mathf.Lerp(flyScale * 1.3f, 0.2f, (u - 0.25f) / 0.75f));
            slot.pivot.localScale = Vector3.one * s;
            yield return null;
        }
        slot.pivot.gameObject.SetActive(false);
        slot.pivot.localScale = Vector3.one;
    }

    private void ShowPop(Vector3 at, string text)
    {
        if (popText == null) return;
        popText.text = text;
        popText.color = popColor;
        popFrom = at + Vector3.up * 0.25f;
        popStart = Time.time;
        popUntil = Time.time + 1.1f;
        popText.transform.position = popFrom;
        popText.gameObject.SetActive(true);
    }

    private void UpdatePop()
    {
        if (popText == null || !popText.gameObject.activeSelf) return;
        float u = Mathf.InverseLerp(popStart, popUntil, Time.time);
        if (u >= 1f) { popText.gameObject.SetActive(false); return; }
        popText.transform.position = popFrom + Vector3.up * (0.35f * (1f - (1f - u) * (1f - u)));
        var cam = Camera.main;
        if (cam != null) popText.transform.rotation = Quaternion.LookRotation(popText.transform.position - cam.transform.position);
        Color c = popColor; c.a = u < 0.7f ? 1f : Mathf.InverseLerp(1f, 0.7f, u);
        popText.color = c;
        float pop = u < 0.12f ? Mathf.Lerp(0.6f, 1.15f, u / 0.12f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((u - 0.12f) / 0.15f));
        popText.transform.localScale = Vector3.one * pop;
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void StartCheckoutRpc(FixedString32Bytes kinds, int total)
    {
        if (visualRoutine != null) StopCoroutine(visualRoutine);
        visualRoutine = StartCoroutine(ScanRoutine(kinds.ToString(), total));
    }

    /// <summary>お会計：商品がカウンターに並び、1 つずつ読み取りの台を通って（光・ピッという音・金額が回る）、袋に入る。</summary>
    private IEnumerator ScanRoutine(string kinds, int total)
    {
        TakeHomes();
        HideAllMoving();
        int n = Mathf.Min(kinds.Length, counterItems.Length);
        int[] prices = SplitPrice(total, Mathf.Max(1, kinds.Length));
        shownTotal = 0;
        SetDisplay($"お会計\n¥0");
        if (bagPivot != null) { bagPivot.localPosition = bagHome; bagPivot.localScale = new Vector3(1f, 0.35f, 1f); bagPivot.gameObject.SetActive(true); }
        // 並べる
        for (int i = 0; i < n; i++)
        {
            ItemSlot slot = counterItems[i];
            if (slot.pivot == null) continue;
            ShowKind(slot, kinds[i] - 'a');
            Vector3 to = i < laneSpots.Length && laneSpots[i] != null ? laneSpots[i].position : slot.pivot.position;
            Vector3 from = anchorItemsFrom != null ? anchorItemsFrom.position : to;
            slot.pivot.gameObject.SetActive(true);
            yield return Slide(slot.pivot, from, to, itemSlideSeconds, 0.12f);
            GameAudio.PlayAt("store/putdown", to, 1f, 0.06f, 10f);
        }
        // 読み取る
        for (int i = 0; i < n; i++)
        {
            ItemSlot slot = counterItems[i];
            if (slot.pivot == null) continue;
            Vector3 scan = anchorScanner != null ? anchorScanner.position + Vector3.up * 0.04f : slot.pivot.position;
            yield return Slide(slot.pivot, slot.pivot.position, scan, scanSeconds * 0.45f, 0.04f);
            if (scanGlow != null) scanGlow.SetActive(true);
            GameAudio.PlayAt("store/scan", scan, 1f, 0.02f, 10f);
            int from = shownTotal, to = shownTotal + prices[Mathf.Min(i, prices.Length - 1)];
            yield return RollTotal(from, to, scanSeconds * 0.3f, kinds[i] - 'a');
            if (scanGlow != null) scanGlow.SetActive(false);
            // 袋へ
            Vector3 bag = bagPivot != null ? bagPivot.position + Vector3.up * 0.18f : scan;
            yield return Slide(slot.pivot, scan, bag, scanSeconds * 0.25f, 0.1f, true);
            slot.pivot.gameObject.SetActive(false);
            slot.pivot.localScale = Vector3.one;
            if (bagPivot != null) bagPivot.localScale = new Vector3(1f, Mathf.Lerp(0.35f, 1f, (i + 1f) / n), 1f);
            GameAudio.PlayAt("store/bag", bag, 0.6f, 0.08f, 10f);
        }
        SetDisplay($"合計\n¥{total:N0}", true);
        visualRoutine = null;
    }

    private IEnumerator RollTotal(int from, int to, float seconds, int kind)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            int v = Mathf.RoundToInt(Mathf.Lerp(from, to, Mathf.Clamp01(t / seconds)));
            if (v != shownTotal) { shownTotal = v; SetDisplay($"{KindName(kind)}\n¥{shownTotal:N0}"); }
            yield return null;
        }
        shownTotal = to;
        SetDisplay($"{KindName(kind)}\n¥{shownTotal:N0}");
        GameAudio.PlayAt("store/tick", transform.position, 0.6f, 0.05f, 10f);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void PaidRpc(int total)
    {
        if (visualRoutine != null) StopCoroutine(visualRoutine);
        visualRoutine = StartCoroutine(PaidRoutine(total));
    }

    /// <summary>支払い：引き出しが開いて音、レシートが出てくる、袋がお客さんの側へ滑ってくる。</summary>
    private IEnumerator PaidRoutine(int total)
    {
        TakeHomes();
        foreach (var s in counterItems) if (s.pivot != null) s.pivot.gameObject.SetActive(false);
        SetDisplay($"ありがとう\nございました", true);
        Vector3 at = drawerPivot != null ? drawerPivot.position : transform.position;
        GameAudio.PlayAt("store/register", at, 1f, 0f, 10f);
        GameAudio.PlayAt("store/coins", at, 0.9f, 0.05f, 10f);
        if (bagPivot != null) { bagPivot.gameObject.SetActive(true); bagPivot.localScale = Vector3.one; }
        float t = 0f;
        const float open = 0.25f;
        if (receiptPivot != null) { receiptPivot.localScale = new Vector3(1f, 0.01f, 1f); receiptPivot.gameObject.SetActive(true); }
        GameAudio.PlayAt("store/receipt", receiptPivot != null ? receiptPivot.position : at, 0.8f, 0.05f, 10f);
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            if (drawerPivot != null) drawerPivot.localPosition = drawerHome + drawerOpenOffset * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / open));
            if (receiptPivot != null) receiptPivot.localScale = new Vector3(1f, Mathf.Clamp01(t / 0.6f), 1f);
            yield return null;
        }
        if (bagPivot != null && anchorBagGive != null)
        {
            yield return Slide(bagPivot, bagPivot.position, anchorBagGive.position, bagSlideSeconds, 0.08f);
            GameAudio.PlayAt("store/bag", anchorBagGive.position, 1f, 0.05f, 10f);
        }
        visualRoutine = null;
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void TakeBagRpc(Vector3 chest)
    {
        if (visualRoutine != null) StopCoroutine(visualRoutine);
        visualRoutine = StartCoroutine(TakeBagRoutine(chest));
    }

    /// <summary>袋を受け取る：袋が持ち上がって人のほうへ飛び、小さくなって消える。引き出しが閉じる。</summary>
    private IEnumerator TakeBagRoutine(Vector3 chest)
    {
        TakeHomes();
        if (bagPivot != null && bagPivot.gameObject.activeSelf)
        {
            GameAudio.PlayAt("store/bag", bagPivot.position, 1f, 0.05f, 10f);
            Vector3 from = bagPivot.position;
            float t = 0f;
            while (t < flySeconds)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / flySeconds);
                Vector3 p = Vector3.Lerp(from, chest, 1f - (1f - u) * (1f - u));
                p.y += Mathf.Sin(u * Mathf.PI) * 0.3f;
                bagPivot.position = p;
                bagPivot.localScale = Vector3.one * Mathf.Lerp(1f, 0.2f, u * u);
                if (drawerPivot != null) drawerPivot.localPosition = Vector3.Lerp(drawerHome + drawerOpenOffset, drawerHome, u);
                yield return null;
            }
        }
        HideAllMoving();
        ShowIdleDisplay();
        visualRoutine = null;
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void CancelRpc()
    {
        if (visualRoutine != null) { StopCoroutine(visualRoutine); visualRoutine = null; }
        HideAllMoving();
        ShowIdleDisplay();
    }

    /// <summary>あとから入った人・表示が戻ったとき：いまの段階に見た目をそろえる。</summary>
    private void SnapToPhase()
    {
        TakeHomes();
        HideAllMoving();
        Phase p = (Phase)phase.Value;
        if (p == Phase.Bag && bagPivot != null && anchorBagGive != null)
        {
            bagPivot.gameObject.SetActive(true);
            bagPivot.position = anchorBagGive.position;
            bagPivot.localScale = Vector3.one;
            SetDisplay($"ありがとう\nございました", true);
        }
        else if (p == Phase.Pay || p == Phase.Scanning)
        {
            if (bagPivot != null) { bagPivot.gameObject.SetActive(true); bagPivot.localPosition = bagHome; bagPivot.localScale = Vector3.one; }
            SetDisplay($"合計\n¥{checkoutTotal.Value:N0}", true);
        }
        else ShowIdleDisplay();
    }

    private void HideAllMoving()
    {
        foreach (var s in flySlots) if (s != null && s.pivot != null) s.pivot.gameObject.SetActive(false);
        foreach (var s in counterItems) if (s != null && s.pivot != null) s.pivot.gameObject.SetActive(false);
        if (bagPivot != null) { bagPivot.gameObject.SetActive(false); bagPivot.localPosition = bagHome; bagPivot.localScale = Vector3.one; }
        if (drawerPivot != null) drawerPivot.localPosition = drawerHome;
        if (receiptPivot != null) receiptPivot.gameObject.SetActive(false);
        if (scanGlow != null) scanGlow.SetActive(false);
    }

    private void ShowIdleDisplay() => SetDisplay("いらっしゃいませ");

    private void SetDisplay(string text, bool strong = false)
    {
        if (displayText == null) return;
        displayText.text = text;
        displayText.color = strong ? Color.white : displayColor;
    }

    private static void ShowKind(ItemSlot slot, int kind)
    {
        for (int i = 0; i < slot.kinds.Length; i++) if (slot.kinds[i] != null) slot.kinds[i].SetActive(i == kind);
    }

    private static IEnumerator Slide(Transform t, Vector3 from, Vector3 to, float seconds, float hop, bool shrink = false)
    {
        float time = 0f;
        while (time < seconds)
        {
            time += Time.deltaTime;
            float u = Mathf.Clamp01(time / seconds);
            float e = 1f - (1f - u) * (1f - u);
            Vector3 p = Vector3.Lerp(from, to, e);
            p.y += Mathf.Sin(u * Mathf.PI) * hop;
            t.position = p;
            if (shrink) t.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, u);
            yield return null;
        }
        t.position = to;
    }

    /// <summary>合計を、商品の数で分ける（割り切れない分は最後の商品へ）。合計の値段そのものは変えない。</summary>
    public static int[] SplitPrice(int total, int count)
    {
        var a = new int[Mathf.Max(1, count)];
        int each = total / a.Length;
        for (int i = 0; i < a.Length; i++) a[i] = each;
        a[a.Length - 1] += total - each * a.Length;
        return a;
    }
}
