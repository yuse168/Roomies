using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 逮捕の演出。文字だけだった旧 `SmugglingArrestOverlay`（IMGUI）の置き換え。
///
/// 規約（AGENTS.md「実行時に作らない」）：
///  ・表示する物はすべてプレハブの子として置いてある。ここでは色と透明度を動かすだけ。
///  ・置き直しは メニュー Roomies/Smuggling/Build Arrest Cinematic。
///
/// 流れ（逮捕された本人の画面だけ）：
///  1. パトカーの赤青が交互に画面を染める／カメラが後ろへ引いて自分が映る
///  2. 白フラッシュ＋「ARRESTED」のスタンプ
///  3. 暗転（サーバーが牢屋へ飛ばすタイミングと合わせる）
///  4. 牢屋で明転し、カメラを一人称へ戻す
///
/// 追跡中の赤い脈動と警告文もここが出す。
/// </summary>
public class ArrestCinematic : MonoBehaviour
{
    public static ArrestCinematic Instance { get; private set; }

    [Header("追跡中の表示")]
    [SerializeField] private Image chaseVignette;
    [SerializeField] private TextMeshProUGUI chaseLabel;
    [SerializeField] private TextMeshProUGUI chaseHint;
    [SerializeField, Range(0f, 1f)] private float chaseVignetteMax = 0.42f;
    [SerializeField, Min(0.1f)] private float chasePulseSeconds = 0.9f;

    [Header("逮捕演出")]
    [SerializeField] private Image sirenRed;
    [SerializeField] private Image sirenBlue;
    [SerializeField] private Image whiteFlash;
    [SerializeField] private Image blackFade;
    [SerializeField] private TextMeshProUGUI stamp;

    [Header("時間（サーバーが1.7秒後に牢屋へ飛ばす）")]
    [SerializeField, Min(0f)] private float sirenSeconds = 1.2f;
    [SerializeField, Min(0.05f)] private float sirenInterval = 0.16f;
    [SerializeField, Min(0f)] private float flashSeconds = 0.25f;
    [SerializeField, Min(0f)] private float fadeInSeconds = 0.25f;
    [SerializeField, Min(0f)] private float holdBlackSeconds = 0.45f;
    [SerializeField, Min(0f)] private float fadeOutSeconds = 0.7f;

    [Header("カメラの引き")]
    [Tooltip("自分の後ろへ何m引くか")]
    [SerializeField, Min(0f)] private float cameraBack = 3.4f;
    [Tooltip("どれだけ上から見るか")]
    [SerializeField] private float cameraUp = 1.9f;
    [Tooltip("体のどの高さを見るか")]
    [SerializeField] private float lookHeight = 1.1f;
    [SerializeField, Min(0.05f)] private float cameraMoveSeconds = 0.55f;

    [Header("赤青の濃さ")]
    [SerializeField, Range(0f, 1f)] private float sirenAlpha = 0.5f;

    private Coroutine routine;
    private bool isChasing;
    private float chaseTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        HideAll();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void HideAll()
    {
        SetAlpha(chaseVignette, 0f);
        SetAlpha(sirenRed, 0f);
        SetAlpha(sirenBlue, 0f);
        SetAlpha(whiteFlash, 0f);
        SetAlpha(blackFade, 0f);
        SetAlpha(stamp, 0f);
        ShowChaseText(false);
    }

    // ================================================================
    // 追跡中
    // ================================================================

    public void SetChasing(bool chasing)
    {
        isChasing = chasing;
        if (!chasing)
        {
            SetAlpha(chaseVignette, 0f);
            ShowChaseText(false);
            return;
        }

        ShowChaseText(true);
    }

    private void Update()
    {
        if (!isChasing) return;

        chaseTimer += Time.unscaledDeltaTime;
        float pulse = Mathf.Abs(Mathf.Sin(chaseTimer / chasePulseSeconds * Mathf.PI));
        SetAlpha(chaseVignette, pulse * chaseVignetteMax);
    }

    private void ShowChaseText(bool show)
    {
        if (chaseLabel != null) chaseLabel.gameObject.SetActive(show);
        if (chaseHint != null) chaseHint.gameObject.SetActive(show);
    }

    // ================================================================
    // 逮捕
    // ================================================================

    public void PlayArrest()
    {
        SetChasing(false);
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ArrestRoutine());
    }

    private IEnumerator ArrestRoutine()
    {
        GameAudio.Play("police/siren");

        // 自分の姿が見えるように、カメラを後ろへ引く
        Transform camera = null;
        Transform cameraParent = null;
        Vector3 cameraLocalPosition = Vector3.zero;
        Quaternion cameraLocalRotation = Quaternion.identity;
        PlayerMovement movement = LocalMovement();

        if (movement != null && movement.cameraTransform != null)
        {
            camera = movement.cameraTransform;
            cameraParent = camera.parent;
            cameraLocalPosition = camera.localPosition;
            cameraLocalRotation = camera.localRotation;

            // 一人称の手ブレ処理と取り合いにならないよう、いったん親から外す
            movement.enableCameraMotion = false;
            camera.SetParent(null, true);
            StartCoroutine(PullBackRoutine(camera, movement.transform));
        }

        // 1) 赤青の点滅
        float elapsed = 0f;
        bool red = true;
        while (elapsed < sirenSeconds)
        {
            SetAlpha(sirenRed, red ? sirenAlpha : 0f);
            SetAlpha(sirenBlue, red ? 0f : sirenAlpha);
            red = !red;

            elapsed += sirenInterval;
            yield return new WaitForSecondsRealtime(sirenInterval);
        }
        SetAlpha(sirenRed, 0f);
        SetAlpha(sirenBlue, 0f);

        // 2) 白フラッシュ＋スタンプ
        GameAudio.Play("police/arrest");
        SetAlpha(stamp, 1f);
        if (stamp != null) stamp.transform.localScale = Vector3.one * 1.6f;

        elapsed = 0f;
        while (elapsed < flashSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / flashSeconds);
            SetAlpha(whiteFlash, 1f - t);
            if (stamp != null)
                stamp.transform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, t);
            yield return null;
        }
        SetAlpha(whiteFlash, 0f);

        // 3) 暗転（サーバーのテレポートに合わせる）
        yield return Fade(0f, 1f, fadeInSeconds);
        SetAlpha(stamp, 0f);
        yield return new WaitForSecondsRealtime(holdBlackSeconds);

        // 4) 牢屋で明転。カメラを一人称へ戻してから開ける
        if (camera != null)
        {
            camera.SetParent(cameraParent, false);
            camera.localPosition = cameraLocalPosition;
            camera.localRotation = cameraLocalRotation;
            if (movement != null) movement.enableCameraMotion = true;
        }

        yield return Fade(1f, 0f, fadeOutSeconds);
        routine = null;
    }

    // カメラを後ろ上へ滑らかに引き、自分を見下ろす
    private IEnumerator PullBackRoutine(Transform camera, Transform player)
    {
        Vector3 start = camera.position;
        Quaternion startRotation = camera.rotation;
        float elapsed = 0f;

        while (elapsed < cameraMoveSeconds && camera != null && player != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / cameraMoveSeconds));

            Vector3 target = player.position
                             - player.forward * cameraBack
                             + Vector3.up * cameraUp;
            Vector3 lookAt = player.position + Vector3.up * lookHeight;

            camera.position = Vector3.Lerp(start, target, t);
            camera.rotation = Quaternion.Slerp(
                startRotation, Quaternion.LookRotation(lookAt - camera.position), t);

            yield return null;
        }

        // 引き切ったあとも本人を追い続ける（牢屋へ飛ぶまで）
        while (camera != null && player != null && routine != null)
        {
            Vector3 target = player.position - player.forward * cameraBack + Vector3.up * cameraUp;
            Vector3 lookAt = player.position + Vector3.up * lookHeight;

            camera.position = Vector3.Lerp(camera.position, target, Time.unscaledDeltaTime * 6f);
            camera.rotation = Quaternion.Slerp(
                camera.rotation, Quaternion.LookRotation(lookAt - camera.position),
                Time.unscaledDeltaTime * 6f);

            yield return null;
        }
    }

    private IEnumerator Fade(float from, float to, float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(blackFade, Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds)));
            yield return null;
        }
        SetAlpha(blackFade, to);
    }

    private static PlayerMovement LocalMovement()
    {
        var nm = Unity.Netcode.NetworkManager.Singleton;
        var player = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
        return player != null ? player.GetComponent<PlayerMovement>() : null;
    }

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null) return;
        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }
}
