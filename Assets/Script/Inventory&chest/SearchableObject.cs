using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 뒤질 수 있는 상자. 칸 데이터는 이 오브젝트의 <see cref="Model"/>에만 있다.
/// </summary>
public abstract class SearchableObject : MonoBehaviour, IInteractable
{
    [Header("Search Settings")]
    public int minItemCount = 1;
    public int maxItemCount = 10;

    [SerializeField, Min(1), Tooltip("이 상자의 칸 수. 생성된 아이템이 이보다 많으면 들어가지 못한 만큼 경고한다.")]
    private int capacity = 10;

    public float interactRange = 3.0f;

    public List<ItemData> possibleItems;

    protected List<ItemData> generatedItems;

    /// <summary>이 상자의 칸. 처음 열기 전에 만들면 null.</summary>
    public InventoryModel Model { get; private set; }

    /// <inheritdoc />
    public float InteractRange => interactRange;

    /// <summary>플레이어에게 보이는 상호작용 문구.</summary>
    public string GetInteractionPrompt()
    {
        return "[F] 열기";
    }

    /// <inheritdoc />
    public Transform GetTransform()
    {
        return transform;
    }

    /// <summary>상자 종류에 맞는 아이템 목록. 칸에 넣는 일은 이 클래스가 한다.</summary>
    public virtual List<ItemData> GenerateItems()
    {
        int count = Random.Range(minItemCount, maxItemCount + 1);
        generatedItems = new List<ItemData>();

        for (int i = 0; i < count; i++)
        {
            ItemData item = possibleItems[Random.Range(0, possibleItems.Count)];
            generatedItems.Add(item);
        }

        return generatedItems;
    }

    /// <summary>모델이 없으면 만들고, 생성된 아이템을 넣는다. 이미 있으면 그대로 둔다.</summary>
    public void EnsureModel()
    {
        if (Model != null)
            return;

        int slotCount = capacity;
        if (slotCount < 1)
        {
            Debug.LogWarning($"[SearchableObject] '{name}' capacity {capacity}는 1로 맞춥니다.", this);
            slotCount = 1;
        }

        Model = new InventoryModel(slotCount);
        List<ItemData> items = GenerateItems();
        if (items == null)
            return;

        for (int i = 0; i < items.Count; i++)
        {
            ItemData item = items[i];
            if (item == null)
                continue;

            int remain = Model.AddItem(item, 1);
            if (remain > 0)
            {
                Debug.LogWarning(
                    $"[SearchableObject] '{name}' 칸({Model.Capacity})이 부족해 '{item.name}'을 넣지 못했습니다. capacity와 maxItemCount를 확인하세요.",
                    this);
            }
        }
    }

    /// <inheritdoc />
    public void Interact(Transform player)
    {
        TryInteract(player);
    }

    /// <summary>거리 안이면 모델을 준비한 뒤 상자 창을 연다.</summary>
    public void TryInteract(Transform player)
    {
        if (player == null || Vector3.Distance(player.position, transform.position) > interactRange)
        {
            Debug.Log("[SearchableObject] 상자가 너무 멉니다.");
            return;
        }

        EnsureModel();
        if (UIManager.Instance == null)
        {
            Debug.LogWarning("[SearchableObject] UIManager가 없습니다.", this);
            return;
        }

        UIManager.Instance.OpenChest(this);
    }
}
