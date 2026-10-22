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
    /// <summary>この形式の版。読み込み時に合わなければ捨てる。</summary>
    public int version = CurrentVersion;
    public const int CurrentVersion = 1;

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
}

/// <summary>参加者の所持金。名前で突き合わせる。</summary>
[System.Serializable]
public class SavedPlayerMoney
{
    public string name = "";
    public int earning;
}
