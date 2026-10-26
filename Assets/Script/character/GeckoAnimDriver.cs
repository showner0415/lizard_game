using UnityEngine;

/// <summary>
/// Player의 Rigidbody 속도를 읽어서 도마뱀 Animator의 Idle/Walk 전환과 재생 속도를 맞춘다.
/// Player 오브젝트(PlayerMovement가 붙은 곳)에 추가하고, Animator는 자식 모델에 둔다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GeckoAnimDriver : MonoBehaviour
{
    [SerializeField] private Animator animator;

    [Tooltip("Walk 클립을 1배속으로 틀었을 때 발이 미끄러지지 않는 이동 속도 (모델 스케일 1 기준)")]
    [SerializeField] private float clipNaturalSpeed = 0.21f;

    [Tooltip("이 속도보다 느리면 Idle로 본다")]
    [SerializeField] private float idleThreshold = 0.1f;

    [Tooltip("Walk 재생 배속 상한/하한 (너무 빨라지면 다리가 떨리는 것처럼 보임)")]
    [SerializeField] private float minPlaybackSpeed = 0.6f;
    [SerializeField] private float maxPlaybackSpeed = 3f;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (animator == null)
            return;

        Vector3 v = rb.linearVelocity;
        v.y = 0f;
        float speed = v.magnitude;

        animator.SetFloat(SpeedHash, speed);

        if (speed > idleThreshold)
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
