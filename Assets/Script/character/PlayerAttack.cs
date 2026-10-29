using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class PlayerAttack : MonoBehaviour, IHomeRestorable
{
    [Header("Magic")]
    public MagicData currentMagic;
    public Transform firePoint;
    public float magicSpeed = 10f;
    public MouseRayCaster raycaster;

    [Header("Scale Cost (optional)")]
    [Tooltip("If assigned, each cast reserves a scale up front and consumes it only when the cast completes.")]
    [FormerlySerializedAs("_spellCaster")]
    [SerializeField] private ScaleSpender _scaleSpender;

    [Tooltip("발사와 장전이 겹치지 않게 잠근다. 비우면 같은 오브젝트와 부모에서 찾음")]
    [SerializeField] private PlayerActionGate _actionGate;

    [Header("Weapon")]
    [Tooltip("무기칸 조회. 비우면 같은 오브젝트에서 찾음. 없으면 시전하지 않음")]
    [SerializeField] private PlayerWeapon playerWeapon;

    [Header("Mana Cost")]
    [Tooltip("시전 비용을 지불할 마나. 비우면 같은 오브젝트에서 찾음")]
    [SerializeField] private PlayerMana playerMana;

    [FormerlySerializedAs("_channelTailInterval")]
    [SerializeField, Min(0.05f), Tooltip("채널링 중 비늘 1개를 소모하는 간격(초).")]
    float _channelScaleInterval = 1f;

    bool _isCharging;
    bool _chargeReservedScale;
    float _chargeStartTime;
    ChannelEffect _channelEffect;
    float _channelScaleTimer;

    /// <summary>차지(캐스팅) 중이거나 채널링 중이면 true.</summary>
    public bool IsCasting => _isCharging || _channelEffect != null;

    void Awake()
    {
        if (_actionGate == null)
            _actionGate = GetComponent<PlayerActionGate>();

        if (_actionGate == null)
            _actionGate = GetComponentInParent<PlayerActionGate>();

        if (_actionGate == null)
            Debug.LogWarning("[PlayerAttack] PlayerActionGate를 찾지 못함 → 시전이 장전과 겹칠 수 있습니다.", this);

        if (playerWeapon == null)
            playerWeapon = GetComponent<PlayerWeapon>();

        if (playerWeapon == null)
            Debug.LogWarning("[PlayerAttack] PlayerWeapon을 찾지 못함 → 시전할 수 없습니다.", this);

        if (playerMana == null)
            playerMana = GetComponent<PlayerMana>();

        if (playerMana == null)
            Debug.LogWarning("[PlayerAttack] PlayerMana를 찾지 못함 → 마나 비용 없이 시전됩니다.", this);

        if (float.IsNaN(_channelScaleInterval) || _channelScaleInterval < 0.05f)
            _channelScaleInterval = 1f;
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
            CastingBarController.Instance.UpdateFill(this, heldTime, currentMagic.castingTime);
        }

        if (_channelEffect != null)
            TickChannelScale();
    }

    public void OnFireStarted(InputAction.CallbackContext context)
    {
        if (!isActiveAndEnabled)
            return;

        if (!context.performed)
            return;

        if (UIManager.Instance != null && UIManager.Instance.IsAnyWindowOpen)
            return;

        if (_actionGate != null && _actionGate.Current == PlayerAction.Loading && !_actionGate.RequestRelease())
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
        if (!isActiveAndEnabled)
            return;

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
        if (!HasWeapon() || !CanCastCurrent() || !CanPayMana())
            return;

        if (!TryEnterAction(PlayerAction.Throwing))
            return;

        _chargeReservedScale = false;
        if (currentMagic.ScaleCost > 0 && _scaleSpender != null && !_scaleSpender.TryBeginCast(currentMagic.ScaleCost))
        {
            Debug.Log("사용 가능한 비늘이 없어 캐스팅할 수 없습니다.");
            ExitAction(PlayerAction.Throwing);
            return;
        }

        _chargeReservedScale = currentMagic.ScaleCost > 0 && _scaleSpender != null;
        _isCharging = true;
        _chargeStartTime = Time.time;
        if (CastingBarController.Instance != null)
            CastingBarController.Instance.Show(this);
    }

    void EndCharge()
    {
        if (!_isCharging)
            return;

        _isCharging = false;
        try
        {
            float heldTime = Time.time - _chargeStartTime;
            if (currentMagic == null || heldTime < currentMagic.castingTime)
            {
                Debug.Log("캐스팅 실패 (너무 빨리 뗌)");
                CancelChargeReservation();
                return;
            }

            if (currentMagic != null && !currentMagic.CanCast(gameObject))
            {
                CancelChargeReservation();
                return;
            }

            // 비늘 소모를 먼저 확정한다. 확정에 실패하면 발사하지 않는다.
            bool canFire = TryGetCastTarget(out Vector3 target) && HasWeapon() && CanPayMana();
            if (canFire && !ConfirmChargeReservation())
            {
                Debug.Log("비늘 예약이 풀려 시전이 취소되었습니다.");
                canFire = false;
            }

            if (canFire && TryPayMana())
                currentMagic.Cast(gameObject, target);
            else
                CancelChargeReservation();
        }
        finally
        {
            if (CastingBarController.Instance != null)
                CastingBarController.Instance.Hide(this);
            ExitAction(PlayerAction.Throwing);
        }
    }

    void BeginChannelCast()
    {
        if (!HasWeapon() || !CanCastCurrent() || !CanPayMana())
            return;

        if (currentMagic is not ChannelMagicData channel)
        {
            Debug.LogWarning("[PlayerAttack] 채널링 마법이 아닙니다.", currentMagic);
            return;
        }

        if (!TryEnterAction(PlayerAction.Throwing))
            return;

        if (_scaleSpender == null || !_scaleSpender.TryConsumeNow(1))
        {
            Debug.Log("사용 가능한 비늘이 없어 채널링을 시작할 수 없습니다.");
            ExitAction(PlayerAction.Throwing);
            return;
        }

        if (!TryPayMana())
        {
            ExitAction(PlayerAction.Throwing);
            return;
        }

        ChannelEffect effect = channel.BeginChannel(gameObject, raycaster);
        if (effect == null)
        {
            ExitAction(PlayerAction.Throwing);
            return;
        }

        _channelEffect = effect;
        _channelScaleTimer = 0f;
    }

    void EndChannel()
    {
        StopChannelEffect();
    }

    void TickChannelScale()
    {
        _channelScaleTimer += Time.deltaTime;
        if (_channelScaleTimer < _channelScaleInterval)
            return;

        _channelScaleTimer -= _channelScaleInterval;
        if (_scaleSpender == null || _scaleSpender.TryConsumeNow(1))
            return;

        Debug.Log("비늘이 없어 채널링을 끝냅니다.");
        EndChannel();
    }

    void CastInstant()
    {
        if (!HasWeapon() || !CanCastCurrent() || !CanPayMana())
            return;

        if (!TryGetCastTarget(out Vector3 target))
            return;

        if (!TryPayMana())
            return;

        currentMagic.Cast(gameObject, target);
    }

    /// <inheritdoc />
    public void RestoreForHome()
    {
        if (_isCharging)
            CancelCharge();

        if (_channelEffect != null)
            StopChannelEffect();

        ExitAction(PlayerAction.Throwing);
    }

    void CancelCharge()
    {
        _isCharging = false;
        CancelChargeReservation();
        if (CastingBarController.Instance != null)
            CastingBarController.Instance.Cancel(this);
        ExitAction(PlayerAction.Throwing);
    }

    /// <summary>차지 예약이 있으면 소모를 확정한다. 예약이 없으면(비용 0) true, 확정에 실패하면 false.</summary>
    bool ConfirmChargeReservation()
    {
        if (!_chargeReservedScale)
            return true;

        _chargeReservedScale = false;
        return _scaleSpender != null && _scaleSpender.CompleteCast();
    }

    void CancelChargeReservation()
    {
        if (!_chargeReservedScale || _scaleSpender == null)
            return;

        _chargeReservedScale = false;
        _scaleSpender.CancelCast();
    }

    void StopChannelEffect()
    {
        ChannelEffect effect = _channelEffect;
        _channelEffect = null;
        _channelScaleTimer = 0f;
        if (effect == null)
            return;

        effect.Stop();
        ExitAction(PlayerAction.Throwing);
    }

    bool TryEnterAction(PlayerAction action)
    {
        if (_actionGate == null)
            return true;

        return _actionGate.TryEnter(action);
    }

    void ExitAction(PlayerAction action)
    {
        if (_actionGate != null)
            _actionGate.Exit(action);
    }

    bool TryGetCastTarget(out Vector3 target)
    {
        if (raycaster != null && raycaster.TryGetMouseWorldPosition(out target))
            return true;

        if (currentMagic != null && !currentMagic.NeedsTargetPoint)
        {
            target = transform.position;
            return true;
        }

        target = default;
        return false;
    }

    bool CanCastCurrent()
    {
        return currentMagic != null && currentMagic.CanCast(gameObject);
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
