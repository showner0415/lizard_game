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

    [SerializeField, Min(0.05f), Tooltip("채널링 중 꼬리 1개를 소모하는 간격(초).")]
    float _channelTailInterval = 1f;

    bool _isCharging;
    float _chargeStartTime;
    ChannelEffect _channelEffect;
    float _channelTailTimer;

    void Awake()
    {
        if (playerWeapon == null)
            playerWeapon = GetComponent<PlayerWeapon>();

        if (playerWeapon == null)
            Debug.LogWarning("[PlayerAttack] PlayerWeapon을 찾지 못함 → 시전할 수 없습니다.", this);

        if (playerMana == null)
            playerMana = GetComponent<PlayerMana>();

        if (playerMana == null)
            Debug.LogWarning("[PlayerAttack] PlayerMana를 찾지 못함 → 마나 비용 없이 시전됩니다.", this);

        if (float.IsNaN(_channelTailInterval) || _channelTailInterval < 0.05f)
            _channelTailInterval = 1f;
    }

    void OnDisable()
    {
        StopChannelEffect();
        if (_isCharging)
            CancelCharge();
    }

    void Update()
    {
        if (_isCharging && currentMagic != null && CastingBarController.Instance != null)
        {
            float heldTime = Time.time - _chargeStartTime;
            CastingBarController.Instance.UpdateFill(heldTime, currentMagic.castingTime);
        }

        if (_channelEffect != null)
            TickChannelTail();
    }

    public void OnFireStarted(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        if (UIManager.Instance != null && UIManager.Instance.IsAnyWindowOpen)
            return;

        if (_isCharging || _channelEffect != null)
            return;

        if (HotbarController.Instance == null)
        {
            Debug.LogWarning("[PlayerAttack] HotbarController.Instance가 null입니다.");
            return;
        }

        currentMagic = HotbarController.Instance.CurrentMagic;
        if (currentMagic == null)
            return;

        switch (currentMagic.Mode)
        {
            case CastMode.Channel:
                BeginChannelCast();
                break;
            case CastMode.Instant:
                CastInstant();
                break;
            default:
                BeginCharge();
                break;
        }
    }

    public void OnFireCanceled(InputAction.CallbackContext ctx)
    {
        if (!ctx.canceled || currentMagic == null)
            return;

        switch (currentMagic.Mode)
        {
            case CastMode.Channel:
                EndChannel();
                break;
            case CastMode.Instant:
                break;
            default:
                EndCharge();
                break;
        }
    }

    void BeginCharge()
    {
        if (!HasWeapon() || !CanPayMana())
            return;

        if (_spellCaster != null && !_spellCaster.TryBeginCast(currentMagic.TailCost))
        {
            Debug.Log("사용 가능한 꼬리가 없어 캐스팅할 수 없습니다.");
            return;
        }

        _isCharging = true;
        _chargeStartTime = Time.time;
        if (CastingBarController.Instance != null)
            CastingBarController.Instance.Show();
    }

    void EndCharge()
    {
        if (!_isCharging)
            return;

        _isCharging = false;
        float heldTime = Time.time - _chargeStartTime;
        if (currentMagic == null || heldTime < currentMagic.castingTime)
        {
            Debug.Log("캐스팅 실패 (너무 빨리 뗌)");
            CancelChargeReservation();
            if (CastingBarController.Instance != null)
                CastingBarController.Instance.Cancel();
            return;
        }

        bool fired = false;
        if (raycaster != null && raycaster.TryGetMouseWorldPosition(out Vector3 target) && HasWeapon() && TryPayMana())
        {
            currentMagic.Cast(gameObject, target);
            fired = true;
        }

        if (_spellCaster != null)
        {
            if (fired)
                _spellCaster.CompleteCast();
            else
                _spellCaster.CancelCast();
        }

        if (CastingBarController.Instance != null)
            CastingBarController.Instance.Hide();
    }

    void BeginChannelCast()
    {
        if (!HasWeapon() || !CanPayMana())
            return;

        if (currentMagic is not ChannelMagicData channel)
        {
            Debug.LogWarning("[PlayerAttack] 채널링 마법이 아닙니다.", currentMagic);
            return;
        }

        if (_spellCaster == null || !_spellCaster.TryConsumeNow(1))
        {
            Debug.Log("사용 가능한 꼬리가 없어 채널링을 시작할 수 없습니다.");
            return;
        }

        if (!TryPayMana())
            return;

        ChannelEffect effect = channel.BeginChannel(gameObject, raycaster);
        if (effect == null)
            return;

        _channelEffect = effect;
        _channelTailTimer = 0f;
    }

    void EndChannel()
    {
        StopChannelEffect();
    }

    void TickChannelTail()
    {
        _channelTailTimer += Time.deltaTime;
        if (_channelTailTimer < _channelTailInterval)
            return;

        _channelTailTimer -= _channelTailInterval;
        if (_spellCaster == null || _spellCaster.TryConsumeNow(1))
            return;

        Debug.Log("꼬리가 없어 채널링을 끝냅니다.");
        EndChannel();
    }

    void CastInstant()
    {
        if (!HasWeapon() || !CanPayMana())
            return;

        if (raycaster == null || !raycaster.TryGetMouseWorldPosition(out Vector3 target))
            return;

        if (!TryPayMana())
            return;

        currentMagic.Cast(gameObject, target);
    }

    void CancelCharge()
    {
        _isCharging = false;
        CancelChargeReservation();
        if (CastingBarController.Instance != null)
            CastingBarController.Instance.Cancel();
    }

    void CancelChargeReservation()
    {
        if (_spellCaster != null)
            _spellCaster.CancelCast();
    }

    void StopChannelEffect()
    {
        ChannelEffect effect = _channelEffect;
        _channelEffect = null;
        _channelTailTimer = 0f;
        if (effect != null)
            effect.Stop();
    }

    bool HasWeapon()
    {
        if (playerWeapon != null && playerWeapon.CurrentWeapon != null)
            return true;

        Debug.Log("무기가 없어 시전할 수 없습니다.");
        return false;
    }

    bool CanPayMana()
    {
        if (playerMana == null || currentMagic == null)
            return true;

        if (playerMana.CanAfford(currentMagic.ManaCost))
            return true;

        Debug.Log($"마나가 부족해 캐스팅할 수 없습니다. ({playerMana.CurrentMana}/{currentMagic.ManaCost})");
        return false;
    }

    bool TryPayMana()
    {
        if (playerMana == null || currentMagic == null)
            return true;

        if (playerMana.TrySpendMana(currentMagic.ManaCost))
            return true;

        Debug.Log("마나가 부족해 시전이 취소되었습니다.");
        return false;
    }
}
