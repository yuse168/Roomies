using System.IO;
using UnityEngine;

/// <summary>
/// セーブの読み書き。**ホストの端末だけ**が使う。
///
/// 置き場所は `Application.persistentDataPath/roomies_save.json`
/// （Windowsなら `%USERPROFILE%/AppData/LocalLow/&lt;会社名&gt;/Roomies/`）。
/// 1枠だけで、上書きしていく（オートセーブ）。
///
/// 流れ：
///  1. ロビーでホストが「続きから」を選ぶと <see cref="PendingLoad"/> に読み込んだデータが入る
///  2. GameRoomシーンの <see cref="RunSaveManager"/> がそれを部屋へ反映して、PendingLoadを空にする
///  3. 以後はターンが変わるたびに <see cref="RunSaveManager"/> が上書き保存する
///
/// 参加者が1人でも5人でも同じように始められる（セーブは部屋の状態だけを持つ）。
/// </summary>
public static class SaveSystem
{
    private const string FileName = "roomies_save.json";

    /// <summary>セーブファイルの場所。</summary>
    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    /// <summary>
    /// ロビーで「続きから」を選んだときに入る。GameRoomで反映したら空に戻す。
    /// シーンをまたいで渡したいだけなので static で持つ。
    /// </summary>
    public static RoomiesSaveData PendingLoad { get; private set; }

    public static void SetPendingLoad(RoomiesSaveData data) => PendingLoad = data;
    public static void ClearPendingLoad() => PendingLoad = null;

    /// <summary>セーブがあるか。</summary>
    public static bool Exists()
    {
        try
        {
            return File.Exists(FilePath);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Save] 存在確認に失敗: {e.Message}");
            return false;
        }
    }

    /// <summary>読み込む。無い・壊れている・版が違うときは null。</summary>
    public static RoomiesSaveData Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;

            string json = File.ReadAllText(FilePath);
            RoomiesSaveData data = JsonUtility.FromJson<RoomiesSaveData>(json);

            if (data == null)
            {
                Debug.LogWarning("[Save] セーブを読めませんでした（中身が空）");
                return null;
            }

            if (data.version != RoomiesSaveData.CurrentVersion)
            {
                Debug.LogWarning(
                    $"[Save] セーブの版が違うので使いません（file={data.version} / now={RoomiesSaveData.CurrentVersion}）");
                return null;
            }

            // null対策（手で編集されたJSONでも落ちないように）
            if (data.furniture == null) data.furniture = new System.Collections.Generic.List<SavedFurniture>();
            if (data.players == null) data.players = new System.Collections.Generic.List<SavedPlayerMoney>();

            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Save] セーブの読み込みに失敗: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 書き込む。いったん一時ファイルへ書いてから置き換えるので、
    /// 保存中に落ちても前のセーブは壊れない。
    /// </summary>
    public static bool Write(RoomiesSaveData data)
    {
        if (data == null) return false;

        try
        {
            data.version = RoomiesSaveData.CurrentVersion;
            data.savedAtUtc = System.DateTime.UtcNow.ToString("o");

            string path = FilePath;
            string temp = path + ".tmp";

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));

            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Save] セーブの書き込みに失敗: {e.Message}");
            return false;
        }
    }

    /// <summary>セーブを消す。</summary>
    public static bool Delete()
    {
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
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
