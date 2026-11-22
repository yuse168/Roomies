using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// セーブの読み書き。**ホストの端末だけ**が使う。
///
/// 置き場所は `Application.persistentDataPath/roomies_save.json`
/// （Windowsなら `%USERPROFILE%/AppData/LocalLow/&lt;会社名&gt;/Roomies/`）。
/// 1枠だけで、上書きしていく（オートセーブ）。
/// 上書きのたびに、ひとつ前のセーブを `roomies_save.json.bak` に残す。
/// 本体が読めないとき（保存中の電源断・手で編集して壊した等）は、この控えから読む。
///
/// 流れ：
///  1. ロビーでホストが「続きから」を選ぶと <see cref="PendingLoad"/> に読み込んだデータが入る
///  2. GameRoomシーンの <see cref="RunSaveManager"/> がそれを部屋へ反映して、PendingLoadを空にする
///  3. 以後は進行が変わるたびに <see cref="RunSaveManager"/> が上書き保存する
///
/// 参加者が1人でも5人でも同じように始められる（セーブは部屋の状態だけを持つ）。
/// </summary>
public static class SaveSystem
{
    private const string FileName = "roomies_save.json";
    private const string BackupSuffix = ".bak";
    private const string TempSuffix = ".tmp";

    private static string overrideFilePath;

    /// <summary>セーブファイルの場所。</summary>
    public static string FilePath =>
        !string.IsNullOrEmpty(overrideFilePath)
            ? overrideFilePath
            : Path.Combine(Application.persistentDataPath, FileName);

    /// <summary>ひとつ前のセーブ（控え）の場所。</summary>
    public static string BackupFilePath => FilePath + BackupSuffix;

    /// <summary>
    /// 自動テスト用。置き場所を一時フォルダへ向ける（null で元に戻す）。
    /// 本物のセーブをテストで上書きしないためのもので、ゲーム本編からは呼ばない。
    /// </summary>
    public static void SetFilePathOverrideForTests(string path) => overrideFilePath = path;

    /// <summary>
    /// ロビーで「続きから」を選んだときに入る。GameRoomで反映したら空に戻す。
    /// シーンをまたいで渡したいだけなので static で持つ。
    /// </summary>
    public static RoomiesSaveData PendingLoad { get; private set; }

    public static void SetPendingLoad(RoomiesSaveData data) => PendingLoad = data;
    public static void ClearPendingLoad() => PendingLoad = null;

    /// <summary>セーブ（本体か控え）のファイルがあるか。読めるかどうかは <see cref="Load"/> で分かる。</summary>
    public static bool Exists()
    {
        try
        {
            return File.Exists(FilePath) || File.Exists(BackupFilePath);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Save] 存在確認に失敗: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 読み込む。無い・壊れている・版が合わないときは null（呼ぶ側は「はじめから」にする）。
    /// 本体が読めなければ、ひとつ前の控えを試す。例外は外へ出さない。
    /// </summary>
    public static RoomiesSaveData Load()
    {
        RoomiesSaveData data = LoadFile(FilePath);
        if (data != null) return data;

        data = LoadFile(BackupFilePath);
        if (data != null) Debug.LogWarning("[Save] 本体が読めなかったので、ひとつ前のセーブから読みました");
        return data;
    }

    private static RoomiesSaveData LoadFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            return FromJson(File.ReadAllText(path));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Save] セーブの読み込みに失敗: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// JSONからセーブを組み立てる。読めない・版が合わないときは null。
    /// 手で編集されたJSONでも落ちないよう、おかしな値は安全な値へ直す。
    /// </summary>
    public static RoomiesSaveData FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("[Save] セーブを読めませんでした（中身が空）");
            return null;
        }

        RoomiesSaveData data;
        try
        {
            data = JsonUtility.FromJson<RoomiesSaveData>(json);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Save] セーブを読めませんでした（JSONが壊れている）: {e.Message}");
            return null;
        }

        if (data == null)
        {
            Debug.LogWarning("[Save] セーブを読めませんでした（中身が空）");
            return null;
        }

        if (!RoomiesSaveData.IsSupportedVersion(data.version))
        {
            Debug.LogWarning(
                $"[Save] セーブの版が合わないので使いません（file={data.version} / " +
                $"読める範囲={RoomiesSaveData.MinSupportedVersion}〜{RoomiesSaveData.CurrentVersion}）");
            return null;
        }

        Sanitize(data);
        return data;
    }

    /// <summary>保存する形のJSONにする（版と保存時刻を入れる）。</summary>
    public static string ToJson(RoomiesSaveData data)
    {
        data.version = RoomiesSaveData.CurrentVersion;
        data.savedAtUtc = System.DateTime.UtcNow.ToString("o");
        return JsonUtility.ToJson(data, true);
    }

    private static void Sanitize(RoomiesSaveData data)
    {
        if (data.furniture == null) data.furniture = new List<SavedFurniture>();
        if (data.players == null) data.players = new List<SavedPlayerMoney>();
        if (data.savedAtUtc == null) data.savedAtUtc = "";
        if (data.hostName == null) data.hostName = "";

        data.day = Mathf.Max(1, data.day);
        data.time = data.time == 1 ? 1 : 0;
        if (!float.IsFinite(data.remainingSeconds)) data.remainingSeconds = -1f;

        data.furniture.RemoveAll(f =>
            f == null || f.catalogIndex < 0 ||
            !float.IsFinite(f.position.x) || !float.IsFinite(f.position.y) || !float.IsFinite(f.position.z) ||
            !float.IsFinite(f.yaw));

        data.players.RemoveAll(p => p == null || string.IsNullOrEmpty(p.name));
    }

    /// <summary>
    /// 書き込む。いったん一時ファイルへ書いてから置き換えるので、
    /// 保存中に落ちても前のセーブは壊れない。置き換え前の中身は控え（.bak）に残る。
    /// </summary>
    public static bool Write(RoomiesSaveData data)
    {
        if (data == null) return false;

        try
        {
            string path = FilePath;
            string temp = path + TempSuffix;
            string backup = BackupFilePath;

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(temp, ToJson(data));

            if (File.Exists(path))
            {
                // 置き換えと控えの更新を一度に行う（途中で落ちても、本体か控えのどちらかは残る）
                File.Replace(temp, path, backup, true);
            }
            else
            {
                File.Move(temp, path);
            }
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Save] セーブの書き込みに失敗: {e.Message}");
            return false;
        }
    }

    /// <summary>セーブを消す（控えと書きかけも一緒に）。</summary>
    public static bool Delete()
    {
        try
        {
            string path = FilePath;
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + BackupSuffix)) File.Delete(path + BackupSuffix);
            if (File.Exists(path + TempSuffix)) File.Delete(path + TempSuffix);
            ClearPendingLoad();
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Save] セーブの削除に失敗: {e.Message}");
            return false;
        }
    }
}
