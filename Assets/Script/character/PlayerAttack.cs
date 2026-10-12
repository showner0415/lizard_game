using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    [Header("Magic")]
    public MagicData currentMagic;
    public Transform firePoint;
    public float magicSpeed = 10f;
    public MouseRayCaster raycaster;

    [Header("Tail Cost (optional)")]
    [Tooltip("If assigned, each cast reserves a tail up front and consumes it only when the cast completes.")]
    [SerializeField] private SpellCaster _spellCaster;

    [Header("Weapon")]
    [Tooltip("무기칸 조회. 비우면 같은 오브젝트에서 찾음. 없으면 시전하지 않음")]
    [SerializeField] private PlayerWeapon playerWeapon;

    [Header("Mana Cost")]
    [Tooltip("시전 비용을 지불할 마나. 비우면 같은 오브젝트에서 찾음")]
    [SerializeField] private PlayerMana playerMana;

    private bool isCasting = false;
    private float castingStartTime;


    private Camera mainCam;

    void Awake()
    {
        mainCam = Camera.main;

        if (playerWeapon == null)
            playerWeapon = GetComponent<PlayerWeapon>();

        if (playerWeapon == null)
            Debug.LogWarning("[PlayerAttack] PlayerWeapon을 찾지 못함 → 시전할 수 없습니다.", this);

        if (playerMana == null)
            playerMana = GetComponent<PlayerMana>();

        if (playerMana == null)
            Debug.LogWarning("[PlayerAttack] PlayerMana를 찾지 못함 → 마나 비용 없이 시전됩니다.", this);
    }

    private void Update()
    {
        // 캐스팅 중일 때만 UI 게이지 업데이트
        if (isCasting && currentMagic != null)
        {
            float heldTime = Time.time - castingStartTime;
            CastingBarController.Instance.UpdateFill(heldTime, currentMagic.castingTime);
        }
    }

    // Input System에서 호출
    public void OnFireStarted(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        if (UIManager.Instance != null && UIManager.Instance.IsAnyWindowOpen)
            return;

        // ✅ HotbarController null 체크 추가
        if (HotbarController.Instance == null)
        {
            Debug.LogWarning("[PlayerAttack] HotbarController.Instance가 null입니다.");
            return;
        }

        currentMagic = HotbarController.Instance.CurrentMagic;
        if (currentMagic == null)
            return;

        Debug.Log("좌클릭 시작");
        StartCasting();
    }

    public void OnFireCanceled(InputAction.CallbackContext ctx)
    {
        if(!ctx.canceled) return;
        Debug.Log("좌클릭 끝");
        EndCasting();
    }

    void CastMagic(Vector3 targetPos)
    {
        if (currentMagic == null)
            return;

        currentMagic.Cast(gameObject, targetPos);
    }

    private void StartCasting()
    {
        if (isCasting)
            return;

        if (playerWeapon == null || playerWeapon.CurrentWeapon == null)
        {
            Debug.Log("무기가 없어 캐스팅할 수 없습니다.");
            return;
        }

        // 마나 부족 시 캐스팅 바/꼬리 예약 없이 바로 중단 (확인만, 차감은 발사 직전)
        if (playerMana != null && !playerMana.CanAfford(currentMagic.ManaCost))
        {
            Debug.Log($"마나가 부족해 캐스팅할 수 없습니다. ({playerMana.CurrentMana}/{currentMagic.ManaCost})");
            return;
        }

        // 꼬리 예약 실패 시 캐스팅 시작 불가
        if (_spellCaster != null && !_spellCaster.TryBeginCast())
        {
            Debug.Log("사용 가능한 꼬리가 없어 캐스팅할 수 없습니다.");
            return;
        }

        isCasting = true;
        castingStartTime = Time.time;

        // UI 표시 시작
        if (CastingBarController.Instance != null)
        {
            CastingBarController.Instance.Show();
        }

        // 이펙트, 사운드 시작 로직은 여기에 추가
    }
    private void EndCasting()
    {
        if (!isCasting)
            return;

        isCasting = false;

        float heldTime = Time.time - castingStartTime;

        if (heldTime < currentMagic.castingTime)
        {
            Debug.Log("캐스팅 실패 (너무 빨리 뗌)");

            // 예약 해제 (꼬리는 그대로 유지)
            if (_spellCaster != null)
                _spellCaster.CancelCast();

            // 캐스팅 실패 UI 처리
            if (CastingBarController.Instance != null)
            {
                CastingBarController.Instance.Cancel();
            }

            return;
        }

        // 마우스 위치 확인
        bool fired = false;
        if (raycaster.TryGetMouseWorldPosition(out Vector3 target))
        {
            if (playerWeapon == null || playerWeapon.CurrentWeapon == null)
            {
                Debug.Log("무기가 없어 시전이 취소되었습니다.");
            }
            else if (playerMana == null || playerMana.TrySpendMana(currentMagic.ManaCost))
            {
                CastMagic(target);
                fired = true;
            }
            else
            {
                Debug.Log("마나가 부족해 시전이 취소되었습니다.");
            }
        }

        // 마법이 실제로 발사된 뒤에만 꼬리를 소모 (실패 시 예약 해제)
        if (_spellCaster != null)
        {
            if (fired) _spellCaster.CompleteCast();
            else _spellCaster.CancelCast();
        }

        if (CastingBarController.Instance != null)
        {
            CastingBarController.Instance.Hide();
        }
    }

}