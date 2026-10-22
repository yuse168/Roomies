using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 遠景を走る電車。たまに通り過ぎるだけの飾りで、当たり判定も通信も無い。
///
/// 規約（AGENTS.md「実行時に作らない」）：
///  ・車両も線路もシーンに実体で置く。ここでは位置と回転を動かすだけ。
///  ・間隔・速さ・音はインスペクターで調整する。
///
/// 1回の通過ごとに、間隔・速さ・向きを引き直すので、同じ間隔で機械的に往復しない。
/// 走っていない間は描画を切って、画面の端に止まって見えないようにする。
/// </summary>
public class BackdropTrain : MonoBehaviour
{
    [Header("走る範囲（親のローカルX座標）")]
    [SerializeField] private float startX = -260f;
    [SerializeField] private float endX = 320f;

    [Header("速さ")]
    [Tooltip("1回ごとにこの範囲から引く（m/s）")]
    [SerializeField, Min(1f)] private float minSpeed = 22f;
    [SerializeField, Min(1f)] private float maxSpeed = 34f;

    [Header("通る間隔")]
    [Tooltip("通り過ぎてから次が来るまでの秒数。毎回この範囲から引く")]
    [SerializeField, Min(0f)] private float minWaitSeconds = 25f;
    [SerializeField, Min(0f)] private float maxWaitSeconds = 70f;

    [Tooltip("最初の1本が来るまでの秒数")]
    [SerializeField, Min(0f)] private float startDelay = 8f;

    [Header("向き")]
    [Tooltip("毎回ランダムで逆走させる（片方向だけだと作り物っぽいため）")]
    [SerializeField] private bool randomDirection = true;

    [Header("車輪")]
    [Tooltip("転がして見せる車輪の直径（m）")]
    [SerializeField, Min(0.1f)] private float wheelDiameter = 0.8f;

    [Header("通過音（電車に付いた口から鳴らす＝音も一緒に動く）")]
    [Tooltip("電車の子に置いたAudioSource。実行時には作らない")]
    [SerializeField] private AudioSource passSource;
    [SerializeField, Range(0f, 1f)] private float passVolume = 0.7f;

    private readonly List<Transform> wheels = new();
    private Renderer[] renderers;

    private float timer;
    private bool running;
    private bool reversed;
    private float speed;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            if (child.name.StartsWith("Wheel")) wheels.Add(child);
    }

    private void OnEnable()
    {
        timer = -startDelay;
        running = false;
        SetVisible(false);

        if (passSource != null) passSource.Stop();
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (!running)
        {
            if (timer < 0f) return;
            BeginPass();
        }

        float total = Mathf.Abs(endX - startX);
        float distance = speed * timer;

        if (distance >= total)
        {
            EndPass();
            return;
        }

        float t = distance / Mathf.Max(0.01f, total);
        Vector3 position = transform.localPosition;
        position.x = reversed ? Mathf.Lerp(endX, startX, t) : Mathf.Lerp(startX, endX, t);
        transform.localPosition = position;

        SpinWheels(Time.deltaTime);

        // 音は電車に付いた口から出るので、走るのに合わせて音も動く。
        // 設定画面の効果音ボリュームをそのまま反映する。
        if (passSource != null && passSource.isPlaying)
        {
            GameSettings.EnsureInitialized();
            passSource.volume = passVolume * GameSettings.SfxVolume;
        }
    }

    private void BeginPass()
    {
        running = true;
        timer = 0f;
        speed = Random.Range(minSpeed, maxSpeed);
        reversed = randomDirection && Random.value < 0.5f;

        // 逆走するときは車両ごと向きを変える（鼻先が進行方向を向く）
        transform.localRotation = Quaternion.Euler(0f, reversed ? 180f : 0f, 0f);

        Vector3 position = transform.localPosition;
        position.x = reversed ? endX : startX;
        transform.localPosition = position;

        SetVisible(true);

        if (passSource != null && passSource.clip != null)
        {
            GameSettings.EnsureInitialized();
            passSource.volume = passVolume * GameSettings.SfxVolume;
            passSource.loop = true;      // 通過し終わるまで鳴らし続ける
            passSource.Play();
        }
    }

    private void EndPass()
    {
        running = false;
        timer = -Random.Range(minWaitSeconds, maxWaitSeconds);
        SetVisible(false);

        if (passSource != null) passSource.Stop();
    }

    private void SpinWheels(float deltaTime)
    {
        if (wheels.Count == 0) return;

        // 進んだぶんだけ回す（見た目だけなので厳密でなくてよい）
        float degrees = speed * deltaTime / (Mathf.PI * wheelDiameter) * 360f;
        if (reversed) degrees = -degrees;

        foreach (Transform wheel in wheels)
            if (wheel != null) wheel.Rotate(0f, degrees, 0f, Space.Self);
    }

    private void SetVisible(bool visible)
    {
        if (renderers == null) return;

        foreach (Renderer renderer in renderers)
            if (renderer != null) renderer.enabled = visible;
    }
}
