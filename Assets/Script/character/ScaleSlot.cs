using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One scale slot. Owns its state, the <see cref="ScaleData"/> it holds and its presentation.
/// It has no timers: <see cref="ScaleMagazine"/> drives every state change (regrowth, loading, reservations).
/// 컴포넌트 enabled는 동작에 영향이 없다. 칸 하나만 끄는 기능은 지원하지 않는다.
/// <para>
/// <b>The slot's GameObject is never deactivated.</b> Empty only flips <c>enabled</c> on the cached
/// renderers/colliders, and every other state restores each one to the enabled value it had in <c>Awake</c>.
/// </para>
/// <para>
/// <b>Look:</b> Empty is hidden, Grown shows the default material, Loaded/Reserved additionally tint with
/// <see cref="ScaleData.SlotColor"/> through a <see cref="MaterialPropertyBlock"/> (no material instances).
/// </para>
/// State model: a guarded transition table (<see cref="IsTransitionAllowed"/>) rather than a bare enum,
/// so illegal transitions are rejected with a warning. Every slot starts Grown.
/// </summary>
public sealed class ScaleSlot : MonoBehaviour, IScaleSlot
{
    [Header("Presentation (toggled instead of SetActive)")]
    [Tooltip("Renderers hidden while the slot is empty and tinted while it holds a scale.")]
    [SerializeField] private Renderer[] _visualRenderers;

    [Tooltip("Colliders disabled while the slot is empty.")]
    [SerializeField] private Collider[] _colliders;

    [Tooltip("If both arrays above are empty, collect all child renderers/colliders automatically.")]
    [SerializeField] private bool _autoCollectIfEmpty = true;

    [Tooltip("Shader color property tinted with ScaleData.slotColor. URP Lit/Unlit use _BaseColor; Built-in uses _Color.")]
    [SerializeField] private string _colorPropertyName = "_BaseColor";

    private ScaleSlotState _state = ScaleSlotState.Grown;
    private ScaleData _scale;
    private int _currentToken;
    private int _tokenCounter;

    private Renderer[] _cachedRenderers = Array.Empty<Renderer>();
    private bool[] _rendererOriginalEnabled = Array.Empty<bool>();
    private Collider[] _cachedColliders = Array.Empty<Collider>();
    private bool[] _colliderOriginalEnabled = Array.Empty<bool>();
    private MaterialPropertyBlock _propertyBlock;
    private int _colorPropertyId;

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
        if (string.IsNullOrEmpty(_colorPropertyName))
        {
            Debug.LogWarning($"[ScaleSlot] '{name}' has no color property name; using _BaseColor.", this);
            _colorPropertyName = "_BaseColor";
        }

        _colorPropertyId = Shader.PropertyToID(_colorPropertyName);
        _propertyBlock = new MaterialPropertyBlock();
        CachePresentation();
        ApplyPresentation();
    }

    /// <inheritdoc />
    public bool TryGrow()
    {
        if (_state != ScaleSlotState.Empty)
        {
            Debug.LogWarning($"[ScaleSlot] '{name}' TryGrow ignored (state={_state}).", this);
            return false;
        }

        if (!TrySetState(ScaleSlotState.Grown))
            return false;

        ApplyPresentation();
        return true;
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

        ApplyPresentation();
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

        ApplyPresentation();
        return true;
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
        ApplyPresentation();
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
        StateChanged?.Invoke(this, previous, next);
        return true;
    }

    private void CachePresentation()
    {
        if (_autoCollectIfEmpty && IsNullOrEmpty(_visualRenderers) && IsNullOrEmpty(_colliders))
        {
            Debug.LogWarning($"[ScaleSlot] '{name}' has no renderer/collider references assigned; collecting from children.", this);
            _visualRenderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
        }

        var seen = new HashSet<UnityEngine.Object>();

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

        var colliders = new List<Collider>();
        var colliderStates = new List<bool>();
        if (_colliders != null)
        {
            foreach (Collider c in _colliders)
            {
                if (c == null || !seen.Add(c)) continue;
                colliders.Add(c);
                colliderStates.Add(c.enabled);
            }
        }

        _cachedRenderers = renderers.ToArray();
        _rendererOriginalEnabled = rendererStates.ToArray();
        _cachedColliders = colliders.ToArray();
        _colliderOriginalEnabled = colliderStates.ToArray();

        if (_cachedRenderers.Length == 0 && _cachedColliders.Length == 0)
            Debug.LogWarning($"[ScaleSlot] '{name}' has nothing to toggle; state changes will have no visible effect.", this);
    }

    private void ApplyPresentation()
    {
        bool visible = _state != ScaleSlotState.Empty;
        bool tinted = _scale != null && (_state == ScaleSlotState.Loaded || _state == ScaleSlotState.Reserved);

        // Restore each component to its own cached value; never blanket-enable.
        for (int i = 0; i < _cachedRenderers.Length; i++)
        {
            Renderer r = _cachedRenderers[i];
            if (r == null) continue;

            r.enabled = visible && _rendererOriginalEnabled[i];
            if (tinted)
            {
                _propertyBlock.Clear();
                _propertyBlock.SetColor(_colorPropertyId, _scale.SlotColor);
                r.SetPropertyBlock(_propertyBlock);
            }
            else
            {
                r.SetPropertyBlock(null);
            }
        }

        for (int i = 0; i < _cachedColliders.Length; i++)
            if (_cachedColliders[i] != null) _cachedColliders[i].enabled = visible && _colliderOriginalEnabled[i];
    }

    private static bool IsNullOrEmpty<T>(T[] array) => array == null || array.Length == 0;
}
