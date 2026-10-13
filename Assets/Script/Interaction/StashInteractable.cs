using UnityEngine;

/// <summary>
/// 거점 창고를 연다. 칸 데이터는 들고 있지 않다. 창고 칸은 <see cref="InventoryController.Stash"/>다.
/// </summary>
public sealed class StashInteractable : MonoBehaviour, IInteractable
{
    const float MinRange = 0.5f;

    [SerializeField, Min(MinRange), Tooltip("이 거리 안에서 F로 창고를 연다. 벗어나면 창이 닫힌다.")]
    float _interactRange = 3f;

    /// <inheritdoc />
    public float InteractRange => _interactRange;

    void Awake()
    {
        if (float.IsNaN(_interactRange) || float.IsInfinity(_interactRange) || _interactRange < MinRange)
        {
            Debug.LogWarning($"[StashInteractable] interactRange {_interactRange}는 {MinRange}로 맞춥니다.", this);
            _interactRange = MinRange;
        }
    }

    /// <inheritdoc />
    public Transform GetTransform()
    {
        return transform;
    }

    /// <inheritdoc />
    public string GetInteractionPrompt()
    {
        return "[F] 창고";
    }

    /// <inheritdoc />
    public void Interact(Transform player)
    {
        if (player == null)
            return;

        float range = InteractRange;
        Vector3 delta = player.position - transform.position;
        if (delta.sqrMagnitude > range * range)
            return;

        if (UIManager.Instance == null)
        {
            Debug.LogWarning("[StashInteractable] UIManager가 없습니다.", this);
            return;
        }

        UIManager.Instance.OpenStash(this);
    }
}
