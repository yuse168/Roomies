#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 7 遠くの霧（2026/11/14）：GameRoom のライティング設定に、遠くを空の色に溶かす直線の霧を入れる。何度実行しても同じ結果。
/// ・霧の色は朝と夜で RoomTimeOfDayLighting（Roomies_TimeOfDay）が切り替える。下の表の色をその部品に書き、シーンには夜の色で保存する（シーンは夜で保存されている）。
/// ・始まりと終わりの距離・色は下の表。あとはインスペクター（Roomies_TimeOfDay の霧の色、ライティング設定の距離）で直してよい。タイトル（MainMenuSteam）は触らない。
/// </summary>
internal static partial class RoomiesMaterialFinishBuilder
{
    const float FogStart = 80f, FogEnd = 700f;   // 山（300〜800m）が白く平らにならないよう、終わりは遠め
    static readonly Color FogMorning = new Color(.62f, .74f, .88f), FogNight = new Color(.04f, .05f, .12f);

    [MenuItem("Roomies/Art/Restyle/Material Finish/7 Distance Fog")]
    internal static void BuildDistanceFog()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.GetRootGameObjects().All(g => g.GetComponentInChildren<DayManager>(true) == null))
        {
            Debug.LogWarning("[Fog] GameRoom をアクティブなシーンにして実行してください: " + scene.name);
            return;
        }
        var tod = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<RoomTimeOfDayLighting>(true)).FirstOrDefault(t => t != null);
        if (tod != null)
        {
            var so = new SerializedObject(tod);
            so.FindProperty("morningFogColor").colorValue = FogMorning;
            so.FindProperty("nightFogColor").colorValue = FogNight;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = FogStart;
        RenderSettings.fogEndDistance = FogEnd;
        RenderSettings.fogColor = FogNight;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Fog] {scene.name}: linear {FogStart}-{FogEnd}");
    }

    /// <summary>Play Mode 中：今の霧の設定と朝／夜を Temp/RoomiesTown/fog.txt に1行足す（確認用）。</summary>
    [MenuItem("Roomies/Art/Restyle/Material Finish/Fog Check (Play Mode)")]
    internal static void FogCheck()
    {
        var day = DayManager.Instance;
        string line = $"{System.DateTime.Now:HH:mm:ss} playing {EditorApplication.isPlaying} night {(day != null ? day.IsNight.ToString() : "no DayManager")} " +
                      $"fog {RenderSettings.fog} {RenderSettings.fogMode} {RenderSettings.fogStartDistance}-{RenderSettings.fogEndDistance} color {RenderSettings.fogColor}";
        Directory.CreateDirectory("Temp/RoomiesTown");
        File.AppendAllText("Temp/RoomiesTown/fog.txt", line + "\n");
    }

    /// <summary>Play Mode 中（ホスト）：朝なら夜へ、夜なら朝へ進める（DayManager の時間切れと同じ処理。霧の切り替えの確認用）。</summary>
    [MenuItem("Roomies/Art/Restyle/Material Finish/Fog Check - Advance Time (Play Mode)")]
    internal static void FogAdvanceTime()
    {
        if (!EditorApplication.isPlaying || DayManager.Instance == null) return;
        typeof(DayManager).GetMethod("AdvanceTurnServer", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(DayManager.Instance, null);
    }
}
#endif
