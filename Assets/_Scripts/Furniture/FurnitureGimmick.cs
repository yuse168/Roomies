using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 置いてある家具を「押せる・使える」物にする。
///
/// 家具は1つのプレハブ（NetworkFurniture）で、見た目だけカタログのidで変わる。
/// そのため、ここでも **idごと** に何が起きるかを決めている。
///
/// 使える家具：
///  ・自販機 / エナドリ冷蔵庫 … 飲み物を買う（共同口座から支払い）。しばらく速く動ける
///  ・コーヒーメーカー         … コーヒーを淹れる。寝不足が消えて少し速くなる
///  ・ちょきんばこ             … 自分の所持金を共同口座へ入れる
///  ・アーケード機             … 1プレイ。当たると共同口座にお金が入る
///  ・招き猫                   … なでると小銭が出ることがある（1日1回）
///
/// 規約（AGENTS.md）：見た目は作らない。支払い・抽選はサーバーが決める。
/// 金額や効果時間はインスペクターで調整できる。
/// </summary>
[RequireComponent(typeof(NetworkFurniture))]
public class FurnitureGimmick : HomeAppliance
{
    [Header("自販機・冷蔵庫（飲み物）")]
    [SerializeField, Min(0)] private int drinkCost = 120;
    [SerializeField, Range(1f, 2f)] private float drinkSpeed = 1.25f;
    [SerializeField, Min(1f)] private float drinkSeconds = 30f;

    [Header("コーヒーメーカー")]
    [SerializeField, Min(0)] private int coffeeCost = 80;
    [SerializeField, Range(1f, 2f)] private float coffeeSpeed = 1.15f;
    [SerializeField, Min(1f)] private float coffeeSeconds = 25f;

    [Header("アーケード機")]
    [SerializeField, Min(0)] private int arcadeCost = 50;
    [SerializeField, Min(0)] private int arcadeMaxPrize = 250;

    [Header("招き猫")]
    [SerializeField, Min(0)] private int luckyCatMax = 120;

    [Header("共通")]
    [Tooltip("続けて押せるようになるまでの秒数")]
    [SerializeField, Min(0.1f)] private float useCooldown = 1.2f;

    private NetworkFurniture furniture;
    private PlacedFurniture placed;
    private float nextUseTime;

    // 招き猫は1日1回だけ当たる
    private readonly NetworkVariable<bool> luckyCatUsed = new(false);

    private void Awake()
    {
        furniture = GetComponent<NetworkFurniture>();
    }

    public override void OnNetworkSpawn()
    {
        DayManager.OnMorningArrived += OnMorning;
    }

    public override void OnNetworkDespawn()
    {
        DayManager.OnMorningArrived -= OnMorning;
    }

    private void OnMorning()
    {
        if (IsServer) luckyCatUsed.Value = false;
    }

    /// <summary>この家具のID（見た目と効果の決め手）。</summary>
    private string ItemId
    {
        get
        {
            if (placed == null) placed = GetComponent<PlacedFurniture>();
            return placed != null ? placed.itemId : null;
        }
    }

    public override bool CanInteract()
    {
        if (!IsSpawned || furniture == null || furniture.IsHeld) return false;
        return LabelFor(ItemId) != null;
    }

    public override string Label => LabelFor(ItemId) ?? "";

    /// <summary>IDごとの案内文。使えない家具は null。</summary>
    private string LabelFor(string id)
    {
        switch (id)
        {
            case "vending_machine":
            case "energy_drink_fridge":
                return $"飲み物を買う ¥{drinkCost:N0}";
            case "coffee_maker":
                return $"コーヒーを淹れる ¥{coffeeCost:N0}";
            case "piggy_bank":
                return "所持金を共同口座へ入れる";
            case "arcade_machine":
                return $"遊ぶ ¥{arcadeCost:N0}";
            case "lucky_cat":
                return luckyCatUsed.Value ? "今日はもう招いてくれない" : "招き猫をなでる";
            default:
                return null;
        }
    }

    public override void Interact()
    {
        if (!IsSpawned) return;
        UseServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void UseServerRpc(RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (Time.time < nextUseTime || !ServerIsNear(sender)) return;

        string id = ItemId;
        if (LabelFor(id) == null) return;

        nextUseTime = Time.time + useCooldown;

        var money = SharedMoneyManager.Instance;
        if (money == null) return;

        switch (id)
        {
            case "vending_machine":
            case "energy_drink_fridge":
                Buy(sender, money, drinkCost, drinkSpeed, drinkSeconds,
                    "ごくごく…", "しばらく体が軽い！");
                break;

            case "coffee_maker":
                if (Buy(sender, money, coffeeCost, coffeeSpeed, coffeeSeconds,
                        "コーヒーを淹れた", "眠気が吹き飛んだ！"))
                    ClearSleepyRpc(RpcTarget.Single(sender, RpcTargetUse.Temp));
                break;

            case "piggy_bank":
                Deposit(sender, money);
                break;

            case "arcade_machine":
                PlayArcade(sender, money);
                break;

            case "lucky_cat":
                RubLuckyCat(money);
                break;
        }
    }

    // ---- それぞれの中身（サーバー） ----

    private bool Buy(ulong client, SharedMoneyManager money, int cost,
                     float speed, float seconds, string title, string body)
    {
        if (!money.TrySpend(cost, SharedMoneyReason.FurniturePurchase, "Furniture gimmick"))
        {
            Announce("お金が足りない", $"¥{cost:N0} 必要だった……", NightEventManager.StyleInfo);
            return false;
        }

        GiveSpeedRpc(speed, seconds, RpcTarget.Single(client, RpcTargetUse.Temp));
        Announce(title, body, NightEventManager.StyleFun);
        return true;
    }

    private void Deposit(ulong client, SharedMoneyManager money)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.ConnectedClients.TryGetValue(client, out var connected)) return;

        PlayerEarning earning = connected.PlayerObject != null
            ? connected.PlayerObject.GetComponent<PlayerEarning>()
            : null;
        if (earning == null) return;

        int amount = Mathf.Max(0, earning.GetEarning());
        if (amount <= 0)
        {
            Announce("からっぽ", "入れるお金が無い……", NightEventManager.StyleInfo);
            return;
        }

        earning.SpendEarning(amount);
        money.TryAdd(amount, SharedMoneyReason.DebugGrant, "Piggy bank");
        Announce("ちょきん！", $"共同口座へ ¥{amount:N0} 入れた", NightEventManager.StyleFun);
    }

    private void PlayArcade(ulong client, SharedMoneyManager money)
    {
        if (!money.TrySpend(arcadeCost, SharedMoneyReason.SlotBet, "Arcade"))
        {
            Announce("お金が足りない", $"1プレイ ¥{arcadeCost:N0}", NightEventManager.StyleInfo);
            return;
        }

        int prize = Random.value < 0.45f ? 0 : Random.Range(arcadeCost, arcadeMaxPrize + 1) / 10 * 10;

        if (prize <= 0)
        {
            Announce("ゲームオーバー", "もう1回やる？", NightEventManager.StyleInfo);
            return;
        }

        money.TryAdd(prize, SharedMoneyReason.SlotReward, "Arcade");
        Announce("ハイスコア！", $"景品の ¥{prize:N0} が出てきた", NightEventManager.StyleFun);
    }

    private void RubLuckyCat(SharedMoneyManager money)
    {
        if (luckyCatUsed.Value) return;
        luckyCatUsed.Value = true;

        int amount = Random.Range(0, luckyCatMax + 1) / 10 * 10;
        if (amount <= 0)
        {
            Announce("招き猫", "……今日は何も出なかった。", NightEventManager.StyleInfo);
            return;
        }

        money.TryAdd(amount, SharedMoneyReason.DebugGrant, "Lucky cat");
        Announce("招き猫", $"どこからか ¥{amount:N0} 出てきた！", NightEventManager.StyleFun);
    }

    // ---- 使った本人の画面でだけ起きること ----

    [Rpc(SendTo.SpecifiedInParams)]
    private void GiveSpeedRpc(float scale, float seconds, RpcParams rpcParams = default)
    {
        PlayerMovement movement = LocalMovement();
        if (movement != null) movement.ApplyTemporarySpeed(scale, seconds);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ClearSleepyRpc(RpcParams rpcParams = default)
    {
        PlayerMovement movement = LocalMovement();
        if (movement != null) movement.speedScale = 1f;
    }

    private static PlayerMovement LocalMovement()
    {
        var nm = NetworkManager.Singleton;
        var player = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
        return player != null ? player.GetComponent<PlayerMovement>() : null;
    }
}
