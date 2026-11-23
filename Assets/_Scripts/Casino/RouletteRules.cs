using System.Collections.Generic;

/// <summary>ルーレットで賭けられる場所の種類。</summary>
public enum RouletteBetKind : byte
{
    Number,     // 数字 1 つ（0〜36）
    Red,
    Black,
    Odd,
    Even,
    Low,        // 1〜18
    High,       // 19〜36
    Dozen1,     // 1〜12
    Dozen2,     // 13〜24
    Dozen3,     // 25〜36
    Column1,    // 列：1・4・7…34（台の絵の「2:1」の、いちばん手前）
    Column2,    // 列：2・5・8…35
    Column3     // 列：3・6・9…36
}

/// <summary>
/// ルーレットの決まり（0 が 1 つの 37 マス）。通信もシーンも使わない計算だけ。テストから直接呼べる。
///
/// 戻る倍率（賭け金を含む）：数字 36 倍、1〜12 などの 3 つ分けと列（2:1）3 倍、赤・黒・奇数・偶数・前半・後半 2 倍。
/// どれも戻る割合は 97.3%。3 階の台は「0 が出たら、2 倍の賭け（赤・黒など）は賭け金が戻る」ので、その賭けだけ 100% になる。
/// </summary>
public static class RouletteRules
{
    public const int PocketCount = 37;

    /// <summary>盤の上の数字の並び（時計回り）。見た目と玉の止まる位置に使う。</summary>
    public static readonly int[] WheelOrder =
    {
        0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13, 36, 11, 30, 8, 23, 10,
        5, 24, 16, 33, 1, 20, 14, 31, 9, 22, 18, 29, 7, 28, 12, 35, 3, 26
    };

    private static readonly HashSet<int> Reds = new HashSet<int>
    {
        1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36
    };

    public static bool IsRed(int number) => Reds.Contains(number);

    /// <summary>盤の上で、その数字が何番目のマスか（0 始まり）。</summary>
    public static int PocketIndex(int number)
    {
        for (int i = 0; i < WheelOrder.Length; i++)
            if (WheelOrder[i] == number) return i;
        return 0;
    }

    /// <summary>2 倍の賭け（赤・黒・奇数・偶数・前半・後半）か。</summary>
    public static bool IsEvenMoney(RouletteBetKind kind) =>
        kind == RouletteBetKind.Red || kind == RouletteBetKind.Black ||
        kind == RouletteBetKind.Odd || kind == RouletteBetKind.Even ||
        kind == RouletteBetKind.Low || kind == RouletteBetKind.High;

    /// <summary>
    /// その賭けに戻る倍率（賭け金を含む。外れは 0、賭け金が戻るだけなら 1）。
    /// </summary>
    public static int ReturnMultiplier(RouletteBetKind kind, int number, int result, bool zeroRefundsEvenMoney)
    {
        if (kind == RouletteBetKind.Number) return number == result ? 36 : 0;
        if (result == 0) return zeroRefundsEvenMoney && IsEvenMoney(kind) ? 1 : 0;

        switch (kind)
        {
            case RouletteBetKind.Red: return IsRed(result) ? 2 : 0;
            case RouletteBetKind.Black: return IsRed(result) ? 0 : 2;
            case RouletteBetKind.Odd: return result % 2 == 1 ? 2 : 0;
            case RouletteBetKind.Even: return result % 2 == 0 ? 2 : 0;
            case RouletteBetKind.Low: return result <= 18 ? 2 : 0;
            case RouletteBetKind.High: return result >= 19 ? 2 : 0;
            case RouletteBetKind.Dozen1: return result <= 12 ? 3 : 0;
            case RouletteBetKind.Dozen2: return result >= 13 && result <= 24 ? 3 : 0;
            case RouletteBetKind.Dozen3: return result >= 25 ? 3 : 0;
            case RouletteBetKind.Column1: return result % 3 == 1 ? 3 : 0;
            case RouletteBetKind.Column2: return result % 3 == 2 ? 3 : 0;
            case RouletteBetKind.Column3: return result % 3 == 0 ? 3 : 0;
            default: return 0;
        }
    }

    /// <summary>その賭けの戻る割合の期待値（1 = 100%）。</summary>
    public static float ExpectedReturn(RouletteBetKind kind, bool zeroRefundsEvenMoney)
    {
        int total = 0;
        for (int result = 0; result < PocketCount; result++)
            total += ReturnMultiplier(kind, 17, result, zeroRefundsEvenMoney);
        return total / (float)PocketCount;
    }

    public static string ResultName(int number)
    {
        if (number < 0) return "-";
        return number == 0 ? "0 緑" : number + (IsRed(number) ? " 赤" : " 黒");
    }

    public static string SpotName(RouletteBetKind kind, int number)
    {
        switch (kind)
        {
            case RouletteBetKind.Number: return number.ToString();
            case RouletteBetKind.Red: return "赤";
            case RouletteBetKind.Black: return "黒";
            case RouletteBetKind.Odd: return "奇数";
            case RouletteBetKind.Even: return "偶数";
            case RouletteBetKind.Low: return "1〜18";
            case RouletteBetKind.High: return "19〜36";
            case RouletteBetKind.Dozen1: return "1〜12";
            case RouletteBetKind.Dozen2: return "13〜24";
            case RouletteBetKind.Dozen3: return "25〜36";
            case RouletteBetKind.Column1: return "1・4・7…の列";
            case RouletteBetKind.Column2: return "2・5・8…の列";
            case RouletteBetKind.Column3: return "3・6・9…の列";
            default: return "";
        }
    }

    /// <summary>当たったときに戻る倍率（案内に出す）。</summary>
    public static int WinMultiplier(RouletteBetKind kind) =>
        kind == RouletteBetKind.Number ? 36 : IsEvenMoney(kind) ? 2 : 3;
}
