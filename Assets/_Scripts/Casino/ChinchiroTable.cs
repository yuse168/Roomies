using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

/// <summary>チンチロの決まり（通信もシーンも使わない計算だけ。テストから直接呼べる）。</summary>
public static class ChinchiroRules
{
    public enum Hand { None, Hifumi, Point, Shigoro, Triple, Pinzoro }

    /// <summary>サイコロ 3 つの役。point は「目」（2 つそろった残りの 1 つ。役が Point のときだけ）。</summary>
    public static Hand Evaluate(int a, int b, int c, out int point)
    {
        point = 0;
        if (a == b && b == c) return a == 1 ? Hand.Pinzoro : Hand.Triple;
        int lo = Mathf.Min(a, Mathf.Min(b, c)), hi = Mathf.Max(a, Mathf.Max(b, c)), mid = a + b + c - lo - hi;
        if (lo == 4 && mid == 5 && hi == 6) return Hand.Shigoro;
        if (lo == 1 && mid == 2 && hi == 3) return Hand.Hifumi;
        if (a == b) { point = c; return Hand.Point; }
        if (a == c) { point = b; return Hand.Point; }
        if (b == c) { point = a; return Hand.Point; }
        return Hand.None;
    }

    public static string HandName(Hand hand, int point)
    {
        switch (hand)
        {
            case Hand.Pinzoro: return "ピンゾロ";
            case Hand.Triple: return "ゾロ目";
            case Hand.Shigoro: return "シゴロ";
            case Hand.Hifumi: return "ヒフミ";
            case Hand.Point: return point + " の目";
            default: return "目なし";
        }
    }
}

/// <summary>
/// チンチロ（3 階）。どんぶりにサイコロ 3 つ。E を押すだけ。1 台につき 1 人ずつ。
///
/// 役が出るまで最大 3 回ふる（3 回とも目なしなら負け）。戻る倍率（賭け金を含む）はインスペクターの表：
///   ピンゾロ（1・1・1）10 倍、ゾロ目 5 倍、シゴロ（4・5・6）3 倍、6 の目 2 倍、5 の目 1.5 倍、4 の目 1.25 倍、それ以外は 0。
/// この表で、戻る割合の期待値は 100.7%（<see cref="ExpectedReturn"/>。テストが見張る）。
///
/// プレハブ：Assets/_Prefabs/Casino/ChinchiroTable.prefab
///  ・ルート：NetworkObject・このスクリプト・台の当たり判定／Visual：台とどんぶり
///  ・Pivot_Die_1〜3：サイコロの軸（中の Visual が見た目。ふるときは軸だけを動かす）／Anchor_Board：文字
/// お金と出目はサーバーだけが決める。
/// </summary>
public class ChinchiroTable : CasinoGame
{
    private enum Phase { Idle, Rolling, Result }

    [Header("賭け金")]
    [SerializeField] private int[] betAmounts = { 500, 2000, 5000 };

    [Header("戻る倍率（賭け金を含む）")]
    [SerializeField, Min(0f)] private float pinzoroReturn = 10f;
    [SerializeField, Min(0f)] private float tripleReturn = 5f;
    [SerializeField, Min(0f)] private float shigoroReturn = 3f;
    [Tooltip("目（2 つそろった残りの 1 つ）ごとの倍率。1 の目から 6 の目の順")]
    [SerializeField] private float[] pointReturns = { 0f, 0f, 0f, 1.25f, 1.5f, 2f };
    [Tooltip("役が出るまでにふれる回数")]
    [SerializeField, Min(1)] private int maxRolls = 3;

    [Header("時間（秒）")]
    [SerializeField, Min(0.3f)] private float rollSeconds = 1.3f;
    [SerializeField, Min(0.5f)] private float resultSeconds = 2.8f;

    [Header("サイコロ（軸を動かすだけ）")]
    [SerializeField] private Transform[] dicePivots = new Transform[0];
    [Tooltip("ふっている間に、サイコロがはねる高さ")]
    [SerializeField] private float hopHeight = 0.12f;

    [Header("文字（置いてある物に書き込むだけ）")]
    [SerializeField] private TextMeshPro resultText;
    [SerializeField] private TextMeshPro statusText;

    private readonly NetworkVariable<int> phase = new((int)Phase.Idle);
    private readonly NetworkVariable<ulong> ownerClientId = new(ulong.MaxValue);
    private readonly NetworkVariable<int> dice = new(0);            // 3 つの出目（百の位・十の位・一の位）。0 = まだ
    private readonly NetworkVariable<int> rollNumber = new(0);      // 何回目か（ふるたびに増える）
    private readonly NetworkVariable<double> rollStartedAt = new(0);
    private readonly NetworkVariable<int> activeBet = new(0);

    private int currentBetIndex;
    private Vector3[] diceRest;
    private Quaternion[] diceShown;
    private int shownRoll = -1;

    public int[] BetAmounts => betAmounts;
    public bool IsIdle => phase.Value == (int)Phase.Idle;
    public int LastDice => dice.Value;
    public int CurrentBet => betAmounts[Mathf.Clamp(currentBetIndex, 0, betAmounts.Length - 1)];

    /// <summary>その役に戻る倍率（賭け金を含む）。</summary>
    public float ReturnFor(ChinchiroRules.Hand hand, int point)
    {
        switch (hand)
        {
            case ChinchiroRules.Hand.Pinzoro: return pinzoroReturn;
            case ChinchiroRules.Hand.Triple: return tripleReturn;
            case ChinchiroRules.Hand.Shigoro: return shigoroReturn;
            case ChinchiroRules.Hand.Point: return point >= 1 && point <= pointReturns.Length ? pointReturns[point - 1] : 0f;
            default: return 0f;
        }
    }

    /// <summary>戻る割合の期待値（1 = 100%）。216 通りの出目と、役が出るまで maxRolls 回ふれることから計算する。</summary>
    public float ExpectedReturn()
    {
        float sum = 0f; int none = 0;
        for (int a = 1; a <= 6; a++) for (int b = 1; b <= 6; b++) for (int c = 1; c <= 6; c++)
        {
            var hand = ChinchiroRules.Evaluate(a, b, c, out int point);
            if (hand == ChinchiroRules.Hand.None) none++;
            else sum += ReturnFor(hand, point);
        }
        float pNone = none / 216f, perRoll = sum / 216f, total = 0f, reach = 1f;
        for (int i = 0; i < maxRolls; i++) { total += reach * perRoll; reach *= pNone; }
        return total;
    }

    [ContextMenu("Log Expected Return")]
    private void LogExpectedReturn() => Debug.Log($"[Chinchiro] {name} 戻る割合の期待値 {ExpectedReturn() * 100f:F1}%", this);

    private void Awake()
    {
        diceRest = new Vector3[dicePivots.Length];
        diceShown = new Quaternion[dicePivots.Length];
        for (int i = 0; i < dicePivots.Length; i++)
        {
            diceRest[i] = dicePivots[i] != null ? dicePivots[i].localPosition : Vector3.zero;
            diceShown[i] = dicePivots[i] != null ? dicePivots[i].localRotation : Quaternion.identity;
        }
    }

    public override void OnNetworkSpawn()
    {
        phase.OnValueChanged += OnChanged; dice.OnValueChanged += OnChanged; rollNumber.OnValueChanged += OnRoll;
        RefreshTexts();
    }

    public override void OnNetworkDespawn()
    {
        phase.OnValueChanged -= OnChanged; dice.OnValueChanged -= OnChanged; rollNumber.OnValueChanged -= OnRoll;
    }

    // ================================================================ 操作（各クライアント）

    public override string GetLabel(Collider lookedAt, ulong clientId)
    {
        if (IsIdle) return $"{Key(GameAction.Interact)} サイコロをふる {CurrentBet:N0}R   {WheelTag} 額";
        return ownerClientId.Value == clientId ? "…" : "ほかの人が遊んでいる";
    }

    public override void ChangeBet(int direction)
    {
        if (!IsIdle || betAmounts == null || betAmounts.Length == 0) return;
        currentBetIndex = (currentBetIndex + direction) % betAmounts.Length;
        if (currentBetIndex < 0) currentBetIndex += betAmounts.Length;
        RefreshTexts();
    }

    public override void Interact(Collider lookedAt, PlayerEarning earning)
    {
        if (!IsSpawned || earning == null || !earning.IsSpawned || !IsIdle) return;
        RollServerRpc(earning.NetworkObjectId, currentBetIndex);
    }

    // ================================================================ サーバー

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RollServerRpc(ulong playerObjectId, int betIndex, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (phase.Value != (int)Phase.Idle || betAmounts == null || betAmounts.Length == 0) return;
        if (!ServerValidatePlayer(playerObjectId, sender, out PlayerEarning earning)) return;

        int bet = Mathf.Max(1, betAmounts[Mathf.Clamp(betIndex, 0, betAmounts.Length - 1)]);
        SharedMoneyManager money = SharedMoneyManager.Instance;
        if (money == null || !money.TrySpend(bet, SharedMoneyReason.CasinoBet, $"chinchiro client={sender}"))
        {
            Tell(sender, "チンチロ", "共同口座のお金が足りない。");
            return;
        }
        earning.SpendEarning(bet);

        ownerClientId.Value = sender;
        activeBet.Value = bet;
        phase.Value = (int)Phase.Rolling;
        StartCoroutine(ServerPlay(sender, playerObjectId, bet));
    }

    private IEnumerator ServerPlay(ulong client, ulong playerObjectId, int bet)
    {
        var hand = ChinchiroRules.Hand.None; int point = 0;
        for (int i = 0; i < maxRolls && hand == ChinchiroRules.Hand.None; i++)
        {
            int a = Random.Range(1, 7), b = Random.Range(1, 7), c = Random.Range(1, 7);
            dice.Value = a * 100 + b * 10 + c;
            rollStartedAt.Value = ServerNow;
            rollNumber.Value = rollNumber.Value + 1;
            yield return new WaitForSeconds(rollSeconds + 0.5f);
            hand = ChinchiroRules.Evaluate(a, b, c, out point);
        }

        int back = Mathf.RoundToInt(bet * ReturnFor(hand, point));
        SharedMoneyManager money = SharedMoneyManager.Instance;
        if (back > 0 && money != null && money.TryAdd(back, SharedMoneyReason.CasinoPayout, $"chinchiro client={client}", bet))
        {
            PlayerEarning earning = ServerFindEarning(playerObjectId);
            if (earning != null) earning.AddEarning(back);
        }
        string name = ChinchiroRules.HandName(hand, point);
        int net = back - bet;
        if (net > 0) Tell(client, "チンチロ　" + name, $"¥{net:N0} の勝ち！", NightEventManager.StyleFun);
        else if (net == 0) Tell(client, "チンチロ　" + name, "賭け金が戻った。");
        else Tell(client, "チンチロ　" + name, $"¥{-net:N0} の負け……");
        ResultSoundRpc(net > 0);

        phase.Value = (int)Phase.Result;
        yield return new WaitForSeconds(resultSeconds);
        ownerClientId.Value = ulong.MaxValue;
        activeBet.Value = 0;
        phase.Value = (int)Phase.Idle;
    }

    // ================================================================ 見た目と音（各クライアント）

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void ResultSoundRpc(bool won) => GameAudio.PlayAt(won ? "casino/win" : "casino/lose", transform.position);

    private void OnChanged(int oldValue, int newValue) => RefreshTexts();

    private void OnRoll(int oldValue, int newValue) => GameAudio.PlayAt("casino/dice", transform.position, 1f, 0.06f);

    /// <summary>その目が上を向く向き（サイコロの形は 上=1・下=6・前(+Z)=2・後ろ=5・右(+X)=3・左=4）。</summary>
    private static Quaternion FaceUp(int value)
    {
        switch (value)
        {
            case 2: return Quaternion.Euler(-90, 0, 0);
            case 3: return Quaternion.Euler(0, 0, 90);
            case 4: return Quaternion.Euler(0, 0, -90);
            case 5: return Quaternion.Euler(90, 0, 0);
            case 6: return Quaternion.Euler(180, 0, 0);
            default: return Quaternion.identity;
        }
    }

    private void Update()
    {
        if (dicePivots.Length == 0 || dice.Value <= 0) return;

        float t = rollSeconds > 0f ? Mathf.Clamp01((float)(ServerNow - rollStartedAt.Value) / rollSeconds) : 1f;
        bool settled = t >= 1f;
        if (settled && shownRoll == rollNumber.Value) return;

        int[] values = { dice.Value / 100, dice.Value / 10 % 10, dice.Value % 10 };
        for (int i = 0; i < dicePivots.Length && i < 3; i++)
        {
            if (dicePivots[i] == null) continue;
            if (settled)
            {
                // 止まった向き：目を上にして、少しずつ違う向きに回しておく
                dicePivots[i].localRotation = Quaternion.Euler(0, (rollNumber.Value * 53 + i * 97) % 360, 0) * FaceUp(values[i]);
                dicePivots[i].localPosition = diceRest[i];
            }
            else
            {
                float spin = (1f - t) * 900f + i * 120f;
                dicePivots[i].localRotation = Quaternion.Euler(spin * 1.3f, spin, spin * .7f);
                dicePivots[i].localPosition = diceRest[i] + Vector3.up * (Mathf.Abs(Mathf.Sin(t * Mathf.PI * 3f + i)) * hopHeight * (1f - t));
            }
        }
        if (settled) { shownRoll = rollNumber.Value; RefreshTexts(); }
    }

    private void RefreshTexts()
    {
        var p = (Phase)phase.Value;
        if (resultText != null)
        {
            if (dice.Value <= 0 || (p == Phase.Rolling && shownRoll != rollNumber.Value)) resultText.text = p == Phase.Idle ? "チンチロ" : "…";
            else
            {
                int a = dice.Value / 100, b = dice.Value / 10 % 10, c = dice.Value % 10;
                var hand = ChinchiroRules.Evaluate(a, b, c, out int point);
                resultText.text = $"{a}・{b}・{c}　{ChinchiroRules.HandName(hand, point)}";
            }
        }
        if (statusText != null)
            statusText.text = p == Phase.Idle ? $"賭け金 {CurrentBet:N0}R" : $"賭け金 {activeBet.Value:N0}R";
    }
}
