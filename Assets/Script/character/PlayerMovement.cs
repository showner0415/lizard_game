using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerStamina))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 14f;
    [SerializeField] private float dashDuration = 0.28f;
    [SerializeField] private bool faceDashDirection = true;
    [SerializeField] private bool allowDashWithoutMoveInput = true;
    [SerializeField] private AnimationCurve dashSpeedCurve = new AnimationCurve(
        new Keyframe(0f, 1f, 0f, 0f),
        new Keyframe(0.4f, 0.75f, -0.8f, -0.8f),
        new Keyframe(1f, 0f, -1.2f, 0f));

    [Header("Aim")]
    [SerializeField] private MouseRayCaster raycaster;

    private Rigidbody rb;
    private Transform cachedTransform;
    private PlayerStamina stamina;

    private Vector2 moveInput;
    private Vector3 moveDir;
    private Vector3 dashDir;
    private Vector3 velocity;
    private Vector3 aimDir;

    private bool sprintHeld;
    private bool isSprinting;
    private bool isDashing;
    private bool hasRaycaster;
    private float dashElapsed;
    private float invDashDuration;
    private Quaternion dashRotation;

    private const float MoveInputDeadzoneSq = 0.01f;
    private const float AimDeadzoneSq = 0.001f;

    public bool IsDashing => isDashing;
    public bool IsSprinting => isSprinting;
    public bool IsUsingStamina => isDashing | isSprinting;

    private void Awake()
    {
        cachedTransform = transform;
        rb = GetComponent<Rigidbody>();
        stamina = GetComponent<PlayerStamina>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (raycaster == null)
            raycaster = GetComponent<MouseRayCaster>();

        hasRaycaster = raycaster != null;
        invDashDuration = dashDuration > 0f ? 1f / dashDuration : 1f;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        sprintHeld = context.ReadValueAsButton();
    }

    public void OnRoll(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        TryStartDash();
    }

    void TryStartDash()
    {
        if (isDashing)
            return;

        bool hasMoveInput = moveInput.sqrMagnitude >= MoveInputDeadzoneSq;
        if (!hasMoveInput && !allowDashWithoutMoveInput)
            return;

        if (hasMoveInput)
        {
            dashDir.x = moveInput.x;
            dashDir.y = 0f;
            dashDir.z = moveInput.y;
        }
        else
        {
            Vector3 forward = cachedTransform.forward;
            dashDir.x = forward.x;
            dashDir.y = 0f;
            dashDir.z = forward.z;
        }

        float magSq = dashDir.sqrMagnitude;
        if (magSq < AimDeadzoneSq)
            return;

        if (stamina != null && !stamina.TryConsumeDash())
            return;

        float invMag = 1f / Mathf.Sqrt(magSq);
        dashDir.x *= invMag;
        dashDir.z *= invMag;

        isDashing = true;
        isSprinting = false;
        dashElapsed = 0f;
        dashRotation = Quaternion.LookRotation(dashDir);
    }

    void UpdateSprintState()
    {
        if (isDashing)
        {
            isSprinting = false;
            return;
        }

        // Shift 안 누르면 즉시 탈출 (스태미나 조회 생략)
        if (!sprintHeld)
        {
            isSprinting = false;
            return;
        }

        if (moveInput.sqrMagnitude < MoveInputDeadzoneSq)
        {
            isSprinting = false;
            return;
        }

        isSprinting = stamina == null || stamina.Current > 0f;
    }

    private void Update()
    {
        UpdateSprintState();

        if (isDashing)
        {
            if (faceDashDirection)
                cachedTransform.rotation = dashRotation;
            return;
        }

        RotateToMouse();
    }

    private void FixedUpdate()
    {
        if (isDashing)
            ApplyDashVelocity();
        else
            ApplyMoveVelocity();
    }

    void ApplyMoveVelocity()
    {
        moveDir.x = moveInput.x;
        moveDir.y = 0f;
        moveDir.z = moveInput.y;

        float speed = isSprinting ? sprintSpeed : moveSpeed;
        float y = rb.linearVelocity.y;
        velocity.x = moveDir.x * speed;
        velocity.y = y;
        velocity.z = moveDir.z * speed;
        rb.linearVelocity = velocity;
    }

    void ApplyDashVelocity()
    {
        dashElapsed += Time.fixedDeltaTime;
        float t = dashElapsed * invDashDuration;
        if (t >= 1f)
        {
            isDashing = false;
            ApplyMoveVelocity();
            return;
        }

        float curve = dashSpeedCurve.Evaluate(t);
        float speed = Mathf.Lerp(moveSpeed, dashSpeed, curve);

        float y = rb.linearVelocity.y;
        velocity.x = dashDir.x * speed;
        velocity.y = y;
        velocity.z = dashDir.z * speed;
        rb.linearVelocity = velocity;
    }

    void RotateToMouse()
    {
        if (!hasRaycaster)
            return;

        if (!raycaster.TryGetMouseWorldPosition(out Vector3 target))
            return;

        Vector3 pos = cachedTransform.position;
        aimDir.x = target.x - pos.x;
        aimDir.y = 0f;
        aimDir.z = target.z - pos.z;
        if (aimDir.sqrMagnitude <= AimDeadzoneSq)
            return;

        cachedTransform.rotation = Quaternion.LookRotation(aimDir);
    }
}
