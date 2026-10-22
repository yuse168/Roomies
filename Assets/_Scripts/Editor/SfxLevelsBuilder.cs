#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 効果音の音量テーブル `Resources/Audio/SfxLevels.asset` を作る／行を足すツール。
///
/// ・既にある行の値は残す（調整した音量を上書きしない）。
/// ・まだ無いキーだけ既定値で追加する。
/// ・音量の調整はこのツールではなく、できたアセットをインスペクターで開いて行う。
///
/// メニュー：Roomies/Audio/Rebuild Sfx Levels
/// </summary>
internal static class SfxLevelsBuilder
{
    private const string AssetPath = "Assets/Resources/Audio/SfxLevels.asset";

    // 既定値。2026/10/22時点の実測（新しい音源が既存より約10dB大きい）に合わせた目安。
    private static readonly (string key, float volume)[] Defaults =
    {
        ("player/footstep", 0.55f),
        ("player/jump", 0.70f),
        ("player/land", 0.70f),

        ("ui/confirm", 0.50f),
        ("ui/cancel", 0.50f),
        ("ui/emote", 0.50f),
        ("ui/logo", 0.70f),

        ("money/gain", 1.00f),
        ("money/spend", 1.00f),

        ("day/morning", 0.60f),
        ("day/night", 0.60f),
        ("day/gameover", 0.70f),

        ("night/trouble", 0.60f),
        ("night/lucky", 0.60f),
        ("night/peace", 0.55f),

        ("slot/spin", 0.50f),
        ("slot/stop", 0.45f),
        ("slot/win", 0.60f),

        ("blackjack/deal", 0.60f),
        ("blackjack/win", 0.70f),
        ("blackjack/lose", 0.60f),

        ("delivery/complete", 0.50f),

        ("furniture/pickup", 0.50f),
        ("furniture/place", 0.60f),

        ("rent/paid", 0.70f),
        ("rent/failed", 0.80f),

        ("police/siren", 0.70f),
        ("police/arrest", 0.80f),

        ("mining/hit", 0.60f),
        ("mining/break", 0.60f),
        ("mining/sale", 0.60f),
        ("mining/warning", 0.70f),
    };

    [MenuItem("Roomies/Audio/Rebuild Sfx Levels")]
    private static void Rebuild()
    {
        System.IO.Directory.CreateDirectory("Assets/Resources/Audio");

        var levels = AssetDatabase.LoadAssetAtPath<SfxLevels>(AssetPath);
        bool created = levels == null;

        if (created)
        {
            levels = ScriptableObject.CreateInstance<SfxLevels>();
            AssetDatabase.CreateAsset(levels, AssetPath);
        }

        var existing = (levels.entries ?? new SfxLevels.Entry[0])
            .Where(e => e != null && !string.IsNullOrEmpty(e.key))
            .ToDictionary(e => e.key, e => e);

        var result = new List<SfxLevels.Entry>();
        int added = 0;

        foreach ((string key, float volume) in Defaults)
        {
            if (existing.TryGetValue(key, out SfxLevels.Entry entry))
            {
                result.Add(entry);      // 調整済みの値は残す
                existing.Remove(key);
            }
            else
            {
                result.Add(new SfxLevels.Entry { key = key, volume = volume });
                added++;
            }
        }

        // 表に手で足した行も消さずに後ろへ残す
        result.AddRange(existing.Values);

        levels.entries = result.ToArray();
        EditorUtility.SetDirty(levels);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Audio] 音量テーブルを更新しました（{(created ? "新規作成" : "更新")}・追加 {added} 行） → {AssetPath}");
        Selection.activeObject = levels;
    }
}
#endif
