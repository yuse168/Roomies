using System.Collections;
using UnityEngine;

/// <summary>
/// BGMの再生口。シーンに置いた実体（`GameMusic` プレハブ）が鳴らす。
///
/// 規約（AGENTS.md「実行時に作らない」）：
///  ・AudioSourceも曲もスクリプトから作らない。プレハブの子として置き、
///    インスペクターから曲・音量・フェード時間を差し替えられるようにする。
///  ・曲の差し替えは `menuTrack` / `dayTrack` / `nightTrack` に別のAudioClipを入れるだけ。
///
/// 動き：
///  ・DayManagerがあるシーン → 朝は `dayTrack`、夜は `nightTrack`。ゲームオーバーで止める。
///  ・DayManagerが無いシーン（メニュー）→ `menuTrack` を流し続ける。
///  ・切り替えは2つのAudioSourceを交互に使ったクロスフェード。
///  ・音量は `GameSettings.BgmVolume` を掛ける（設定画面のスライダーがそのまま効く）。
/// </summary>
public class GameMusic : MonoBehaviour
{
    public static GameMusic Instance { get; private set; }

    [Header("再生口（プレハブの子を割り当てる。実行時には作らない）")]
    [SerializeField] private AudioSource sourceA;
    [SerializeField] private AudioSource sourceB;

    [Header("曲")]
    [Tooltip("メインメニュー・ロビーで流す曲")]
    [SerializeField] private AudioClip menuTrack;
    [Tooltip("朝〜日没まで流す曲")]
    [SerializeField] private AudioClip dayTrack;
    [Tooltip("夜の自由行動中に流す曲")]
    [SerializeField] private AudioClip nightTrack;

    [Header("音量・フェード")]
    [Tooltip("曲ごとの音量差を吸収する倍率。設定画面のBGM音量にさらに掛かる。")]
    [SerializeField, Range(0f, 1f)] private float menuVolume = 0.55f;
    [SerializeField, Range(0f, 1f)] private float dayVolume = 0.50f;
    [SerializeField, Range(0f, 1f)] private float nightVolume = 0.55f;
    [Tooltip("曲を切り替えるときのクロスフェード秒数")]
    [SerializeField, Min(0f)] private float fadeSeconds = 1.6f;
    [Tooltip("ゲームオーバーで曲を消すまでの秒数")]
    [SerializeField, Min(0f)] private float stopFadeSeconds = 1.0f;

    [Tooltip("朝の演出（日付切替の効果音とカットイン）と重ならないよう、曲を遅らせる秒数")]
    [SerializeField, Min(0f)] private float morningDelaySeconds = 1.0f;

    [Tooltip("演出が終わるのを待つ最大時間（保険）")]
    [SerializeField, Min(1f)] private float transitionWaitLimit = 12f;

    private AudioSource current;   // いま鳴っている方
    private AudioSource standby;   // 次に使う方
    private Coroutine fadeRoutine;
    private Coroutine waitRoutine;

    // 時間帯と曲がずれていないか見直す間隔（秒）
    private const float CheckInterval = 0.5f;
    private float nextCheckTime;
    private bool warnedMissingClip;
    private float targetVolume;
    private AudioClip targetClip;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        current = sourceA;
        standby = sourceB;
    }

    private void OnEnable()
    {
        DayManager.OnMorningArrived += PlayDay;
        DayManager.OnNightArrived += PlayNight;
    }

    private void OnDisable()
    {
        DayManager.OnMorningArrived -= PlayDay;
        DayManager.OnNightArrived -= PlayNight;
    }

    private void Start()
    {
        if (sourceA == null || sourceB == null)
        {
            Debug.LogWarning("[GameMusic] AudioSourceが割り当てられていません。プレハブの子を2つ割り当ててください。");
            enabled = false;
            return;
        }

        // DayManagerがいないシーン（メニュー）ではメニュー曲、
        // いるシーンでは今が朝か夜かに合わせて始める。
        var day = DayManager.Instance;
        if (day == null) PlayMenu();
        else if (day.IsNight) PlayNight();
        else PlayDay();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // 設定画面のスライダーを動かした瞬間に反映させる。
        // フェード中はコルーチンが音量を作っているので触らない（触ると音が戻ってしまう）。
        if (fadeRoutine == null && current != null && current.isPlaying)
            current.volume = targetVolume * BgmVolume;

        // いまの時間帯に合った曲になっているかを定期的に見直す。
        // イベント（朝・夜の切り替え）を取りこぼしても、ここで必ず追いつく。
        nextCheckTime -= Time.unscaledDeltaTime;
        if (nextCheckTime > 0f) return;

        nextCheckTime = CheckInterval;
        RefreshTrack();
    }

    /// <summary>いまの状況に合う曲へ合わせる（既に合っていれば何もしない）。</summary>
    private void RefreshTrack()
    {
        var day = DayManager.Instance;

        // DayManagerがまだ出ていない間（ロビーから入った直後など）はメニュー曲
        if (day == null || !day.IsSpawned)
        {
            Switch(menuTrack, menuVolume);
            return;
        }

        if (day.IsGameOver) return;     // ゲームオーバーは StopMusic 側に任せる

        if (day.IsNight) PlayNight();
        else PlayDay();
    }

    private static float BgmVolume
    {
        get
        {
            GameSettings.EnsureInitialized();
            return GameSettings.BgmVolume;
        }
    }

    // ---- 外から呼べる切り替え ----

    public void PlayMenu() => Switch(menuTrack, menuVolume);

    /// <summary>朝の曲。切り替え演出が終わってから鳴らす（演出の音と被らせない）。</summary>
    public void PlayDay()
    {
        if (!isActiveAndEnabled || dayTrack == null) return;

        // すでに朝の曲が鳴っている／待っている最中なら何もしない
        if (waitRoutine != null) return;
        if (targetClip == dayTrack && current != null && current.isPlaying) return;

        waitRoutine = StartCoroutine(PlayAfterTransition(dayTrack, dayVolume));
    }

    public void PlayNight()
    {
        if (!isActiveAndEnabled) return;

        // 朝の曲を待っている途中なら取り消す（夜なのに朝の曲が始まるのを防ぐ）
        if (waitRoutine != null)
        {
            StopCoroutine(waitRoutine);
            waitRoutine = null;
        }

        Switch(nightTrack, nightVolume);
    }

    private IEnumerator PlayAfterTransition(AudioClip clip, float volume)
    {
        float waited = 0f;

        // 演出中（DayTransitionUIが出ている間）は待つ
        while (waited < transitionWaitLimit &&
               DayManager.Instance != null && DayManager.Instance.IsTransitioning)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (morningDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(morningDelaySeconds);

        waitRoutine = null;
        Switch(clip, volume);
    }

    /// <summary>ゲームオーバーなどで曲を消す。</summary>
    public void StopMusic()
    {
        if (!isActiveAndEnabled) return;
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeOutRoutine());
        targetClip = null;
    }

    private void Switch(AudioClip clip, float volume)
    {
        if (!isActiveAndEnabled) return;

        if (clip == null)
        {
            // 曲が割り当てられていないと無音になる。原因が分かるように一度だけ知らせる。
            if (!warnedMissingClip)
            {
                warnedMissingClip = true;
                Debug.LogWarning("[GameMusic] 曲が割り当てられていません。" +
                                 "GameMusic プレハブの Menu/Day/Night Track を確認してください。");
            }
            return;
        }
        if (targetClip == clip && current != null && current.isPlaying) return;

        targetClip = clip;
        targetVolume = volume;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(CrossFadeRoutine(clip, volume));
    }

    private IEnumerator CrossFadeRoutine(AudioClip clip, float volume)
    {
        AudioSource from = current;
        AudioSource to = standby;

        to.clip = clip;
        to.loop = true;
        to.volume = 0f;
        to.Play();

        float fromStart = from != null ? from.volume : 0f;
        float elapsed = 0f;

        while (elapsed < fadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = fadeSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeSeconds);

            to.volume = volume * BgmVolume * t;
            if (from != null) from.volume = fromStart * (1f - t);

            yield return null;
        }

        if (from != null)
        {
            from.Stop();
            from.clip = null;
        }

        to.volume = volume * BgmVolume;

        current = to;
        standby = from;
        fadeRoutine = null;
    }

    private IEnumerator FadeOutRoutine()
    {
        AudioSource from = current;
        if (from == null) yield break;

        float start = from.volume;
        float elapsed = 0f;

        while (elapsed < stopFadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = stopFadeSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / stopFadeSeconds);
            from.volume = start * (1f - t);
            yield return null;
        }

        from.Stop();
        from.clip = null;
        fadeRoutine = null;
    }
}
