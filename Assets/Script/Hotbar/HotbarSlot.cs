using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 핫바 슬롯. 마법책을 받아 등록한다. 시전할 마법은 <see cref="Magic"/>이다.
/// </summary>
public class HotbarSlot : MonoBehaviour, IPointerClickHandler
{
    public Image backgroundImage;
    public Image iconImage;

    [SerializeField, Tooltip("인벤이 가득 차 등록이 거절될 때 칸이 바뀌는 색.")]
    private Color _rejectColor = new Color(1f, 0.25f, 0.25f, 1f);

    [SerializeField, Min(0.05f), Tooltip("거절 깜박임이 한 번 왕복하는 시간(초).")]
    private float _flashDuration = 0.35f;

    Coroutine _flashRoutine;
    Color _restColor = Color.white;

    /// <summary>이 칸에 올린 마법서. 비었으면 null.</summary>
    public MagicBookItemData book;

    /// <summary>시전할 마법. 마법서가 없거나 연결이 없으면 null.</summary>
    public MagicData Magic => book != null ? book.magic : null;

    /// <summary>마법서를 올리거나 칸을 비운다. 아이콘은 마법서에서 가져온다.</summary>
    public void SetBook(MagicBookItemData newBook)
    {
        book = newBook;

        if (book != null)
        {
            iconImage.sprite = book.icon;
            iconImage.enabled = book.icon != null;
        }
        else
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }
    }

    public void SetSelected(bool selected)
    {
        if (HotbarController.Instance == null)
            return;

        backgroundImage.sprite = selected
            ? HotbarController.Instance.selectedSprite
            : HotbarController.Instance.normalSprite;
    }

    /// <summary>마법서가 있으면 true.</summary>
    public bool HasMagic()
    {
        return Magic != null;
    }

    void Awake()
    {
        if (backgroundImage != null)
            _restColor = backgroundImage.color;
    }

    /// <summary>인벤에 넣지 못해 등록이 거절됐을 때 칸을 붉게 깜박인다.</summary>
    public void FlashReject()
    {
        if (!isActiveAndEnabled || backgroundImage == null)
            return;

        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRoutine());
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            SetBook(null);
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        MouseItemUI mouseItem = MouseItemUI.Instance;
        if (mouseItem == null || !mouseItem.HasItem())
            return;

        if (mouseItem.GetItem() is not MagicBookItemData bookToPlace)
            return;

        if (HotbarController.Instance == null)
        {
            Debug.LogWarning("[HotbarSlot] HotbarController.Instance가 null입니다.");
            return;
        }

        if (bookToPlace.magic == null)
        {
            Debug.LogWarning($"[HotbarSlot] '{bookToPlace.name}'에 연결된 마법이 없습니다. 마법서 에셋의 Magic 칸에 MagicData를 넣으세요.");
            return;
        }

        if (!ItemTransfer.TryStoreHeldInPlayerInventory())
        {
            FlashReject();
            return;
        }

        SetBook(bookToPlace);
    }

    IEnumerator FlashRoutine()
    {
        float half = _flashDuration * 0.5f;
        backgroundImage.color = _rejectColor;
        yield return new WaitForSeconds(half);
        backgroundImage.color = _restColor;
        yield return new WaitForSeconds(half * 0.5f);
        backgroundImage.color = _rejectColor;
        yield return new WaitForSeconds(half * 0.5f);
        backgroundImage.color = _restColor;
        _flashRoutine = null;
    }
}
