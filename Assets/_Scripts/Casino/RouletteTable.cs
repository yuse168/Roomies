using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ルーレットの台。何人でも同じ回に賭けられる（全員が同じ 1 回の結果を見る）。
///
/// 流れ：最初の 1 枚が置かれると締め切りの秒読みが始まる → 締め切り → 盤が回る → 結果 → 払い戻し → 次の回。
/// 操作：賭ける場所を見て E でチップを 1 枚置く、ホイールでチップの額を変える、R で自分のチップをその場所から下げる（締め切りまで）。
///
/// プレハブ：Assets/_Prefabs/Casino/RouletteTable.prefab（配置ツール Roomies/Casino/2 Build Gimmick Prefabs）
///  ・ルート：NetworkObject・このスクリプト・台の当たり判定
///  ・Visual：台の見た目／Pivot_Wheel：回る盤（中の Visual が見た目、Anchor_Ball が玉）
///  ・Spots/Spot_〜：賭ける場所（RouletteSpot と当たり判定。Anchor_Chips がチップの見た目）
///  ・Anchor_Board：結果・秒読み・これまでの数字の文字
///
/// お金と結果はサーバーだけが決める。賭けた時点で共同口座から引き、当たりは結果のあとにまとめて戻す。
/// </summary>
public class RouletteTable : CasinoGame
{
    private enum Phase { Idle, Betting, Spinning, Result }

    private struct Bet
    {
        public ulong clientId;
        public ulong playerObjectId;
        public int spot;
        public int amount;
    }

    [Header("賭け金")]
    [Tooltip("チップ 1 枚の額（ホイールで切り替える）")]
    [SerializeField] private int[] betAmounts = { 100, 500, 1000 };
    [Tooltip("1 人が 1 回に賭けられる合計の上限")]
    [SerializeField, Min(1)] private int maxBetPerPlayer = 5000;

    [Header("決まり")]
    [Tooltip("0 が出たとき、2 倍の賭け（赤・黒・奇数・偶数・前半・後半）は賭け金を戻す（3 階の台）")]
    [SerializeField] private bool zeroRefundsEvenMoney;

    [Header("時間（秒）")]
    [SerializeField, Min(1f)] private float bettingSeconds = 12f;
    [SerializeField, Min(1f)] private float spinSeconds = 6f;
    [SerializeField, Min(0.5f)] private float resultSeconds = 4f;

    [Header("賭ける場所（置いてある順が番号）")]
    [SerializeField] private RouletteSpot[] spots = new RouletteSpot[0];
    [Tooltip("チップの山の高さ：この額ごとに 1 枚ぶん高くなる")]
    [SerializeField, Min(1)] private int chipUnit = 100;
    [SerializeField, Min(1)] private int chipMaxStack = 12;

    [Header("盤と玉（見た目）")]
    [Tooltip("回る盤の軸（Pivot_Wheel）")]
    [SerializeField] private Transform wheelPivot;
    [Tooltip("玉（Pivot_Wheel の子の Anchor_Ball）")]
    [SerializeField] private Transform ball;
    [SerializeField] private float idleWheelSpeed = 18f;
    [SerializeField] private float spinWheelSpeed = 240f;
    [SerializeField, Min(1)] private int ballTurns = 5;
    [SerializeField] private float ballOuterRadius = 0.36f;
    [SerializeField] private float ballPocketRadius = 0.27f;
    [SerializeField] private float ballOuterHeight = 0.07f;
    [SerializeField] private float ballPocketHeight = 0.025f;

    [Header("文字（置いてある物に書き込むだけ）")]
    [SerializeField] private TextMeshPro resultText;
    [SerializeField] private TextMeshPro statusText;
    [SerializeField] private TextMeshPro historyText;

    private readonly NetworkVariable<int> phase = new((int)Phase.Idle);
    private readonly NetworkVariable<double> phaseEndsAt = new(0);
    private readonly NetworkVariable<double> spinStartedAt = new(0);
    private readonly NetworkVariable<int> resultNumber = new(-1);
    private readonly NetworkVariable<FixedString64Bytes> history = new(new FixedString64Bytes(""));
    private NetworkList<int> spotTotals;          // 場所ごとの、全員の賭けの合計

    private readonly List<Bet> bets = new();                       // サーバーだけ
    private readonly Dictionary<int, int> ownBets = new();          // 自分の賭け（場所 → 額）。自分の画面だけ
    private readonly List<int> recentResults = new();               // サーバーだけ
    private int currentBetIndex;
    private float wheelAngle;
    private float nextTextTime;

    public int[] BetAmounts => betAmounts;
    public bool ZeroRefundsEvenMoney => zeroRefundsEvenMoney;
    public int SpotCount => spots.Length;
    public RouletteSpot SpotAt(int index) => index >= 0 && index < spots.Length ? spots[index] : null;
    public bool IsIdle => phase.Value == (int)Phase.Idle;
    public bool IsBettingOpen => phase.Value == (int)Phase.Idle || phase.Value == (int)Phase.Betting;
    public int LastResult => resultNumber.Value;
    public int CurrentBet => betAmounts[Mathf.Clamp(currentBetIndex, 0, betAmounts.Length - 1)];

    /// <summary>その場所の、全員の賭けの合計。</summary>
    public int TotalOn(int spot) => spotTotals != null && spot >= 0 && spot < spotTotals.Count ? spotTotals[spot] : 0;

    private void Awake()
    {
        spotTotals = new NetworkList<int>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            spotTotals.Clear();
            for (int i = 0; i < spots.Length; i++) spotTotals.Add(0);
        }
        spotTotals.OnListChanged += OnTotalsChanged;
        phase.OnValueChanged += OnPhaseChanged;
        RefreshChips();
        RefreshTexts();
    }

    public override void OnNetworkDespawn()
    {
        if (spotTotals != null) spotTotals.OnListChanged -= OnTotalsChanged;
        phase.OnValueChanged -= OnPhaseChanged;
    }

    // ================================================================ 操作（各クライアント）

    private int SpotIndex(Collider lookedAt)
    {
        if (lookedAt == null) return -1;
        RouletteSpot spot = lookedAt.GetComponentInParent<RouletteSpot>();
        return spot != null ? System.Array.IndexOf(spots, spot) : -1;
    }

    public override string GetLabel(Collider lookedAt, ulong clientId)
    {
        switch ((Phase)phase.Value)
        {
            case Phase.Spinning: return "ルーレットが回っている…";
            case Phase.Result: return "結果　" + RouletteRules.ResultName(resultNumber.Value);
        }

        int index = SpotIndex(lookedAt);
        if (index < 0) return $"賭ける場所を見て {Key(GameAction.Interact)}   {WheelTag} チップ {CurrentBet:N0}R";

        RouletteSpot spot = spots[index];
        ownBets.TryGetValue(index, out int own);
        string label = $"{Key(GameAction.Interact)} 「{RouletteRules.SpotName(spot.kind, spot.number)}」に {CurrentBet:N0}R" +
                       $"（当たると {RouletteRules.WinMultiplier(spot.kind)}倍）   {WheelTag} 額";
        if (own > 0) label += $"   {Key(GameAction.Rotate)} 下げる（置いた {own:N0}R）";
        return label;
    }

    public override void ChangeBet(int direction)
    {
        if (betAmounts == null || betAmounts.Length == 0) return;
        currentBetIndex = (currentBetIndex + direction) % betAmounts.Length;
        if (currentBetIndex < 0) currentBetIndex += betAmounts.Length;
    }

    public override void Interact(Collider lookedAt, PlayerEarning earning)
    {
        if (!IsSpawned || earning == null || !earning.IsSpawned || !IsBettingOpen) return;
        int index = SpotIndex(lookedAt);
        if (index < 0) return;
        PlaceBetServerRpc(earning.NetworkObjectId, index, currentBetIndex);
    }

    public override void Secondary(Collider lookedAt, PlayerEarning earning)
    {
        if (!IsSpawned || !IsBettingOpen) return;
        int index = SpotIndex(lookedAt);
        if (index < 0 || !ownBets.ContainsKey(index)) return;
        RemoveBetServerRpc(index);
    }

    // ================================================================ サーバー

    private int ServerStakeOf(ulong clientId)
    {
        int sum = 0;
        foreach (Bet b in bets) if (b.clientId == clientId) sum += b.amount;
        return sum;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PlaceBetServerRpc(ulong playerObjectId, int spot, int betIndex, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!IsBettingOpen || spot < 0 || spot >= spots.Length) return;
        if (betAmounts == null || betAmounts.Length == 0) return;
        if (!ServerValidatePlayer(playerObjectId, sender, out PlayerEarning earning)) return;

        int amount = Mathf.Max(1, betAmounts[Mathf.Clamp(betIndex, 0, betAmounts.Length - 1)]);
        if (ServerStakeOf(sender) + amount > maxBetPerPlayer)
        {
            Tell(sender, "ルーレット", $"1 回に賭けられるのは ¥{maxBetPerPlayer:N0} まで。");
            return;
        }

        SharedMoneyManager money = SharedMoneyManager.Instance;
        if (money == null || !money.TrySpend(amount, SharedMoneyReason.CasinoBet, $"roulette client={sender}"))
        {
            Tell(sender, "ルーレット", "共同口座のお金が足りない。");
            return;
        }
        earning.SpendEarning(amount);

        // 同じ人の同じ場所の賭けは 1 つにまとめる
        int own = amount;
        int at = bets.FindIndex(b => b.clientId == sender && b.spot == spot);
        if (at >= 0)
        {
            Bet b = bets[at]; b.amount += amount; own = b.amount; bets[at] = b;
        }
        else bets.Add(new Bet { clientId = sender, playerObjectId = playerObjectId, spot = spot, amount = amount });

        spotTotals[spot] = spotTotals[spot] + amount;
        OwnBetRpc(spot, own, RpcTarget.Single(sender, RpcTargetUse.Temp));
        ChipSoundRpc(spot);

        if (phase.Value == (int)Phase.Idle)
        {
            phaseEndsAt.Value = ServerNow + bettingSeconds;
            phase.Value = (int)Phase.Betting;
            StartCoroutine(ServerRound());
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RemoveBetServerRpc(int spot, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!IsBettingOpen) return;

        int at = bets.FindIndex(b => b.clientId == sender && b.spot == spot);
        if (at < 0) return;

        Bet bet = bets[at];
        bets.RemoveAt(at);
        spotTotals[spot] = Mathf.Max(0, spotTotals[spot] - bet.amount);

        // 賭け金をそのまま戻す（賭け金と同じ額を stake に渡すので、「稼ぎ」には数えない）
        SharedMoneyManager money = SharedMoneyManager.Instance;
        if (money != null && money.TryAdd(bet.amount, SharedMoneyReason.CasinoPayout, $"roulette take back client={sender}", bet.amount))
        {
            PlayerEarning earning = ServerFindEarning(bet.playerObjectId);
            if (earning != null) earning.AddEarning(bet.amount);
        }
        OwnBetRpc(spot, 0, RpcTarget.Single(sender, RpcTargetUse.Temp));
    }

    private IEnumerator ServerRound()
    {
        yield return new WaitForSeconds(bettingSeconds);

        if (bets.Count == 0)
        {
            // 全員が下げた：回さずに待ちへ戻る
            phase.Value = (int)Phase.Idle;
            yield break;
        }

        int result = Random.Range(0, RouletteRules.PocketCount);
        resultNumber.Value = result;
        spinStartedAt.Value = ServerNow;
        phaseEndsAt.Value = ServerNow + spinSeconds;
        phase.Value = (int)Phase.Spinning;
        yield return new WaitForSeconds(spinSeconds);

        ServerPayOut(result);
        recentResults.Insert(0, result);
        if (recentResults.Count > 8) recentResults.RemoveAt(recentResults.Count - 1);
        history.Value = new FixedString64Bytes(string.Join("  ", recentResults));
        phaseEndsAt.Value = ServerNow + resultSeconds;
        phase.Value = (int)Phase.Result;
        yield return new WaitForSeconds(resultSeconds);

        bets.Clear();
        for (int i = 0; i < spotTotals.Count; i++)
            if (spotTotals[i] != 0) spotTotals[i] = 0;
        ClearOwnBetsRpc();
        phase.Value = (int)Phase.Idle;
    }

    /// <summary>結果に合わせて、人ごとにまとめて払い戻す。</summary>
    private void ServerPayOut(int result)
    {
        var stakes = new Dictionary<ulong, int>();
        var returns = new Dictionary<ulong, int>();
        var players = new Dictionary<ulong, ulong>();
        foreach (Bet b in bets)
        {
            RouletteSpot spot = spots[b.spot];
            int back = b.amount * RouletteRules.ReturnMultiplier(spot.kind, spot.number, result, zeroRefundsEvenMoney);
            stakes[b.clientId] = (stakes.TryGetValue(b.clientId, out int s) ? s : 0) + b.amount;
            returns[b.clientId] = (returns.TryGetValue(b.clientId, out int r) ? r : 0) + back;
            players[b.clientId] = b.playerObjectId;
        }

        SharedMoneyManager money = SharedMoneyManager.Instance;
        string resultName = RouletteRules.ResultName(result);
        foreach (KeyValuePair<ulong, int> kv in stakes)
        {
            ulong client = kv.Key;
            int stake = kv.Value, back = returns[client];
            if (back > 0 && money != null &&
                money.TryAdd(back, SharedMoneyReason.CasinoPayout, $"roulette {result} client={client}", stake))
            {
                PlayerEarning earning = ServerFindEarning(players[client]);
                if (earning != null) earning.AddEarning(back);
            }

            int net = back - stake;
            if (net > 0) Tell(client, "ルーレット　" + resultName, $"¥{net:N0} の勝ち！", NightEventManager.StyleFun);
            else if (net == 0) Tell(client, "ルーレット　" + resultName, "賭け金が戻った。");
            else Tell(client, "ルーレット　" + resultName, $"¥{-net:N0} の負け……");
            ResultSoundRpc(net > 0, RpcTarget.Single(client, RpcTargetUse.Temp));
        }
    }

    // ================================================================ 見た目と音（各クライアント）

    [Rpc(SendTo.SpecifiedInParams)]
    private void OwnBetRpc(int spot, int amount, RpcParams rpcParams = default)
    {
        if (amount > 0) ownBets[spot] = amount;
        else ownBets.Remove(spot);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void ClearOwnBetsRpc() => ownBets.Clear();

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void ChipSoundRpc(int spot)
    {
        Vector3 at = spot >= 0 && spot < spots.Length && spots[spot] != null ? spots[spot].transform.position : transform.position;
        GameAudio.PlayAt("casino/chip", at, 1f, 0.08f, 10f);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ResultSoundRpc(bool won, RpcParams rpcParams = default)
    {
        GameAudio.PlayAt(won ? "casino/win" : "casino/lose", transform.position);
    }

    private void OnPhaseChanged(int oldValue, int newValue)
    {
        if ((Phase)newValue == Phase.Spinning) GameAudio.PlayAt("casino/roulette_spin", transform.position);
        if ((Phase)newValue == Phase.Result) GameAudio.PlayAt("casino/roulette_stop", transform.position);
        RefreshTexts();
    }

    private void OnTotalsChanged(NetworkListEvent<int> change) => RefreshChips();

    /// <summary>チップの山：賭けのある場所だけ出し、額に合わせて高さを変える。</summary>
    private void RefreshChips()
    {
        for (int i = 0; i < spots.Length; i++)
        {
            RouletteSpot spot = spots[i];
            if (spot == null || spot.chips == null) continue;
            int total = TotalOn(i);
            bool show = total > 0;
            if (spot.chips.gameObject.activeSelf != show) spot.chips.gameObject.SetActive(show);
            if (!show) continue;
            Vector3 s = spot.chips.localScale;
            s.y = Mathf.Clamp(Mathf.CeilToInt(total / (float)chipUnit), 1, chipMaxStack);
            spot.chips.localScale = s;
        }
    }

    private void RefreshTexts()
    {
        var p = (Phase)phase.Value;
        if (resultText != null)
            resultText.text = resultNumber.Value >= 0 && p != Phase.Spinning ? RouletteRules.ResultName(resultNumber.Value) : "？";

        if (statusText != null)
        {
            int left = Mathf.Max(0, Mathf.CeilToInt((float)(phaseEndsAt.Value - ServerNow)));
            switch (p)
            {
                case Phase.Idle: statusText.text = "賭ける場所を見て E"; break;
                case Phase.Betting: statusText.text = $"締め切りまで {left}"; break;
                case Phase.Spinning: statusText.text = "回っています"; break;
                default: statusText.text = "払い戻し"; break;
            }
        }

        if (historyText != null) historyText.text = history.Value.ToString();
    }

    private void Update()
    {
        var p = (Phase)phase.Value;

        // 盤：いつもゆっくり回り、回している間は速く回ってだんだん落ち着く
        float t = p == Phase.Spinning && spinSeconds > 0f
            ? Mathf.Clamp01((float)(ServerNow - spinStartedAt.Value) / spinSeconds)
            : 1f;
        float speed = p == Phase.Spinning ? Mathf.Lerp(spinWheelSpeed, idleWheelSpeed, t * t) : idleWheelSpeed;
        wheelAngle = Mathf.Repeat(wheelAngle + speed * Time.deltaTime, 360f);
        if (wheelPivot != null) wheelPivot.localRotation = Quaternion.Euler(0f, wheelAngle, 0f);

        // 玉：盤から見た角度で動かす。回している間は外側を逆向きに回り、最後に当たりのマスへ落ちる
        if (ball != null)
        {
            int shown = resultNumber.Value >= 0 ? resultNumber.Value : 0;
            float pocketAngle = RouletteRules.PocketIndex(shown) * 360f / RouletteRules.PocketCount;
            float ease = 1f - Mathf.Pow(1f - t, 3f);
            float angle = pocketAngle - ballTurns * 360f * (1f - ease);
            float fall = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 1f, t));
            float radius = Mathf.Lerp(ballOuterRadius, ballPocketRadius, fall);
            float height = Mathf.Lerp(ballOuterHeight, ballPocketHeight, fall)
                           + Mathf.Abs(Mathf.Sin(t * 40f)) * 0.012f * (1f - t) * fall;
            ball.localPosition = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius + Vector3.up * height;
        }

        if (Time.unscaledTime >= nextTextTime)
        {
            nextTextTime = Time.unscaledTime + 0.2f;
            RefreshTexts();
        }
    }
}
