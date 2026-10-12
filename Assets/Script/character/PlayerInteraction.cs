using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// 플레이어 상호작용 시스템 (통합)
/// SearchableObject(상자)와 PickupableItem(아이템) 모두 처리
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    public static PlayerInteraction Instance;

    [Header("Interaction Settings")]
    [SerializeField, Min(0.5f), Tooltip("OverlapSphere 반경. 가장 먼 상호작용 거리보다 조금 크게.")]
    private float searchRange = 3f;
    [SerializeField] private LayerMask interactableLayer;

    [Header("UI")]
    public GameObject indicatorPrefab;

    // 통합 상호작용 타겟
    private IInteractable currentTarget;
    private IInteractable previousTarget;

    // 최적화용 버퍼
    private readonly List<IInteractable> candidates = new();
    private Collider[] overlapBuffer = new Collider[32];
    private GameObject currentIndicator;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (currentIndicator != null)
            Destroy(currentIndicator);
    }

    private void Update()
    {
        UpdateClosestInteractable();
        UpdateIndicatorIfNeeded();

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            TryInteract();
        }
    }

    /// <summary>
    /// F키로 상호작용 시도
    /// </summary>
    private void TryInteract()
    {
        if (currentTarget == null)
        {
            Debug.Log("[PlayerInteraction] 주변에 상호작용 가능한 오브젝트 없음");
            return;
        }

        // ✅ 단일 타겟만 상호작용 (충돌 방지)
        currentTarget.Interact(transform);
    }

    /// <summary>
    /// 가장 가까운 상호작용 가능 오브젝트 찾기 (거리 기반 단일 타겟팅)
    /// </summary>
    private void UpdateClosestInteractable()
    {
        // OverlapSphere로 범위 내 모든 Collider 감지
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, searchRange, overlapBuffer, interactableLayer);

        candidates.Clear();

        // IInteractable 컴포넌트 수집
        for (int i = 0; i < count; i++)
        {
            // SearchableObject 체크
            if (overlapBuffer[i].TryGetComponent(out SearchableObject searchable))
            {
                candidates.Add(searchable);
            }
            // PickupableItem 체크
            else if (overlapBuffer[i].TryGetComponent(out PickupableItem pickupable))
            {
                candidates.Add(pickupable);
            }
        }

        // SearchableUI가 열려있는데 주변에 아무것도 없으면 닫기
        if (candidates.Count == 0 && UIManager.Instance != null && UIManager.Instance.IsChestOpen)
        {
            UIManager.Instance.CloseAll();

            currentTarget = null;
            return;
        }

        // ✅ 가장 가까운 1개만 타겟으로 설정 (충돌 방지)
        currentTarget = GetClosestInteractable();
    }

    /// <summary>
    /// candidates 중 가장 가까운 오브젝트 반환
    /// </summary>
    private IInteractable GetClosestInteractable()
    {
        if (candidates.Count == 0)
            return null;

        IInteractable closest = null;
        float minDistSq = float.MaxValue;

        Vector3 playerPos = transform.position;

        foreach (var candidate in candidates)
        {
            if (candidate == null)
                continue;

            Transform candidateTransform = candidate.GetTransform();
            if (candidateTransform == null)
                continue;

            // 거리 제곱 계산 (Sqrt 회피)
            float distSq = Vector3.SqrMagnitude(candidateTransform.position - playerPos);

            // 상호작용 범위 내인지 체크
            float range = candidate.InteractRange;
            if (distSq > range * range)
                continue;

            if (distSq < minDistSq)
            {
                minDistSq = distSq;
                closest = candidate;
            }
        }

        return closest;
    }

    /// <summary>
    /// 인디케이터 업데이트 (타겟 변경 시에만)
    /// </summary>
    private void UpdateIndicatorIfNeeded()
    {
        // 인벤토리가 열려있으면 인디케이터 숨기기
        if (UIManager.Instance != null && UIManager.Instance.IsAnyWindowOpen)
        {
            HideIndicator();
            previousTarget = null;
            return;
        }

        if (currentTarget == previousTarget)
        {
            if (currentTarget != null)
                MoveIndicator(currentTarget);
            return;
        }

        if (currentTarget != null)
            MoveIndicator(currentTarget);
        else
            HideIndicator();

        previousTarget = currentTarget;
    }

    void MoveIndicator(IInteractable target)
    {
        if (indicatorPrefab == null)
            return;

        Transform targetTransform = target.GetTransform();
        if (targetTransform == null)
            return;

        if (currentIndicator == null)
            currentIndicator = Instantiate(indicatorPrefab);

        Vector3 worldPos = targetTransform.position;
        worldPos.y += 1.5f;
        worldPos.x += 1f;
        currentIndicator.SetActive(true);
        currentIndicator.transform.position = worldPos;
    }

    void HideIndicator()
    {
        if (currentIndicator != null)
            currentIndicator.SetActive(false);
    }

    /// <summary>
    /// 현재 타겟 가져오기 (다른 시스템에서 사용)
    /// </summary>
    public IInteractable GetCurrentTarget()
    {
        return currentTarget;
    }

    private void OnDrawGizmosSelected()
    {
        // 상호작용 범위 시각화
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, searchRange);
    }
}
