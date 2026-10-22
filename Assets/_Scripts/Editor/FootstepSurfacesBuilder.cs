#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 足音の材質表 `Resources/Audio/FootstepSurfaces.asset` を作る／行を足すツール。
/// 既にある行の値は残す（調整したピッチ・音量を上書きしない）。
///
/// メニュー：Roomies/Audio/Rebuild Footstep Surfaces
/// </summary>
internal static class FootstepSurfacesBuilder
{
    private const string AssetPath = "Assets/Resources/Audio/FootstepSurfaces.asset";

    // このプロジェクトのマテリアル名（wood / cream / green …）に合わせた既定値。
    // 同じ足音素材のまま、ピッチと音量だけで床の違いを出す。
    private static readonly (string keyword, float pitch, float volume)[] Defaults =
    {
        ("wood",   0.98f, 1.00f),   // 板張りの床・テラス
        ("paper",  1.00f, 0.85f),   // 内装の白い面
        ("cream",  1.02f, 0.95f),   // コンクリートの土間
        ("silver", 1.14f, 1.00f),   // 金属・レール
        ("ink",    1.06f, 0.90f),   // 暗い石・アスファルト
        ("green",  0.90f, 0.70f),   // 芝生・植え込み
        ("blue",   0.94f, 0.80f),   // 水辺・タイル
        ("teal",   1.04f, 0.90f),   // 塗装された床
        ("stone",  1.10f, 1.00f),
        ("rock",   1.08f, 1.00f),
        ("grass",  0.90f, 0.70f),
        ("metal",  1.18f, 1.00f),
        ("carpet", 0.86f, 0.60f),
    };

    [MenuItem("Roomies/Audio/Rebuild Footstep Surfaces")]
    private static void Rebuild()
    {
        System.IO.Directory.CreateDirectory("Assets/Resources/Audio");

        var table = AssetDatabase.LoadAssetAtPath<FootstepSurfaces>(AssetPath);
        bool created = table == null;

        if (created)
        {
            table = ScriptableObject.CreateInstance<FootstepSurfaces>();
            AssetDatabase.CreateAsset(table, AssetPath);
        }

        var existing = (table.entries ?? new FootstepSurfaces.Entry[0])
            .Where(e => e != null && !string.IsNullOrEmpty(e.keyword))
            .ToDictionary(e => e.keyword, e => e);

        var result = new List<FootstepSurfaces.Entry>();
        int added = 0;

        foreach ((string keyword, float pitch, float volume) in Defaults)
        {
            if (existing.TryGetValue(keyword, out FootstepSurfaces.Entry entry))
            {
                result.Add(entry);
                existing.Remove(keyword);
            }
            else
            {
                result.Add(new FootstepSurfaces.Entry
                {
                    keyword = keyword,
                    pitch = pitch,
                    volume = volume,
                });
                added++;
            }
        }

        result.AddRange(existing.Values);   // 手で足した行は残す

        table.entries = result.ToArray();
        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Audio] 足音の材質表を更新しました（{(created ? "新規作成" : "更新")}・追加 {added} 行） → {AssetPath}");
        Selection.activeObject = table;
    }
}
#endif
