using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 구역 안에 파리를 두고, 잡힌 자리는 시간이 지나면 다시 채운다.
/// 씬에 빈 오브젝트로 둔다. 파리는 이 오브젝트의 자식으로 생긴다.
/// </summary>
public sealed class FlySpawner : MonoBehaviour
{
    const float InitialDelay = 0.1f;
    const float RefillInterval = 0.5f;
    const float GizmoLift = 0.05f;
    const int CircleSegments = 48;
    const float DefaultWanderRadius = 2f;
    const float DefaultFleeLeash = 2f;

    static readonly Color SpawnCircle = new Color(0.3f, 0.85f, 0.4f, 1f);
    static readonly Color ActivityCircle = new Color(0.3f, 0.85f, 0.4f, 0.28f);

    [SerializeField, Tooltip("Fly가 붙은 프리팹.")]
    Fly _flyPrefab;

    [SerializeField, Min(0), Tooltip("이 구역에 유지할 마리 수.")]
    int _count = 5;

    [SerializeField, Min(0f), Tooltip("스포너 중심에서 파리를 놓을 반경.")]
    float _radius = 6f;

    [SerializeField, Min(0f), Tooltip("바닥에서 띄울 높이.")]
    float _hoverHeight = 1.2f;

    [SerializeField, Tooltip("바닥 레이어. 이 프로젝트의 바닥은 Floor. 비어 있으면 파리를 만들지 않는다.")]
    LayerMask _groundMask;

    [SerializeField, Tooltip("놓으면 안 되는 곳. 벽 등.")]
    LayerMask _blockMask;

    [SerializeField, Min(0f), Tooltip("플레이어와 이 거리 안에는 생성하지 않는다.")]
    float _minPlayerDistance = 4f;

    [SerializeField, Tooltip("잡히면 다시 채운다.")]
    bool _respawn = true;

    [SerializeField, Min(0f), Tooltip("다시 채우기까지 걸리는 시간(초).")]
    float _respawnDelay = 10f;

    [SerializeField, Min(1), Tooltip("위치 하나를 찾을 때 최대 시도 횟수.")]
    int _maxAttempts = 10;

    [Header("자리 검사")]
    [SerializeField, Min(0.05f), Tooltip("이 반경 안에 벽이 있으면 그 자리는 버린다.")]
    float _blockProbeRadius = 0.25f;

    [SerializeField, Min(0.5f), Tooltip("바닥을 찾을 때 스포너보다 이만큼 위에서 아래로 쏜다.")]
    float _groundProbeUp = 8f;

    [SerializeField, Min(0.5f), Tooltip("바닥 레이가 스포너 아래로 내려가는 거리. 그 안에 바닥이 없으면 실패한다.")]
    float _groundProbeDepth = 30f;

    readonly List<Fly> _flies = new List<Fly>();
    readonly List<float> _dueTimes = new List<float>();
    Transform _player;
    float _nextCheck = float.PositiveInfinity;
    int _startFrame;
    bool _spawned;

    void Reset()
    {
        _groundMask = LayerMask.GetMask("Floor");
    }

    void Start()
    {
        if (!CanSpawn())
            return;

        // 플레이어 이동은 다음 프레임의 Update가 끝난 뒤에 실행된다.
        _startFrame = Time.frameCount;
        _nextCheck = Time.time + InitialDelay;
    }

    void Update()
    {
        if (Time.time < _nextCheck)
            return;

        if (!_spawned)
        {
            if (Time.frameCount < _startFrame + 2)
                return;

            SpawnInitial();
            return;
        }

        _nextCheck = Time.time + RefillInterval;
        ScheduleMissing(RemoveDestroyed());
        SpawnDue(Time.time);
    }

    void SpawnInitial()
    {
        _spawned = true;
        for (int i = 0; i < _count; i++)
            TrySpawn();

        if (!_respawn)
        {
            _nextCheck = float.PositiveInfinity;
            return;
        }

        ScheduleMissing(_count - _flies.Count);
        _nextCheck = Time.time + RefillInterval;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;
        center.y += GizmoLift;
        DrawCircle(center, _radius, SpawnCircle);
        DrawCircle(center, ActivityRadius(), ActivityCircle);
    }

    bool CanSpawn()
    {
        if (_flyPrefab == null)
        {
            Debug.LogWarning($"[FlySpawner] '{name}'에 파리 프리팹이 없습니다.", this);
            return false;
        }

        if (_groundMask.value == 0)
        {
            Debug.LogWarning($"[FlySpawner] '{name}'의 바닥 레이어가 비어 있습니다. 파리를 만들 수 없습니다.", this);
            return false;
        }

        return true;
    }

    int RemoveDestroyed()
    {
        int removed = 0;
        for (int i = _flies.Count - 1; i >= 0; i--)
        {
            if (_flies[i] != null)
                continue;

            _flies.RemoveAt(i);
            removed++;
        }

        return removed;
    }

    void ScheduleMissing(int missing)
    {
        if (missing <= 0)
            return;

        int room = _count - _flies.Count - _dueTimes.Count;
        if (missing > room)
            missing = room;

        if (missing <= 0)
            return;

        float due = Time.time + _respawnDelay;
        for (int i = 0; i < missing; i++)
            _dueTimes.Add(due);
    }

    void SpawnDue(float now)
    {
        for (int i = _dueTimes.Count - 1; i >= 0; i--)
        {
            if (_dueTimes[i] > now)
                continue;

            if (_flies.Count >= _count)
            {
                _dueTimes.RemoveAt(i);
                continue;
            }

            if (TrySpawn())
            {
                _dueTimes.RemoveAt(i);
                continue;
            }

            // 이번 시도는 건너뛴다. 예약을 유지해야 마리 수가 줄지 않는다.
            _dueTimes[i] = now + _respawnDelay;
        }
    }

    bool TrySpawn()
    {
        if (_flyPrefab == null || !TryPickPosition(out Vector3 position))
            return false;

        // 생성 후에 옮기면 Fly.Awake가 원점을 배회 중심으로 저장한다.
        Fly fly = Instantiate(_flyPrefab, position, _flyPrefab.transform.rotation, transform);
        _flies.Add(fly);
        return true;
    }

    bool TryPickPosition(out Vector3 position)
    {
        for (int i = 0; i < _maxAttempts; i++)
        {
            if (TrySample(out position))
                return true;
        }

        Debug.LogWarning($"[FlySpawner] '{name}'에서 자리를 {_maxAttempts}번 안에 찾지 못했습니다.", this);
        position = default;
        return false;
    }

    bool TrySample(out Vector3 position)
    {
        position = default;
        Vector2 disk = Random.insideUnitCircle * _radius;
        Vector3 center = transform.position;
        Vector3 origin = new Vector3(center.x + disk.x, center.y + _groundProbeUp, center.z + disk.y);

        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, _groundProbeUp + _groundProbeDepth, _groundMask, QueryTriggerInteraction.Ignore))
            return false;

        Vector3 spawn = hit.point + Vector3.up * _hoverHeight;
        if (_blockMask.value != 0 &&
            Physics.CheckSphere(spawn, _blockProbeRadius, _blockMask, QueryTriggerInteraction.Ignore))
            return false;

        if (IsTooCloseToPlayer(spawn))
            return false;

        position = spawn;
        return true;
    }

    bool IsTooCloseToPlayer(Vector3 point)
    {
        if (_minPlayerDistance <= 0f)
            return false;

        Transform player = Player();
        if (player == null)
            return false;

        Vector3 offset = point - player.position;
        offset.y = 0f;
        return offset.sqrMagnitude < _minPlayerDistance * _minPlayerDistance;
    }

    Transform Player()
    {
        if (_player != null)
            return _player;

        if (PlayerRoot.Instance == null)
            return null;

        _player = PlayerRoot.Instance.transform.Find("Player");
        return _player;
    }

    float ActivityRadius()
    {
        float wander = DefaultWanderRadius;
        float leash = DefaultFleeLeash;
        if (_flyPrefab != null)
        {
            wander = _flyPrefab.WanderRadius;
            leash = _flyPrefab.FleeLeashMultiplier;
        }

        return _radius + wander * leash;
    }

    static void DrawCircle(Vector3 center, float radius, Color color)
    {
        if (radius <= 0.001f)
            return;

        Gizmos.color = color;
        float step = Mathf.PI * 2f / CircleSegments;
        Vector3 previous = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= CircleSegments; i++)
        {
            float angle = step * i;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
