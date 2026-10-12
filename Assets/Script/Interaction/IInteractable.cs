using UnityEngine;

/// <summary>
/// 플레이어가 상호작용할 수 있는 모든 오브젝트의 공통 인터페이스
/// SearchableObject(상자)와 PickupableItem(아이템) 모두 구현
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// 플레이어와 상호작용 시도
    /// </summary>
    /// <param name="player">플레이어 Transform</param>
    void Interact(Transform player);

    /// <summary>
    /// 상호작용 가능 거리
    /// </summary>
    float InteractRange { get; }

    /// <summary>
    /// 상호작용 UI에 표시될 텍스트 (예: "F키로 열기", "F키로 줍기")
    /// </summary>
    string GetInteractionPrompt();

    /// <summary>
    /// 오브젝트의 Transform (거리 계산용)
    /// </summary>
    Transform GetTransform();
}
