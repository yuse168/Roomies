using System;
using UnityEngine;

/// <summary>
/// 効果音ごとの音量テーブル。
///
/// 規約（AGENTS.md「実行時に作らない」）：
///  ・音量をコードに直書きしない。この`.asset`をインスペクターで開いて調整する。
///  ・`Assets/Resources/Audio/SfxLevels.asset` に1つだけ置く。
///    メニュー Roomies/Audio/Rebuild Sfx Levels で作り直せる（既存の値は残す）。
///
/// 使い方：`GameAudio.Play("ui/confirm")` と呼ぶだけ。音量はここの値が掛かる。
/// キーが表に無ければ 1.0 として扱う（音は鳴る）。
/// </summary>
public class SfxLevels : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("Resources/Audio からの相対パス（例：ui/confirm）")]
        public string key;

        [Tooltip("この音だけの音量。設定画面のSE音量がさらに掛かる。")]
        [Range(0f, 2f)]
        public float volume = 1f;
    }

    [Tooltip("音が大きすぎる・小さすぎるときはここを直す。再生側のコードは触らなくてよい。")]
    public Entry[] entries = Array.Empty<Entry>();

    /// <summary>キーの音量。表に無ければ1。</summary>
    public float Get(string key)
    {
        if (entries == null || string.IsNullOrEmpty(key)) return 1f;

        for (int i = 0; i < entries.Length; i++)
        {
            Entry entry = entries[i];
            if (entry != null && entry.key == key) return Mathf.Max(0f, entry.volume);
        }
        return 1f;
    }
}
