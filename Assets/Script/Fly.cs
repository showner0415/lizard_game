using UnityEngine;

/// <summary>
/// 맵에서 돌아다니다가, 올가미에 붙잡히면 플레이어에게 날아와 인벤에 들어간다.
/// F 상호작용 대상이 아니다.
/// </summary>
public sealed class Fly : MonoBehaviour, IPullable
{
    const float MinRadius = 0.5f;
    const float MinPullTime = 0.5f;
    const float MinCooldown = 0.05f;

    enum FlyState
    {
        Wander,
        Flee,
        Pulled
    }

    [SerializeField, Tooltip("인벤에 넣을 파리 아이템.")]
    ItemData _flyItem;

    [SerializeField, Min(1), Tooltip("한 마리가 주는 수량.")]
    int _amount = 1;

    [SerializeField, Min(0f), Tooltip("배회 속도.")]
    float _wanderSpeed = 1.2f;

    [SerializeField, Min(MinRadius), Tooltip("태어난 자리 주변에서 배회하는 반경.")]
    float _wanderRadius = 2f;

    [SerializeField, Min(0f), Tooltip("플레이어가 이 거리 안이면 도망간다. 0이면 도망하지 않는다.")]
    float _fleeDistance = 2.5f;

    [SerializeField, Min(0f), Tooltip("도망 속도.")]
    float _fleeSpeed = 3.5f;

    [SerializeField, Min(1f), Tooltip("도망이 태어난 자리에서 벗어날 수 있는 거리. 배회 반경에 곱한다.")]
    float _fleeLeashMultiplier = 2f;

    [SerializeField, Min(MinPullTime), Tooltip("끌려오는 최대 시간. 넘으면 다시 배회한다.")]
    float _maxPullTime = 3f;

    [SerializeField, Min(MinCooldown), Tooltip("인벤이 가득 찬 뒤 다시 수집을 시도하기까지 기다리는 시간.")]
    float _collectCooldown = 1f;

    [SerializeField, Min(0.05f), Tooltip("플레이어와 닿았다고 볼 세계 반경.")]
    float _collectRadius = 0.45f;

    Rigidbody _body;
    Vector3 _home;
    Vector3 _wanderTarget;
    Transform _pullTarget;
    Transform _player;
    float _pullSpeed;
    float _pullUntil;
    float _collectBlockedUntil;
    int _amountLeft;
    bool _deferMove;
    FlyState _state;

    void Awake()
    {
        _home = transform.position;
        _amountLeft = _amount < 1 ? 1 : _amount;
        EnsureKinematicBody();
        EnsureTrigger();
        PickWanderTarget();
    }

    /// <inheritdoc />
    public void BeginPull(Transform target, float speed)
    {
        if (target == null)
            return;

        _pullTarget = target;
        _pullSpeed = speed < 0f ? 0f : speed;
        _pullUntil = Time.time + (_maxPullTime < MinPullTime ? MinPullTime : _maxPullTime);
        _state = FlyState.Pulled;
    }

    void FixedUpdate()
    {
        if (_body == null)
            return;

        if (_deferMove)
        {
            _deferMove = false;
            return;
        }

        if (_state == FlyState.Pulled)
        {
            UpdatePulled();
            return;
        }

        if (ShouldFlee())
            UpdateFlee();
        else
            UpdateWander();
    }

    void OnTriggerEnter(Collider other)
    {
        TryCollect(other);
    }

    void OnTriggerStay(Collider other)
    {
        TryCollect(other);
    }

    void UpdatePulled()
    {
        if (_pullTarget == null || Time.time >= _pullUntil)
        {
            _pullTarget = null;
            _state = FlyState.Wander;
            PickWanderTarget();
            return;
        }

        Vector3 next = Vector3.MoveTowards(Position, _pullTarget.position, _pullSpeed * Time.fixedDeltaTime);
        _body.MovePosition(next);
    }

    void UpdateWander()
    {
        _state = FlyState.Wander;
        Vector3 to = _wanderTarget - Position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.04f)
            PickWanderTarget();
        else
            MoveFlat(_wanderTarget, _wanderSpeed);
    }

    bool ShouldFlee()
    {
        if (_fleeDistance <= 0f)
            return false;

        Transform player = PlayerTransform();
        if (player == null)
            return false;

        Vector3 away = Position - player.position;
        away.y = 0f;
        return away.sqrMagnitude <= _fleeDistance * _fleeDistance;
    }

    void UpdateFlee()
    {
        _state = FlyState.Flee;
        Transform player = PlayerTransform();
        if (player == null)
        {
            _state = FlyState.Wander;
            return;
        }

        Vector3 away = Position - player.position;
        away.y = 0f;
        if (away.sqrMagnitude > _fleeDistance * _fleeDistance)
        {
            _state = FlyState.Wander;
            PickWanderTarget();
            return;
        }

        if (away.sqrMagnitude < 0.0001f)
            away = Vector3.forward;

        Vector3 dest = Position + away.normalized;
        dest.y = Position.y;
        MoveFlat(ClampToHome(dest), _fleeSpeed);
    }

    void TryCollect(Collider other)
    {
        if (other == null || _amountLeft <= 0 || Time.time < _collectBlockedUntil || !IsPlayer(other))
            return;

        if (_flyItem == null)
        {
            Debug.LogWarning("[Fly] 파리 아이템이 비어 있습니다.", this);
            return;
        }

        if (InventoryController.Instance == null)
        {
            Debug.LogWarning("[Fly] InventoryController가 없습니다.", this);
            return;
        }

        int remain = InventoryController.Instance.AddToInventory(_flyItem, _amountLeft);
        if (remain <= 0)
        {
            _amountLeft = 0;
            Destroy(gameObject);
            return;
        }

        _amountLeft = remain;
        _collectBlockedUntil = Time.time + _collectCooldown;
        _pullTarget = null;
        _state = FlyState.Wander;
        StepAway(other.transform);
        PickWanderTarget();
    }

    void StepAway(Transform player)
    {
        Vector3 away = Position - player.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f)
            away = Vector3.forward;

        Vector3 next = Position + away.normalized * (_collectRadius + 0.25f);
        next.y = _home.y;
        _body.MovePosition(next);
        _deferMove = true;
    }

    Vector3 ClampToHome(Vector3 point)
    {
        float multiplier = _fleeLeashMultiplier < 1f ? 1f : _fleeLeashMultiplier;
        float limit = _wanderRadius * multiplier;
        Vector3 flat = point - _home;
        flat.y = 0f;
        if (flat.sqrMagnitude <= limit * limit)
            return point;

        Vector3 clamped = _home + flat.normalized * limit;
        clamped.y = point.y;
        return clamped;
    }

    Vector3 Position => _body != null ? _body.position : transform.position;

    void MoveFlat(Vector3 worldTarget, float speed)
    {
        Vector3 next = Vector3.MoveTowards(Position, worldTarget, speed * Time.fixedDeltaTime);
        next.y = Position.y;
        _body.MovePosition(next);
    }

    void PickWanderTarget()
    {
        Vector2 circle = Random.insideUnitCircle * _wanderRadius;
        _wanderTarget = _home + new Vector3(circle.x, 0f, circle.y);
    }

    Transform PlayerTransform()
    {
        if (_player != null)
            return _player;

        if (PlayerRoot.Instance == null)
            return null;

        _player = PlayerRoot.Instance.transform.Find("Player");
        return _player;
    }

    void EnsureKinematicBody()
    {
        _body = GetComponent<Rigidbody>();
        if (_body == null)
            _body = gameObject.AddComponent<Rigidbody>();

        _body.isKinematic = true;
        _body.useGravity = false;
        _body.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void EnsureTrigger()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].isTrigger = true;
        }

        SphereCollider sphere = GetComponent<SphereCollider>();
        if (sphere == null)
            sphere = gameObject.AddComponent<SphereCollider>();

        sphere.isTrigger = true;
        Vector3 scale = transform.lossyScale;
        float minAxis = Mathf.Min(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        if (minAxis < 0.001f)
            minAxis = 1f;

        sphere.radius = _collectRadius / minAxis;
    }

    static bool IsPlayer(Collider other)
    {
        if (other.CompareTag("Player"))
            return true;

        if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player"))
            return true;

        Transform parent = other.transform.parent;
        return parent != null && parent.CompareTag("Player");
    }
}
