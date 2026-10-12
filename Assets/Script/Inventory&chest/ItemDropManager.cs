using UnityEngine;

/// <summary>
/// 아이템을 3D 월드에 드롭하는 중앙 관리자
/// </summary>
public class ItemDropManager : MonoBehaviour
{
    public static ItemDropManager Instance { get; private set; }

    [Header("Drop Settings")]
    [SerializeField] private float dropDistance = 1.5f;
    [SerializeField] private float dropForce = 3f;
    [SerializeField] private float dropUpwardForce = 1f;
    [SerializeField] private LayerMask groundLayer = ~0;

    [Header("Safety")]
    [SerializeField] private float maxDropHeight = 2f;
    [SerializeField] private float raycastDistance = 5f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// 아이템을 플레이어 앞 바닥에 드롭한다. 월드에 생겼으면 true.
    /// </summary>
    /// <param name="itemData">드롭할 아이템 데이터</param>
    /// <param name="amount">드롭할 수량</param>
    /// <param name="playerTransform">플레이어 Transform (null이면 카메라 기준)</param>
    public bool DropItem(ItemData itemData, int amount, Transform playerTransform = null)
    {
        if (itemData == null)
        {
            Debug.LogWarning("[ItemDropManager] ⚠️ itemData가 null입니다. 드롭을 취소합니다.");
            return false;
        }

        if (itemData.worldPrefab == null)
        {
            Debug.LogWarning($"[ItemDropManager] ⚠️ '{itemData.name}'의 worldPrefab이 설정되지 않았습니다. " +
                $"ItemData Inspector에서 World Drop 섹션의 worldPrefab을 할당하세요.");
            return false;
        }

        if (amount <= 0)
        {
            Debug.LogWarning($"[ItemDropManager] ⚠️ 드롭 수량이 0 이하입니다: {amount}");
            return false;
        }

        if (!TryGetSafeDropPosition(playerTransform, out Vector3 dropPosition))
        {
            Debug.LogWarning("[ItemDropManager] 안전한 드롭 위치를 찾을 수 없습니다.");
            return false;
        }

        // 아이템 생성
        GameObject droppedItem = Instantiate(itemData.worldPrefab, dropPosition, Quaternion.identity);
        
        // 아이템 이름 설정 (디버깅 편의)
        droppedItem.name = $"{itemData.name} x{amount}";

        // ✅ PickupableItem 컴포넌트에 데이터 설정
        PickupableItem pickupable = droppedItem.GetComponent<PickupableItem>();
        if (pickupable != null)
        {
            pickupable.SetItemData(itemData, amount);
        }
        else
        {
            Debug.LogWarning($"[ItemDropManager] ⚠️ '{itemData.name}' 프리팹에 PickupableItem 컴포넌트가 없습니다. " +
                $"F키로 줍기 기능이 작동하지 않습니다.");
        }

        // ✅ Rigidbody가 있으면 물리 효과 추가
        Rigidbody rb = droppedItem.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // 🔧 강제 초기화: 프리팹 설정 무시하고 물리 활성화
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            
            // 회전만 잠금 (위치는 자유롭게)
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            Vector3 forceDirection = playerTransform != null ? playerTransform.forward : Vector3.forward;
            Vector3 force = forceDirection * dropForce + Vector3.up * dropUpwardForce;
            rb.AddForce(force, ForceMode.Impulse);
        }
        else
        {
            Debug.LogWarning($"[ItemDropManager] ⚠️ '{itemData.name}' 프리팹에 Rigidbody가 없습니다!");
        }

        // ✅ Collider 초기화
        Collider col = droppedItem.GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = false; // 충돌 활성화
        }

        return true;
    }

    /// <summary>
    /// 플레이어 앞 바닥 위치를 구한다. 실패는 위치 값과 따로 bool로 알린다.
    /// </summary>
    bool TryGetSafeDropPosition(Transform playerTransform, out Vector3 position)
    {
        position = default;
        Vector3 origin;
        Vector3 forward;

        if (playerTransform != null)
        {
            origin = playerTransform.position;
            forward = playerTransform.forward;
        }
        else if (Camera.main != null)
        {
            origin = Camera.main.transform.position;
            forward = Camera.main.transform.forward;
        }
        else
        {
            Debug.LogError("[ItemDropManager] 플레이어도 카메라도 없습니다.");
            return false;
        }

        // 플레이어 앞쪽으로 dropDistance만큼 이동
        Vector3 targetPosition = origin + forward * dropDistance;
        
        // 높이 조정: 플레이어보다 약간 위에서 시작
        targetPosition.y = origin.y + 0.5f;

        // 아래쪽으로 Raycast하여 바닥 찾기
        Ray ray = new Ray(targetPosition, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, groundLayer))
        {
            // 바닥을 찾았으면 그 위치에서 살짝 위로
            Vector3 dropPos = hit.point + Vector3.up * 0.1f;

            // 너무 높은 곳에 떨어지는 것 방지
            if (Mathf.Abs(dropPos.y - origin.y) > maxDropHeight)
            {
                Debug.LogWarning("[ItemDropManager] ⚠️ 드롭 위치가 플레이어와 높이 차이가 너무 큽니다. 플레이어 위치 사용.");
                dropPos.y = origin.y;
            }

            position = dropPos;
            return true;
        }

        Debug.LogWarning("[ItemDropManager] 바닥을 찾지 못했습니다. 기본 위치를 사용합니다.");
        targetPosition.y = origin.y;
        position = targetPosition;
        return true;
    }

    /// <summary>
    /// 여러 개의 아이템을 한번에 드롭 (스택 아이템용)
    /// </summary>
    /// <summary>스택이면 한 번에, 아니면 하나씩 드롭한다. 전부 생겼으면 true.</summary>
    public bool DropItemStack(ItemData itemData, int amount, Transform playerTransform = null)
    {
        if (itemData == null || amount <= 0)
            return false;

        if (itemData.Stackable)
            return DropItem(itemData, amount, playerTransform);

        for (int i = 0; i < amount; i++)
        {
            if (!DropItem(itemData, 1, playerTransform))
                return false;
        }

        return true;
    }
}
