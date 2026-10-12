using UnityEngine;

/// <summary>
/// �÷��̾ �ε巴�� ���󰡴� ī�޶�.
/// ��: �÷��̾� �̵��� FixedUpdate���� �̷�����ٸ�,
/// �÷��̾� Rigidbody�� Interpolate ������ �Ѿ� ī�޶� ������ �ʴ´�.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Offset")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 17.5f, -5f);

    [Header("Follow Settings")]
    [Tooltip("��ǥ �������� �����ϴ� �� �ɸ��� �뷫���� �ð�(��)")]
    [SerializeField] private float smoothTime = 0.2f;

    private Transform cachedTransform;
    private Vector3 currentVelocity;
    private Vector3 desiredPosition;

    private void Awake()
    {
        cachedTransform = transform;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 targetPos = target.position;
        desiredPosition.x = targetPos.x + offset.x;
        desiredPosition.y = targetPos.y + offset.y;
        desiredPosition.z = targetPos.z + offset.z;

        cachedTransform.position = Vector3.SmoothDamp(
            cachedTransform.position,
            desiredPosition,
            ref currentVelocity,
            smoothTime);
    }
}
