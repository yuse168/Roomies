using System;
using UnityEngine;

/// <summary>
/// 足音を床の材質ごとに少し変える表。
///
/// 規約（AGENTS.md）：音の調整値はコードに直書きしない。
/// `Assets/Resources/Audio/FootstepSurfaces.asset` をインスペクターで開いて直す。
/// メニュー Roomies/Audio/Rebuild Footstep Surfaces で作り直せる（既存の値は残る）。
///
/// 素材は1種類のまま、ピッチと音量だけ変えて「木の上」「草の上」を作り分ける。
/// 判定は足元のレンダラーのマテリアル名に、ここの `keyword` が含まれるかどうか。
/// </summary>
public class FootstepSurfaces : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("マテリアル名に含まれていたらこの設定を使う（小文字で比較）")]
        public string keyword = "wood";

        [Tooltip("再生ピッチ。高いほど硬い床に聞こえる")]
        [Range(0.5f, 1.8f)] public float pitch = 1f;

        [Tooltip("この床での音量")]
        [Range(0f, 2f)] public float volume = 1f;
    }

    [Tooltip("上から順に探して、最初に当たったものを使う")]
    public Entry[] entries = Array.Empty<Entry>();

    [Header("どれにも当たらなかったとき")]
    [Range(0.5f, 1.8f)] public float defaultPitch = 1f;
    [Range(0f, 2f)] public float defaultVolume = 1f;

    /// <summary>マテリアル名から設定を引く。</summary>
    public void Resolve(string materialName, out float pitch, out float volume)
    {
        pitch = defaultPitch;
        volume = defaultVolume;

        if (string.IsNullOrEmpty(materialName) || entries == null) return;

        string lowered = materialName.ToLowerInvariant();
        foreach (Entry entry in entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.keyword)) continue;
            if (!lowered.Contains(entry.keyword.ToLowerInvariant())) continue;

            pitch = entry.pitch;
            volume = entry.volume;
            return;
        }
    }
}
