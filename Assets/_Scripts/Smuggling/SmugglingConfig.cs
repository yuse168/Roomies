using UnityEngine;

/// <summary>
/// 闇バイト「運び屋」の共通設定。
/// 全クライアントで同じ値を参照する必要があるため static で持つ。
/// （紙袋の中身は index だけを同期して、各クライアントがここから文言を復元する）
/// </summary>
public static class SmugglingConfig
{
    /// <summary>売人に渡し切ったときの報酬。</summary>
    public const int SuccessReward = 500;

    /// <summary>逮捕されたときの罰金。</summary>
    public const int ArrestFine = 500;

    /// <summary>牢屋から出るのに必要な労働回数（仮：作業台をEで叩く回数）。</summary>
    public const int JailLaborCount = 10;

    /// <summary>紙袋の中身（成功後に売人が教えてくれる）。</summary>
    public static readonly string[] BagContents =
    {
        "大量のカブトムシ",
        "お菓子",
        "隠したアダルトな本",
        "使い古しのゲームソフト",
        "誰かの卒業アルバム",
        "賞味期限切れのチーズ",
        "封の開いたトレカ",
        "やたら重い石ころ",
    };

    public static string GetContentName(int index)
    {
        if (BagContents.Length == 0) return "なにか";
        if (index < 0 || index >= BagContents.Length) return "なにか";
        return BagContents[index];
    }

    public static int RandomContentIndex()
    {
        return Random.Range(0, BagContents.Length);
    }
}
