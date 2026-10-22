using UnityEngine;

/// <summary>
/// NPC（警察・売人・運び屋など）の見た目と足音。
///
/// ・プレイヤーと同じBobモデルを使っているのに Animator が動いていないと
///   Aポーズのまま滑るので、自分の移動量から `Speed` を入れて歩かせる
/// ・足音も同じ素材で鳴らす（位置から鳴るので近くにいる人にだけ聞こえる）
///
/// 規約（AGENTS.md）：見た目も音も新しく作らない。置いてあるAnimatorを動かすだけ。
/// 参照はインスペクターから割り当てる（メニュー Roomies/Smuggling/Setup NPC Animation で自動設定できる）。
/// </summary>
public class NpcMotionFeedback : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("モデルのAnimator（Visual/ModelRoot/… にある）")]
    [SerializeField] private Animator animator;

    [Header("歩き")]
    [Tooltip("この速さ以上で歩いていると見なす")]
    [SerializeField, Min(0f)] private float moveThreshold = 0.15f;

    [Tooltip("Animatorへ渡す速さの上限（プレイヤーの走り速度に合わせる）")]
    [SerializeField, Min(1f)] private float maxSpeed = 8f;

    [Header("足音")]
    [Tooltip("何m進むごとに1歩鳴らすか")]
    [SerializeField, Min(0.2f)] private float footstepDistance = 1.7f;

    [Tooltip("足音の音量（素材ごとの倍率がさらに掛かる）")]
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.7f;

    [Tooltip("足音が届く距離")]
    [SerializeField, Min(2f)] private float footstepRange = 16f;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int GroundedHash = Animator.StringToHash("IsGrounded");

    private bool hasSpeed;
    private bool hasGrounded;
    private Vector3 lastPosition;
    private float travel;
    private float smoothedSpeed;
    private string channel;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        lastPosition = transform.position;

        // コントローラーに無いパラメーターを触ると警告が出るので、あるものだけ使う
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.name == "Speed") hasSpeed = true;
                else if (parameter.name == "IsGrounded") hasGrounded = true;
            }
        }

        // NPCごとに別チャンネル。別々のNPCの足音は重なってよい。
        channel = "npc/" + GetInstanceID();
    }

    private void Update()
    {
        Vector3 position = transform.position;
        Vector3 delta = position - lastPosition;
        delta.y = 0f;
        lastPosition = position;

        float distance = delta.magnitude;
        float speed = Time.deltaTime > 0f ? distance / Time.deltaTime : 0f;
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, Time.deltaTime * 10f);

        if (animator != null)
        {
            if (hasSpeed) animator.SetFloat(SpeedHash, Mathf.Clamp(smoothedSpeed, 0f, maxSpeed));
            if (hasGrounded) animator.SetBool(GroundedHash, true);
        }

        if (smoothedSpeed < moveThreshold)
        {
            travel = 0f;
            return;
        }

        travel += distance;
        if (travel < footstepDistance) return;

        travel = 0f;
        FootstepAudio.PlayAt(position, footstepVolume, channel, footstepRange);
    }
}
