using TMPro;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// NPC の頭の上の吹き出し。サーバーがせりふを決めて、全員の画面に同じ文を出す。
///
/// プレハブの形：ルートに NetworkObject とこのスクリプト。吹き出しはルートの直下の `Anchor_Bubble`
/// （中の Visual が板、Text が文字。ふだんは隠してある）。実行時には作らず、出し入れと向きだけを変える。
/// 吹き出しは、見ている人のカメラのほうを向く（各自の画面で）。
/// </summary>
public class NpcSpeechBubble : NetworkBehaviour
{
    [Header("吹き出し（Anchor_Bubble）と、その中の文字")]
    [SerializeField] private Transform bubble;
    [SerializeField] private TMP_Text text;

    [Header("出している長さ（秒）＝ 基本 ＋ 1 文字あたり")]
    [SerializeField, Min(0.5f)] private float baseSeconds = 2.2f;
    [SerializeField, Min(0f)] private float secondsPerCharacter = 0.09f;
    [Tooltip("出るとき・消えるときに伸び縮みする秒数")]
    [SerializeField, Min(0.01f)] private float popSeconds = 0.16f;
    [Tooltip("これより遠いカメラには出さない（遠くの吹き出しが壁ごしに見えないように）")]
    [SerializeField, Min(1f)] private float maxDistance = 16f;

    private float hideAt;
    private float shownAt;
    private Vector3 baseScale = Vector3.one;
    private bool baseScaleTaken;

    /// <summary>いま吹き出しが出ているか（確認の道具用）。</summary>
    public bool IsShowing => bubble != null && bubble.gameObject.activeSelf;
    /// <summary>いま出している文（確認の道具用）。</summary>
    public string CurrentLine => text != null ? text.text : "";

    private void Awake()
    {
        TakeBaseScale();
        if (bubble != null) bubble.gameObject.SetActive(false);
    }

    private void TakeBaseScale()
    {
        if (baseScaleTaken || bubble == null) return;
        baseScaleTaken = true;
        baseScale = bubble.localScale;
    }

    /// <summary>全員の画面に、そのせりふを出す（サーバーのみ）。</summary>
    public void ServerSay(string line)
    {
        if (!IsServer || !IsSpawned || string.IsNullOrEmpty(line)) return;
        SayRpc(line);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void SayRpc(string line)
    {
        if (bubble == null || text == null) return;
        TakeBaseScale();
        text.text = line;
        shownAt = Time.time;
        hideAt = Time.time + baseSeconds + secondsPerCharacter * line.Length;
        bubble.gameObject.SetActive(true);
        bubble.localScale = baseScale * 0.01f;
        GameAudio.PlayAt("casino/talk", bubble.position, 0.5f, 0.1f, 12f);
    }

    private void LateUpdate()
    {
        if (bubble == null || !bubble.gameObject.activeSelf) return;

        float left = hideAt - Time.time;
        if (left <= 0f) { bubble.gameObject.SetActive(false); return; }

        // 出るときはふくらみ、消えるときは縮む
        float grow = Mathf.Clamp01((Time.time - shownAt) / popSeconds), shrink = Mathf.Clamp01(left / popSeconds);
        float k = Mathf.Min(grow * (2f - grow) * 1.0f, shrink);
        bubble.localScale = baseScale * Mathf.Max(0.01f, k);

        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 toCamera = cam.transform.position - bubble.position;
        if (toCamera.sqrMagnitude > maxDistance * maxDistance) { bubble.localScale = baseScale * 0.0001f; return; }
        toCamera.y = 0f;
        if (toCamera.sqrMagnitude > 0.0001f) bubble.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
    }
}
