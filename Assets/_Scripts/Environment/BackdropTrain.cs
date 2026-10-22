using UnityEngine;

/// <summary>
/// 遠景を走る電車。奥行きを出すための飾りで、当たり判定も通信も無い。
///
/// 規約（AGENTS.md「実行時に作らない」）：
///  ・車両も線路もシーンに実体で置く。ここでは位置を動かすだけ。
///  ・速さ・走る範囲・待ち時間はインスペクターで調整する。
///
/// 動き：`startX` から `endX` まで走り、通り過ぎたら `waitSeconds` だけ待って
/// また `startX` から走り直す（ループ）。全クライアントで同じ見た目になるよう、
/// ゲーム内時間ではなく経過時間だけで決める。
/// </summary>
public class BackdropTrain : MonoBehaviour
{
    [Header("走る範囲（ワールドX座標）")]
    [SerializeField] private float startX = -260f;
    [SerializeField] private float endX = 320f;

    [Header("速さと間隔")]
    [Tooltip("秒速（m/s）。遠いので速めでちょうどよく見える")]
    [SerializeField, Min(1f)] private float speed = 38f;

    [Tooltip("走り終えてから次の電車が来るまでの秒数")]
    [SerializeField, Min(0f)] private float waitSeconds = 14f;

    [Tooltip("最初の1本が来るまでの秒数")]
    [SerializeField, Min(0f)] private float startDelay = 4f;

    private float timer;
    private bool running;

    private void OnEnable()
    {
        timer = -startDelay;
        running = false;
        Hide();
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (!running)
        {
            if (timer < 0f) return;

            running = true;
            timer = 0f;
            Show();
        }

        float distance = speed * timer;
        float total = Mathf.Abs(endX - startX);

        if (distance >= total)
        {
            running = false;
            timer = -waitSeconds;
            Hide();
            return;
        }

        Vector3 position = transform.localPosition;
        position.x = Mathf.Lerp(startX, endX, distance / Mathf.Max(0.01f, total));
        transform.localPosition = position;
    }

    // 走っていない間は端に置いて見えなくする（オブジェクトは消さない）
    private void Hide()
    {
        Vector3 position = transform.localPosition;
        position.x = startX;
        transform.localPosition = position;

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            renderer.enabled = false;
    }

    private void Show()
    {
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            renderer.enabled = true;
    }
}
