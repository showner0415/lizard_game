using UnityEngine;

/// <summary>
/// 바닥에 떨어진 아이템 (줍기 가능)
/// IInteractable 구현으로 기존 상호작용 시스템과 통합
/// 
/// 물리 동작:
/// - 떨어질 때 회전하지 않음 (Rigidbody constraints)
/// - 바닥에 닿으면 완전히 정지 (kinematic으로 전환)
/// - 착지 후 모든 충돌 비활성화 (Collider를 Trigger로 전환)
/// </summary>
[RequireComponent(typeof(Collider))]
public class PickupableItem : MonoBehaviour, IInteractable
{
    [Header("Item Data")]
    [SerializeField] private ItemData itemData;
    [SerializeField] private int amount = 1;

    [Header("Interaction Settings")]
    [SerializeField] private float interactRange = 2f;
    [SerializeField] private string interactionPrompt = "F키로 줍기";

    [Header("Physics Settings")]
    [SerializeField] private LayerMask groundLayer = 1 << 0; // Default layer (바닥)
    [SerializeField] private float ghostDelayTime = 1f; // 유령화까지 대기 시간
    
    private Rigidbody rb;
    private Collider col;
    private bool hasLanded = false;
    private bool canBecomeGhost = false; // 유령화 가능 플래그

    // IInteractable 구현
    public float InteractRange => interactRange;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        // ✅ 초기 상태 강제 설정 (프리팹 설정과 무관하게)
        if (rb != null)
        {
            rb.isKinematic = false;   // 물리 활성화
            rb.useGravity = true;     // 중력 활성화
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        if (col != null)
        {
            col.isTrigger = false;  // 충돌 활성화
        }

        // 플래그 초기화
        canBecomeGhost = false;
        hasLanded = false;
    }

    /// <summary>
    /// 아이템 데이터 설정 (드롭 시 ItemDropManager에서 호출)
    /// </summary>
    public void SetItemData(ItemData data, int count)
    {
        itemData = data;
        amount = count;
        
        // 오브젝트 이름 업데이트 (디버깅 편의)
        if (data != null)
            gameObject.name = $"{data.name} x{count}";

        // ✅ 플래그 리셋 (재사용 대비)
        canBecomeGhost = false;
        hasLanded = false;

        // ✅ 일정 시간 후 유령화 허용
        StartCoroutine(EnableGhostModeAfterDelay());
    }

    /// <summary>
    /// 지정된 시간 후 유령화 가능 상태로 전환
    /// </summary>
    private System.Collections.IEnumerator EnableGhostModeAfterDelay()
    {
        yield return new WaitForSeconds(ghostDelayTime);
        canBecomeGhost = true;
    }

    public string GetInteractionPrompt()
    {
        if (itemData != null)
            return $"[F] {itemData.name} x{amount} 줍기";
        
        return interactionPrompt;
    }

    public Transform GetTransform()
    {
        return transform;
    }

    /// <summary>
    /// 플레이어가 F키로 상호작용 시도
    /// </summary>
    public void Interact(Transform player)
    {
        // ✅ 예외 처리 1: 거리 체크
        float distance = Vector3.Distance(player.position, transform.position);
        if (distance > interactRange)
        {
            Debug.Log($"[PickupableItem] ⚠️ 너무 멀리 있음: {distance:F1}m > {interactRange}m");
            return;
        }

        // ✅ 예외 처리 2: ItemData 검증
        if (itemData == null)
        {
            Debug.LogError("[PickupableItem] ❌ itemData가 null입니다. SetItemData()를 호출하지 않았거나 데이터가 손상되었습니다.");
            return;
        }

        if (InventoryController.Instance == null)
        {
            Debug.LogError("[PickupableItem] InventoryController가 없습니다.");
            return;
        }

        int remain = InventoryController.Instance.AddToInventory(itemData, amount);
        int picked = amount - remain;

        if (picked > 0)
            Debug.Log($"[PickupableItem] '{itemData.name}' x{picked} 획득");

        if (remain <= 0)
        {
            Destroy(gameObject);
            return;
        }

        amount = remain;
        gameObject.name = $"{itemData.name} x{amount}";
        Debug.LogWarning($"[PickupableItem] 인벤이 가득 차 '{itemData.name}' x{amount}은 바닥에 남습니다.");
    }

    /// <summary>
    /// 바닥에 닿는 순간 감지 -> 유령 오브젝트로 전환 (1초 후부터만)
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        // 이미 착지했으면 무시
        if (hasLanded)
            return;

        // ✅ 유령화 가능 시간이 지나지 않았으면 무시 (물리 계속 작동)
        if (!canBecomeGhost)
            return;

        // 바닥 레이어와 충돌했는지 확인
        if (((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            hasLanded = true;
            MakeGhostObject();
        }
    }

    /// <summary>
    /// 유령 오브젝트로 전환: 물리 정지 + 충돌 비활성화
    /// </summary>
    private void MakeGhostObject()
    {
        // ✅ 1. Rigidbody를 Kinematic으로 전환 -> 물리 시뮬레이션 정지
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // ✅ 2. Collider를 Trigger로 전환 -> 충돌 없이 통과 가능
        if (col != null)
        {
            col.isTrigger = true;
        }

        // ✅ 3. 회전 완전히 고정 (혹시 모를 잔여 회전 방지)
        transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
    }

    /// <summary>
    /// Scene View에서 상호작용 범위 시각화
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = hasLanded ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
