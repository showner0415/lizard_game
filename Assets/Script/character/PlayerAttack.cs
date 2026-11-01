using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class PlayerAttack : MonoBehaviour, IHomeRestorable, IDeathPenalty, IGateInterruptible
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

    [Header("Ornament")]
    [Tooltip("꼬리 장식에서 마법을 읽는다. 비우면 같은 오브젝트에서 찾음.")]
    [FormerlySerializedAs("playerWeapon")]
    [SerializeField] private PlayerOrnament _ornament;

    [Header("Mana Cost")]
    [Tooltip("시전 비용을 지불할 마나. 비우면 같은 오브젝트에서 찾음")]
    [SerializeField] private PlayerMana playerMana;

    [Tooltip("죽었으면 발사하지 않는다. 비우면 같은 오브젝트와 부모에서 찾는다.")]
    [SerializeField] private PlayerStats _playerStats;

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

    /// <summary>이번 삶에서 이미 죽었으면 true.</summary>
    bool IsDead => _playerStats != null && _playerStats.IsDead;

    void Awake()
    {
        if (_actionGate == null)
            _actionGate = GetComponent<PlayerActionGate>();

        if (_actionGate == null)
            _actionGate = GetComponentInParent<PlayerActionGate>();

        if (_actionGate == null)
            Debug.LogWarning("[PlayerAttack] PlayerActionGate를 찾지 못함 → 시전이 장전과 겹칠 수 있습니다.", this);

        if (_ornament == null)
            _ornament = GetComponent<PlayerOrnament>();

        if (_ornament == null)
            Debug.LogWarning("[PlayerAttack] PlayerOrnament를 찾지 못함 → 시전할 수 없습니다.", this);

        if (playerMana == null)
            playerMana = GetComponent<PlayerMana>();

        if (playerMana == null)
            Debug.LogWarning("[PlayerAttack] PlayerMana를 찾지 못함 → 마나 비용 없이 시전됩니다.", this);

        if (_playerStats == null)
            _playerStats = GetComponent<PlayerStats>();

        if (_playerStats == null)
            _playerStats = GetComponentInParent<PlayerStats>();

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
        if (_channelEffect != null)
            TickChannelScale();
    }

    /// <inheritdoc />
    public void InterruptByGate()
    {
        if (_isCharging)
            CancelCharge();

        if (_channelEffect != null)
            StopChannelEffect();
    }

    public void OnFireStarted(InputAction.CallbackContext context)
    {
        if (IsDead)
            return;

        if (!isActiveAndEnabled)
            return;

        if (!context.performed)
            return;

        if (UIManager.Instance != null && UIManager.Instance.IsAnyWindowOpen)
            return;

        if (_isCharging || _channelEffect != null)
            return;

        if (_ornament == null)
            return;

        MagicData magic = _ornament.CurrentMagic;
        if (magic == null)
            return;

        string failReason;
        if (!CanStartFire(magic, out failReason))
        {
            if (!string.IsNullOrEmpty(failReason))
                GameNotice.Show(failReason);
            return;
        }

        if (_actionGate != null && _actionGate.Current == PlayerAction.Loading)
            _actionGate.RequestRelease();

        currentMagic = magic;
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
        if (!CanCastCurrent(out _) || !CanPayMana())
            return;

        if (!TryEnterThrowing(currentMagic.castingTime))
        {
            GameNotice.Show(NoticeText.Busy);
            return;
        }

        _chargeReservedScale = false;
        if (currentMagic.ScaleCost > 0 && _scaleSpender != null && !_scaleSpender.TryBeginCast(currentMagic.ScaleCost))
        {
            GameNotice.Show(NoticeText.NotEnoughScales);
            ExitThrowing();
            return;
        }

        _chargeReservedScale = currentMagic.ScaleCost > 0 && _scaleSpender != null;
        _isCharging = true;
        _chargeStartTime = Time.time;
    }

    void EndCharge()
    {
        if (!_isCharging)
            return;

        _isCharging = false;
        try
        {
            if (IsDead)
            {
                CancelChargeReservation();
                return;
            }

            float heldTime = Time.time - _chargeStartTime;
            if (currentMagic == null || heldTime < currentMagic.castingTime)
            {
                CancelChargeReservation();
                return;
            }

            string castReason;
            if (!currentMagic.CanCast(gameObject, out castReason))
            {
                if (!string.IsNullOrEmpty(castReason))
                    GameNotice.Show(castReason);
                CancelChargeReservation();
                return;
            }

            if (!TryGetCastTarget(out Vector3 target))
            {
                CancelChargeReservation();
                return;
            }

            if (!CanPayMana())
            {
                GameNotice.Show(NoticeText.NotEnoughMana);
                CancelChargeReservation();
                return;
            }

            bool scalesReady = _scaleSpender == null || !_chargeReservedScale || _scaleSpender.CanCompleteCast();
            if (!scalesReady)
            {
                GameNotice.Show(NoticeText.NotEnoughScales);
                CancelChargeReservation();
                return;
            }

            if (!TryPayMana())
            {
                Debug.LogError("[PlayerAttack] 마나 확인 뒤에 지불에 실패했습니다.", this);
                CancelChargeReservation();
                return;
            }

            if (!ConfirmChargeReservation())
            {
                Debug.LogError("[PlayerAttack] 비늘 확인 뒤에 소모에 실패했습니다.", this);
                CancelChargeReservation();
                return;
            }

            currentMagic.Cast(gameObject, target);
        }
        finally
        {
            ExitThrowing();
        }
    }

    void BeginChannelCast()
    {
        if (!CanCastCurrent(out _) || !CanPayMana())
            return;

        if (currentMagic is not ChannelMagicData channel)
        {
            Debug.LogWarning("[PlayerAttack] 채널링 마법이 아닙니다.", currentMagic);
            return;
        }

        if (!HasLoadedScales(1))
        {
            GameNotice.Show(NoticeText.NotEnoughScales);
            return;
        }

        if (!TryEnterThrowing(0f))
        {
            GameNotice.Show(NoticeText.Busy);
            return;
        }

        ChannelEffect effect = channel.BeginChannel(gameObject, raycaster);
        if (effect == null)
        {
            ExitThrowing();
            return;
        }

        if (!TryPayMana() || _scaleSpender == null || !_scaleSpender.TryConsumeNow(1, !PlayerCostRules.IsFree))
        {
            Debug.LogError("[PlayerAttack] 확인 뒤에 비용 지불에 실패했습니다.", this);
            effect.Stop();
            ExitThrowing();
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
        if (_scaleSpender == null || _scaleSpender.TryConsumeNow(1, !PlayerCostRules.IsFree))
            return;

        GameNotice.Show(NoticeText.ScalesRanOut);
        EndChannel();
    }

    void CastInstant()
    {
        if (!CanCastCurrent(out _) || !CanPayMana())
            return;

        if (!TryGetCastTarget(out Vector3 target))
            return;

        int cost = currentMagic.ScaleCost;
        if (!HasLoadedScales(cost))
        {
            GameNotice.Show(NoticeText.NotEnoughScales);
            return;
        }

        if (!TryPayMana())
        {
            Debug.LogError("[PlayerAttack] 마나 확인 뒤에 지불에 실패했습니다.", this);
            return;
        }

        if (cost > 0 && (_scaleSpender == null || !_scaleSpender.TryConsumeNow(cost, !PlayerCostRules.IsFree)))
        {
            Debug.LogError("[PlayerAttack] 비늘 확인 뒤에 소모에 실패했습니다.", this);
            return;
        }

        currentMagic.Cast(gameObject, target);
    }

    /// <inheritdoc />
    public void ApplyDeathPenalty()
    {
        RestoreForHome();
    }

    /// <inheritdoc />
    public void RestoreForHome()
    {
        if (_isCharging)
            CancelCharge();

        if (_channelEffect != null)
            StopChannelEffect();

        ExitThrowing();
    }

    void CancelCharge()
    {
        _isCharging = false;
        CancelChargeReservation();
        ExitThrowing();
    }

    /// <summary>차지 예약이 있으면 소모를 확정한다. 예약이 없으면(비용 0) true, 확정에 실패하면 false.</summary>
    bool ConfirmChargeReservation()
    {
        if (!_chargeReservedScale)
            return true;

        _chargeReservedScale = false;
        return _scaleSpender != null && _scaleSpender.CompleteCast(!PlayerCostRules.IsFree);
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
        ExitThrowing();
    }

    bool TryEnterThrowing(float duration)
    {
        if (_actionGate == null)
            return true;

        return _actionGate.TryEnter(PlayerAction.Throwing, this, duration);
    }

    void ExitThrowing()
    {
        if (_actionGate != null)
            _actionGate.Exit(PlayerAction.Throwing, this);
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

    bool CanStartFire(MagicData magic, out string failReason)
    {
        failReason = null;
        if (magic == null)
            return false;

        string castReason;
        if (!magic.CanCast(gameObject, out castReason))
        {
            failReason = string.IsNullOrEmpty(castReason) ? NoticeText.CannotUseNow : castReason;
            return false;
        }

        if (!PlayerCostRules.IsFree && playerMana != null && !playerMana.CanAfford(magic.ManaCost))
        {
            failReason = NoticeText.NotEnoughMana;
            return false;
        }

        int required = magic.RequiredScalesToStart;
        if (required > 0)
        {
            IScaleMagazine magazine = null;
            if (_scaleSpender != null)
                magazine = _scaleSpender.Magazine;

            int loaded = magazine != null ? magazine.LoadedCount : 0;
            if (loaded < required)
            {
                failReason = NoticeText.NotEnoughScales;
                return false;
            }
        }

        return true;
    }

    bool HasLoadedScales(int count)
    {
        if (count < 1)
            return true;

        if (_scaleSpender == null)
            return false;

        IScaleMagazine magazine = _scaleSpender.Magazine;
        return magazine != null && magazine.LoadedCount >= count;
    }

    bool CanCastCurrent(out string failReason)
    {
        failReason = null;
        if (currentMagic == null)
            return false;

        return currentMagic.CanCast(gameObject, out failReason);
    }

    bool CanPayMana()
    {
        if (PlayerCostRules.IsFree || playerMana == null || currentMagic == null)
            return true;

        return playerMana.CanAfford(currentMagic.ManaCost);
    }

    bool TryPayMana()
    {
        if (PlayerCostRules.IsFree || playerMana == null || currentMagic == null)
            return true;

        return playerMana.TrySpendMana(currentMagic.ManaCost);
    }
}
