using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 効果音の再生口。
///
/// 使い方：`Assets/Resources/Audio/` 以下へ音声ファイルを置き、パスをキーにして呼ぶ。
///   GameAudio.Play("ui/confirm");                  → Resources/Audio/ui/confirm
///   GameAudio.PlayAt("player/land", transform.position);
///
/// バリエーション：`jump_1` `jump_2` … と連番で置くと、Play("player/jump") が
/// 毎回ランダムに1つ選ぶ。足音のように繰り返す音の単調さを消すため。
///
/// 設計方針（AGENTS.mdの「軽さ・分かりやすさ優先」に合わせる）：
///  ・AudioMixerを使わない。音量はGameSettingsの値を掛けるだけ
///  ・Inspector配線をしない。ファイルを置けば鳴る（シーン編集が不要）
///  ・AudioSourceは使い回す。1音ごとにGameObjectを作らない
///  ・クリップが無いキーは黙って無視する。音が未入荷でもゲームは壊れない
/// </summary>
public static class GameAudio
{
    private const string ResourceRoot = "Audio/";
    private const int PoolSize = 12;
    private const int MaxVariations = 8;

    private static readonly Dictionary<string, AudioClip[]> clipCache = new();
    private static AudioSource[] pool;
    private static int nextSource;

    // ================================================================
    // 再生
    // ================================================================

    /// <summary>2Dで鳴らす（UI・通知など、距離に関係ない音）。</summary>
    public static void Play(string key, float volume = 1f, float pitchJitter = 0f)
    {
        AudioClip clip = Pick(key);
        if (clip == null) return;

        AudioSource source = NextSource();
        if (source == null) return;

        source.transform.localPosition = Vector3.zero;
        source.spatialBlend = 0f;
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume) * SfxVolume;
        source.pitch = Pitch(pitchJitter);
        source.Play();
    }

    /// <summary>ワールド座標で鳴らす（足音・採掘など、位置のある音）。</summary>
    public static void PlayAt(
        string key, Vector3 position, float volume = 1f, float pitchJitter = 0f,
        float maxDistance = 22f)
    {
        AudioClip clip = Pick(key);
        if (clip == null) return;

        AudioSource source = NextSource();
        if (source == null) return;

        source.transform.position = position;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1.5f;
        source.maxDistance = maxDistance;
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume) * SfxVolume;
        source.pitch = Pitch(pitchJitter);
        source.Play();
    }

    /// <summary>キーに対応するクリップが1つでも入荷しているか。</summary>
    public static bool Has(string key) => Pick(key) != null;

    private static float SfxVolume
    {
        get
        {
            GameSettings.EnsureInitialized();
            return GameSettings.SfxVolume;
        }
    }

    private static float Pitch(float jitter)
    {
        if (jitter <= 0f) return 1f;
        return 1f + Random.Range(-jitter, jitter);
    }

    // ================================================================
    // クリップの読み込み
    // ================================================================

    /// <summary>キーのクリップ群から1つ選ぶ。未入荷ならnull。</summary>
    private static AudioClip Pick(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;

        AudioClip[] clips = Load(key);
        if (clips.Length == 0) return null;
        return clips.Length == 1 ? clips[0] : clips[Random.Range(0, clips.Length)];
    }

    /// <summary>
    /// `key` 単体と `key_1`〜`key_8` を拾う。結果はキャッシュするので
    /// 鳴らすたびにResourcesを叩かない。
    /// </summary>
    private static AudioClip[] Load(string key)
    {
        if (clipCache.TryGetValue(key, out AudioClip[] cached)) return cached;

        var found = new List<AudioClip>();

        AudioClip single = Resources.Load<AudioClip>(ResourceRoot + key);
        if (single != null) found.Add(single);

        for (int i = 1; i <= MaxVariations; i++)
        {
            AudioClip variation = Resources.Load<AudioClip>($"{ResourceRoot}{key}_{i}");
            if (variation == null) break;
            found.Add(variation);
        }

        AudioClip[] result = found.ToArray();
        clipCache[key] = result;
        return result;
    }

    /// <summary>音声ファイルを差し替えた直後に鳴らし分けを作り直す（Editor用）。</summary>
    public static void ClearCache() => clipCache.Clear();

    // ================================================================
    // AudioSourceプール
    // ================================================================

    private static AudioSource NextSource()
    {
        if (!EnsurePool()) return null;

        // 一周して空きが無ければ、最も古いものを奪って鳴らす
        for (int i = 0; i < PoolSize; i++)
        {
            AudioSource candidate = pool[nextSource];
            nextSource = (nextSource + 1) % PoolSize;
            if (candidate != null && !candidate.isPlaying) return candidate;
        }

        AudioSource fallback = pool[nextSource];
        nextSource = (nextSource + 1) % PoolSize;
        return fallback;
    }

    private static bool EnsurePool()
    {
        if (pool != null && pool.Length == PoolSize && pool[0] != null) return true;
        if (!Application.isPlaying) return false;

        var host = new GameObject("GameAudio");
        Object.DontDestroyOnLoad(host);

        pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var child = new GameObject($"Source{i}");
            child.transform.SetParent(host.transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            pool[i] = source;
        }

        nextSource = 0;
        return true;
    }
}
