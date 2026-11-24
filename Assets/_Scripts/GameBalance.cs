using UnityEngine;

/// <summary>
/// ゲーム全体の数値をまとめた表。
///
/// 規約（AGENTS.md「実行時に作らない」＝数値はインスペクターで触れるようにする）：
///  ・家賃・罰金・報酬・夜イベントの倍率をコードに直書きしない。
///    `Assets/Resources/Balance/GameBalance.asset` を開いて調整する。
///  ・メニュー Roomies/Balance/Rebuild Game Balance で作り直せる（既存の値は残る）。
///
/// 全クライアントが同じ値を見る必要があるので、Resources から読む1つだけの資産にしている。
/// 資産が無くてもゲームは動く（このクラスの既定値が使われる）。
/// </summary>
public class GameBalance : ScriptableObject
{
    private const string ResourcePath = "Balance/GameBalance";

    private static GameBalance instance;
    private static bool loaded;

    /// <summary>読み込み済みの表。無ければ既定値のインスタンス。</summary>
    public static GameBalance Current
    {
        get
        {
            if (!loaded)
            {
                instance = Resources.Load<GameBalance>(ResourcePath);
                if (instance == null) instance = CreateInstance<GameBalance>();
                loaded = true;
            }
            return instance;
        }
    }

    /// <summary>資産を差し替えた直後に読み直す（Editor用）。</summary>
    public static void Reload()
    {
        instance = null;
        loaded = false;
    }

    [Header("家賃")]
    [Tooltip("1回目の家賃")]
    [Min(0)] public int baseRent = 500;

    [Tooltip("家賃を1回払うたびに、次回の家賃へ上乗せする額（難易度上昇）")]
    [Min(0)] public int rentIncreasePerCycle = 150;

    [Tooltip("家賃がこれ以上は上がらない上限")]
    [Min(0)] public int maxRent = 2000;

    [Header("夜イベント")]
    [Tooltip("Dayが1増えるごとに金額へ足す割合（0.3なら1日ごとに+30%）")]
    [Range(0f, 1f)] public float nightAmountScalePerDay = 0.3f;

    [Tooltip("夜イベントの金額倍率の上限")]
    [Range(1f, 5f)] public float nightAmountScaleMax = 2.5f;

    [Tooltip("Day1の「何も起きない夜」の重み。Dayが進むと減る")]
    [Min(0)] public int peaceWeightDay1 = 25;

    [Tooltip("Dayが1増えるごとに「何も起きない夜」の重みから引く量")]
    [Min(0)] public int peaceWeightDropPerDay = 7;

    [Header("闇バイト（運び屋）")]
    [Tooltip("売人に渡し切ったときの報酬")]
    [Min(0)] public int smugglingReward = 500;

    [Tooltip("逮捕されたときの罰金")]
    [Min(0)] public int arrestFine = 500;

    [Tooltip("牢屋から出るのに必要な労働回数")]
    [Min(1)] public int jailLaborCount = 10;

    [Header("荷物の値段（割れ物）")]
    [Tooltip("ふつうの箱の最初の値段")]
    [Min(0)] public int normalBoxValue = 100;

    [Tooltip("レア（割れ物）の箱の最初の値段")]
    [Min(0)] public int rareBoxValue = 300;

    [Tooltip("この速さ（m/s）を超えてぶつかったときだけ値段が下がる。そっと置く・歩いて運ぶぶんには下がらない")]
    [Min(0.1f)] public float boxDamageSpeed = 3f;

    [Tooltip("しきい値を1m/s超えるごとに、最初の値段の何割が減るか")]
    [Range(0f, 1f)] public float boxDamagePerSpeed = 0.1f;

    [Tooltip("ふつうの箱の壊れやすさ（減る量に掛ける）")]
    [Min(0f)] public float normalBoxFragility = 0.6f;

    [Tooltip("レア（割れ物）の箱の壊れやすさ（減る量に掛ける）")]
    [Min(0f)] public float rareBoxFragility = 1.5f;

    [Tooltip("1回ぶつかって値段が下がったあと、次に下がるまでの秒数（転がって何度も減るのを防ぐ）")]
    [Min(0f)] public float boxDamageCooldown = 0.3f;

    [Header("配達トラック")]
    [Tooltip("朝の残りがこの秒数になったとき、全員に発車を知らせる（大きい順）")]
    public int[] truckWarnSeconds = { 60, 30, 10 };

    [Tooltip("発車のとき、トラックからこの距離（m）以内にいる人が「乗った」ことになる")]
    [Min(0.5f)] public float truckBoardRadius = 6f;

    [Tooltip("町に出ている荷物の上限（荷台の中は数えない）。荷台に積むと、この数まで集荷場に補充される")]
    [Min(1)] public int deliveryLooseBoxes = 4;

    [Tooltip("町に出ている荷物と荷台の荷物を合わせた上限（物が増えすぎないように）")]
    [Min(1)] public int deliveryMaxBoxes = 16;

    [Header("カジノ")]
    [Tooltip("2 階へ上がる鍵の値段（1 階の店の人から買う。買い切り）")]
    [Min(0)] public int casinoKeyPrice2F = 3000;

    [Tooltip("3 階へ上がる鍵の値段（2 階の店の人から買う。買い切り）")]
    [Min(0)] public int casinoKeyPrice3F = 10000;

    [Header("コンビニのレジ打ち（朝だけのバイト）")]
    [Tooltip("1 回のバイトで来るお客さんの数")]
    [Min(1)] public int storeJobCustomers = 5;

    [Tooltip("お客さん 1 人のお会計を最後までやったときのバイト代")]
    [Min(0)] public int storeJobPayPerCustomer = 30;

    [Tooltip("速さのボーナス（品物を置いてから袋を渡すまでが storeJobFastSeconds 以内のとき）")]
    [Min(0)] public int storeJobFastBonus = 15;

    [Tooltip("速さのボーナスがつく秒数")]
    [Min(1f)] public float storeJobFastSeconds = 10f;

    [Tooltip("全員ミスなしで終えたときのボーナス")]
    [Min(0)] public int storeJobPerfectBonus = 30;

    [Tooltip("お客さんが待てる秒数（品物を置いてから。過ぎると帰ってしまい、その人の分は 0）")]
    [Min(5f)] public float storeJobPatienceSeconds = 25f;

    /// <summary>その階（2 か 3）へ上がる鍵の値段。</summary>
    public int CasinoKeyPrice(int floor) => floor <= 2 ? casinoKeyPrice2F : casinoKeyPrice3F;

    /// <summary>
    /// ぶつかった速さから、減る額を返す（しきい値以下なら 0）。
    /// </summary>
    public int BoxDamage(int startValue, float fragility, float impactSpeed)
    {
        float over = impactSpeed - boxDamageSpeed;
        if (over <= 0f || startValue <= 0) return 0;
        return Mathf.Max(1, Mathf.RoundToInt(startValue * boxDamagePerSpeed * fragility * over));
    }

    /// <summary>
    /// 何周目の家賃か（1始まり）に対する請求額。
    /// 1周目は `baseRent`、以降は `rentIncreasePerCycle` ずつ上がる（`maxRent` 止まり）。
    /// </summary>
    public int RentForCycle(int cycle)
    {
        int index = Mathf.Max(0, cycle - 1);
        long amount = baseRent + (long)rentIncreasePerCycle * index;
        return (int)Mathf.Min(amount, maxRent);
    }

    /// <summary>夜イベントの金額倍率（Dayが進むほど上がる）。</summary>
    public float NightScale(int day)
    {
        float scale = 1f + nightAmountScalePerDay * Mathf.Max(0, day - 1);
        return Mathf.Min(scale, nightAmountScaleMax);
    }

    /// <summary>その日の「何も起きない夜」の重み。</summary>
    public int PeaceWeight(int day)
    {
        return Mathf.Max(6, peaceWeightDay1 - peaceWeightDropPerDay * Mathf.Max(0, day - 1));
    }
}
