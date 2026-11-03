using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One scale slot. Owns its state, the <see cref="ScaleData"/> it holds and its presentation.
/// It has no timers: <see cref="ScaleMagazine"/> drives every state change (regrowth, loading, reservations).
/// 컴포넌트 enabled는 동작에 영향이 없다. 칸 하나만 끄는 기능은 지원하지 않는다.
/// <para>
/// <b>The slot's GameObject is never deactivated.</b> Renderers stay enabled only while the slot is
/// Loaded or Reserved, and only if that renderer was enabled when it was cached. Empty and Grown hide them.
/// </para>
/// State model: a guarded transition table (<see cref="IsTransitionAllowed"/>) rather than a bare enum,
/// so illegal transitions are rejected with a warning. Every slot starts Grown.
/// </summary>
public sealed class ScaleSlot : MonoBehaviour, IScaleSlot
{
    [Header("Presentation (toggled instead of SetActive)")]
    [Tooltip("장전됐거나 예약된 동안에만 켠다. 비어 있거나 자란 상태에서는 숨긴다.")]
    [SerializeField] private Renderer[] _visualRenderers;

    [Tooltip("렌더러를 비워 두면 자식 렌더러를 자동으로 모은다.")]
    [SerializeField] private bool _autoCollectIfEmpty = true;

    [SerializeField, Min(0), Tooltip("소모 순서. 작을수록 먼저 쓰이고 먼저 자란다.")]
    private int _order;

    private ScaleSlotState _state = ScaleSlotState.Grown;
    private ScaleData _scale;
    private int _currentToken;
    private int _tokenCounter;

    private Renderer[] _cachedRenderers = Array.Empty<Renderer>();
    private bool[] _rendererOriginalEnabled = Array.Empty<bool>();

    /// <summary>소모 순서. 작을수록 먼저 쓰이고 먼저 자란다.</summary>
    public int Order => _order;

    /// <inheritdoc />
    public event Action<IScaleSlot, ScaleSlotState, ScaleSlotState> StateChanged;

    /// <inheritdoc />
    public event Action<IScaleSlot> OnScaleConsumed;

    /// <inheritdoc />
    public ScaleSlotState State => _state;

    /// <inheritdoc />
    public ScaleData Scale => _scale;

    private void Awake()
    {
        CachePresentation();
        ApplyPresentation();

        if (_order < 0)
        {
            Debug.LogWarning($"[ScaleSlot] '{name}' Order {_order}는 0으로 맞춥니다.", this);
            _order = 0;
        }
    }

    /// <inheritdoc />
    public bool TryGrow()
    {
        if (_state != ScaleSlotState.Empty)
        {
            Debug.LogWarning($"[ScaleSlot] '{name}' TryGrow ignored (state={_state}).", this);
            return false;
        }

        return TrySetState(ScaleSlotState.Grown);
    }

    /// <inheritdoc />
    public bool TryLoad(ScaleData scale)
    {
        if (scale == null)
        {
            Debug.LogWarning($"[ScaleSlot] '{name}' TryLoad called with null scale.", this);
            return false;
        }

        if (_state != ScaleSlotState.Grown)
        {
            Debug.LogWarning($"[ScaleSlot] '{name}' TryLoad ignored (state={_state}).", this);
            return false;
        }

        _scale = scale;
        if (!TrySetState(ScaleSlotState.Loaded))
        {
            _scale = null;
            return false;
        }

        return true;
    }

    /// <inheritdoc />
    public bool TryUnload()
    {
        if (_state != ScaleSlotState.Loaded)
        {
            Debug.LogWarning($"[ScaleSlot] '{name}' TryUnload ignored (state={_state}).", this);
            return false;
        }

        ScaleData dropped = _scale;
        _scale = null;
        if (!TrySetState(ScaleSlotState.Grown))
        {
            _scale = dropped;
            return false;
        }

        return true;
    }

    /// <summary>이 토큰으로 Reserved이면 true. 상태를 바꾸지 않는다.</summary>
    internal bool IsReservedBy(int token)
    {
        return token != 0 && _state == ScaleSlotState.Reserved && token == _currentToken;
    }

    /// <inheritdoc />
    public bool TryReserve(out int token)
    {
        token = 0;
        if (_state != ScaleSlotState.Loaded)
            return false;

        if (!TrySetState(ScaleSlotState.Reserved))
            return false;

        token = ++_tokenCounter;
        _currentToken = token;
        return true;
    }

    /// <inheritdoc />
    public bool Confirm(int token)
    {
        if (_state != ScaleSlotState.Reserved || token != _currentToken)
        {
            Debug.LogWarning($"[ScaleSlot] '{name}' Confirm ignored (state={_state}, tokenMatch={token == _currentToken}).", this);
            return false;
        }

        _currentToken = 0;
        _scale = null;
        TrySetState(ScaleSlotState.Empty);
        OnScaleConsumed?.Invoke(this);
        return true;
    }

    /// <inheritdoc />
    public bool Release(int token)
    {
        if (_state != ScaleSlotState.Reserved || token != _currentToken)
        {
            Debug.LogWarning($"[ScaleSlot] '{name}' Release ignored (state={_state}, tokenMatch={token == _currentToken}).", this);
            return false;
        }

        _currentToken = 0;
        return TrySetState(ScaleSlotState.Loaded);
    }

    private static bool IsTransitionAllowed(ScaleSlotState from, ScaleSlotState to)
    {
        switch (from)
        {
            case ScaleSlotState.Empty: return to == ScaleSlotState.Grown;
            case ScaleSlotState.Grown: return to == ScaleSlotState.Loaded;
            case ScaleSlotState.Loaded: return to == ScaleSlotState.Reserved || to == ScaleSlotState.Grown;
            case ScaleSlotState.Reserved: return to == ScaleSlotState.Empty || to == ScaleSlotState.Loaded;
            default: return false;
        }
    }

    private bool TrySetState(ScaleSlotState next)
    {
        if (!IsTransitionAllowed(_state, next))
        {
            Debug.LogWarning($"[ScaleSlot] '{name}' illegal transition {_state} -> {next} rejected.", this);
            return false;
        }

        ScaleSlotState previous = _state;
        _state = next;
        ApplyPresentation();
        StateChanged?.Invoke(this, previous, next);
        return true;
    }

    private void CachePresentation()
    {
        if (_autoCollectIfEmpty && IsNullOrEmpty(_visualRenderers))
        {
            Debug.LogWarning($"[ScaleSlot] '{name}' has no renderer references assigned; collecting from children.", this);
            _visualRenderers = GetComponentsInChildren<Renderer>(true);
        }

        var seen = new HashSet<Renderer>();
        var renderers = new List<Renderer>();
        var rendererStates = new List<bool>();
        if (_visualRenderers != null)
        {
            foreach (Renderer r in _visualRenderers)
            {
                if (r == null || !seen.Add(r)) continue;
                renderers.Add(r);
                rendererStates.Add(r.enabled);
            }
        }

        _cachedRenderers = renderers.ToArray();
        _rendererOriginalEnabled = rendererStates.ToArray();

        if (_cachedRenderers.Length == 0)
            Debug.LogWarning($"[ScaleSlot] '{name}' has nothing to toggle; state changes will have no visible effect.", this);
    }

    private static bool IsVisible(ScaleSlotState state) => state == ScaleSlotState.Loaded || state == ScaleSlotState.Reserved;

    private void ApplyPresentation()
    {
        bool visible = IsVisible(_state);
        for (int i = 0; i < _cachedRenderers.Length; i++)
        {
            Renderer r = _cachedRenderers[i];
            if (r == null) continue;
            r.enabled = visible && _rendererOriginalEnabled[i];
        }
    }

    [ContextMenu("Log Presentation")]
    void LogPresentation()
    {
        Debug.Log($"[ScaleSlot] '{name}' state={_state} visible={IsVisible(_state)} renderers={_cachedRenderers.Length}", this);
        for (int i = 0; i < _cachedRenderers.Length; i++)
        {
            Renderer r = _cachedRenderers[i];
            string rendererName = r != null ? r.name : "null";
            bool enabled = r != null && r.enabled;
            Debug.Log($"[ScaleSlot] '{name}' [{i}] {rendererName} enabled={enabled} original={_rendererOriginalEnabled[i]}", this);
        }
    }

    private static bool IsNullOrEmpty<T>(T[] array) => array == null || array.Length == 0;
}
