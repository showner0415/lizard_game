using UnityEngine;

/// <summary>
/// Player의 Rigidbody 속도와 PlayerMovement의 대시 상태를 읽어서
/// 도마뱀 Animator의 Idle/Walk 전환, 걷기 재생 속도, 구르기(Roll / RollForward) 트리거를 처리한다.
/// Player 오브젝트(PlayerMovement가 붙은 곳)에 추가하고, Animator는 자식 모델에 둔다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GeckoAnimDriver : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMovement movement;

    [Header("Walk")]
    [Tooltip("Walk 클립을 1배속으로 틀었을 때 발이 미끄러지지 않는 이동 속도 (모델 스케일 1 기준)")]
    [SerializeField] private float clipNaturalSpeed = 0.21f;
    [Tooltip("이 속도보다 느리면 Idle로 본다")]
    [SerializeField] private float idleThreshold = 0.1f;
    [SerializeField] private float minPlaybackSpeed = 0.6f;
    [SerializeField] private float maxPlaybackSpeed = 3f;

    [Header("Roll")]
    [Tooltip("켜면 앞구르기(RollForward), 끄면 옆으로 도는 구르기(Roll)를 쓴다")]
    [SerializeField] private bool useForwardRoll = true;
    [Tooltip("PlayerMovement의 Dash Duration과 같은 값으로 맞출 것")]
    [SerializeField] private float rollDuration = 0.28f;

    private const float SideRollClipLength = 17f / 60f;    // Roll 클립 길이 (0.283초)
    private const float ForwardRollClipLength = 30f / 60f; // RollForward 클립 길이 (0.5초)

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int RollHash = Animator.StringToHash("Roll");
    private static readonly int RollForwardHash = Animator.StringToHash("RollForward");

    private Rigidbody rb;
    private bool wasDashing;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (movement == null)
            movement = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        if (animator == null)
            return;

        bool dashing = movement != null && movement.IsDashing;

        if (dashing && !wasDashing)
            animator.SetTrigger(useForwardRoll ? RollForwardHash : RollHash);
        wasDashing = dashing;

        Vector3 v = rb.linearVelocity;
        v.y = 0f;
        float speed = v.magnitude;
        animator.SetFloat(SpeedHash, speed);

        if (dashing)
        {
            // 구르기 클립이 대시 시간 안에 딱 끝나도록 배속 조절
            float clipLength = useForwardRoll ? ForwardRollClipLength : SideRollClipLength;
            animator.speed = rollDuration > 0f ? clipLength / rollDuration : 1f;
        }
        else if (speed > idleThreshold)
        {
            float natural = clipNaturalSpeed * animator.transform.lossyScale.x;
            animator.speed = Mathf.Clamp(speed / natural, minPlaybackSpeed, maxPlaybackSpeed);
        }
        else
        {
            animator.speed = 1f;
        }
    }
}
