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
