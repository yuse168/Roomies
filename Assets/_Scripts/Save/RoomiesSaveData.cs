using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// セーブデータの中身。ホストの端末にJSONで1ファイル保存する。
///
/// 保存するのは「部屋の進行状況」だけ。
/// 誰が参加しているかは保存せず、続きから始めるときは**1人でも何人でも**始められる。
/// 参加者の所持金は**名前で覚えておき**、同じ名前の人が入ってきたら戻す。
///
/// `JsonUtility` で読み書きするので、フィールドは public、型は単純な物だけにする。
/// </summary>
[System.Serializable]
public class RoomiesSaveData
{
    /// <summary>この形式の版。書き込むときは必ず <see cref="CurrentVersion"/> になる。</summary>
    public int version = CurrentVersion;

    /// <summary>
    /// いまの版。項目を足したら上げる。
    ///  1 … 進行・口座・家賃の上乗せ・家具・参加者の所持金
    ///  2 … 冷蔵庫（食材・故障）と、家具ごとの効果の有効/無効を追加
    /// </summary>
    public const int CurrentVersion = 2;

    /// <summary>
    /// 読み込める一番古い版。これより古い・<see cref="CurrentVersion"/> より新しいセーブは使わない。
    /// 足した項目は「無ければ既定値」で読めるようにしておけば、ここは上げなくてよい
    /// （上げると、その版より前のセーブは全員分が捨てられる）。
    /// </summary>
    public const int MinSupportedVersion = 1;

    /// <summary>この版のセーブを読み込んでよいか。</summary>
    public static bool IsSupportedVersion(int fileVersion) =>
        fileVersion >= MinSupportedVersion && fileVersion <= CurrentVersion;

    /// <summary>保存した時刻（表示用。ISO 8601のUTC）。</summary>
    public string savedAtUtc = "";

    /// <summary>保存したときのホスト名（表示用）。</summary>
    public string hostName = "";

    [Header("進行")]
    public int day = 1;

    /// <summary>0=朝 / 1=夜。</summary>
    public int time;

    /// <summary>そのターンの残り秒。マイナスならターンの頭から始める。</summary>
    public float remainingSeconds = -1f;

    [Header("お金")]
    public int sharedMoney;

    /// <summary>次回家賃への上乗せ額（夜イベントなどの積み上がり）。</summary>
    public int rentSurcharge;

    [Header("家の設備")]
    /// <summary>冷蔵庫に食材が入っているか（次の食費を肩代わりする）。</summary>
    public bool fridgeStocked;

    /// <summary>冷蔵庫が壊れているか（直すまで補充できない）。</summary>
    public bool fridgeBroken;

    [Header("置いてある物")]
    public List<SavedFurniture> furniture = new List<SavedFurniture>();

    public List<SavedPlayerMoney> players = new List<SavedPlayerMoney>();

    /// <summary>セーブ選択画面に出す1行（「Day 2 夜 ／ ¥1,200」）。</summary>
    public string Summary =>
        $"Day {Mathf.Max(1, day)} {(time == 1 ? "夜" : "朝")}　／　共同口座 ¥{sharedMoney:N0}" +
        (furniture.Count > 0 ? $"　／　家具 {furniture.Count}個" : "");
}

/// <summary>置いてある家具1つ。カタログの番号と場所だけ覚える。</summary>
[System.Serializable]
public class SavedFurniture
{
    public int catalogIndex;
    public Vector3 position;

    /// <summary>向き（Y軸の角度）。家具は立っているので他の軸は覚えない。</summary>
    public float yaw;

    /// <summary>
    /// 効果（移動速度・毎朝の収入など）が効いているか。買った夜はまだ false で、翌朝に true になる。
    /// 版1のセーブにはこの項目が無いので、無ければ true（置いてあった家具は効いていた扱い）にする。
    /// </summary>
    public bool effectActive = true;
}

/// <summary>参加者の所持金。名前で突き合わせる。</summary>
[System.Serializable]
public class SavedPlayerMoney
{
    public string name = "";
    public int earning;
}
