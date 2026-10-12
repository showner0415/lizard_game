using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One consumable tail. Owns its state machine, presentation toggling and regeneration timer.
/// <para>
/// <b>The tail's GameObject is never deactivated.</b> Deactivating it would stop the regeneration
/// coroutine. "Consuming" only flips <c>enabled</c> on the cached renderers/colliders, and
/// regeneration restores each one to the enabled value it had in <c>Awake</c>.
/// </para>
/// <para>
/// <b>Regeneration delay precedence (single rule):</b> if <c>_overrideRegenerationDelay</c> is true the tail's own
/// <c>_regenerationDelay</c> wins; otherwise the default injected by <see cref="TailManager"/> at registration is used
/// (falling back to the tail's own value if it was never registered).
/// </para>
/// <para>
/// <b>Time semantics:</b> scaled <see cref="Time.time"/>. Regeneration pauses with <c>Time.timeScale = 0</c>.
/// The deadline is stored as an absolute timestamp, so disabling and re-enabling the component never
/// loses or restarts the timer.
/// </para>
/// State model: a guarded transition table (<see cref="IsTransitionAllowed"/>) rather than a bare enum,
/// so illegal transitions are rejected with a warning.
/// </summary>
public sealed class Tail : MonoBehaviour, ITailConsumable
{
    /// <summary>Lowest regeneration delay accepted; smaller or invalid values are clamped to this.</summary>
    public const float MinRegenerationDelay = 0.05f;

    [Header("Regeneration")]
    [Tooltip("If true, this tail uses its own delay below instead of the TailManager default.")]
    [SerializeField] private bool _overrideRegenerationDelay;

    [Tooltip("Seconds (scaled time) until this tail regrows. Only used when the override is enabled.")]
    [SerializeField, Min(MinRegenerationDelay)] private float _regenerationDelay = 6f;

    [Header("Presentation (toggled instead of SetActive)")]
    [Tooltip("Renderers hidden while the tail is consumed.")]
    [SerializeField] private Renderer[] _visualRenderers;

    [Tooltip("Colliders disabled while the tail is consumed.")]
    [SerializeField] private Collider[] _colliders;

    [Tooltip("If both arrays above are empty, collect all child renderers/colliders automatically.")]
    [SerializeField] private bool _autoCollectIfEmpty = true;

    private TailState _state = TailState.Available;
    private int _currentToken;
    private int _tokenCounter;

    private float _injectedDelay;
    private bool _hasInjectedDelay;
    private float _regenCompleteAtTime;
    private Coroutine _regenRoutine;

    private Renderer[] _cachedRenderers = Array.Empty<Renderer>();
    private bool[] _rendererOriginalEnabled = Array.Empty<bool>();
    private Collider[] _cachedColliders = Array.Empty<Collider>();
    private bool[] _colliderOriginalEnabled = Array.Empty<bool>();

    /// <inheritdoc />
    public event Action<ITailConsumable, TailState, TailState> StateChanged;

    /// <inheritdoc />
    public event Action<ITailConsumable> OnTailConsumed;

    /// <inheritdoc />
    public TailState State => _state;

    /// <inheritdoc />
    public bool IsAvailable => _state == TailState.Available && isActiveAndEnabled;

    /// <summary>The delay that will be used the next time this tail regenerates, per the documented precedence.</summary>
    public float EffectiveRegenerationDelay =>
        _overrideRegenerationDelay || !_hasInjectedDelay ? _regenerationDelay : _injectedDelay;

    /// <summary>
    /// Injects the project-wide default delay (called by <see cref="TailManager"/> at registration).
    /// Ignored for timing if this tail overrides its delay.
    /// </summary>
    public void InjectDefaultRegenerationDelay(float delaySeconds)
    {
        _injectedDelay = SanitizeDelay(delaySeconds, MinRegenerationDelay, this, "injected default delay");
        _hasInjectedDelay = true;
    }

    /// <summary>Clamps a delay to <see cref="MinRegenerationDelay"/> and warns if it was negative, zero or non-finite.</summary>
    public static float SanitizeDelay(float value, float fallback, UnityEngine.Object context, string label)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < MinRegenerationDelay)
        {
            float clamped = float.IsNaN(value) || float.IsInfinity(value) ? Mathf.Max(fallback, MinRegenerationDelay) : MinRegenerationDelay;
            Debug.LogWarning($"[Tail] Invalid {label} ({value}); using {clamped}s.", context);
            return clamped;
        }

        return value;
    }

    private void Awake()
    {
        _regenerationDelay = SanitizeDelay(_regenerationDelay, 6f, this, "_regenerationDelay");
        CachePresentation();
    }

    private void OnEnable()
    {
        // The absolute deadline survives disabling; the routine recomputes the remaining time.
        if (_state == TailState.Regenerating)
            StartRegenerationRoutine();
    }

    private void OnDisable()
    {
        StopRegenerationRoutine();
    }

    private void OnValidate()
    {
        if (_regenerationDelay < MinRegenerationDelay)
            _regenerationDelay = MinRegenerationDelay;
    }

    /// <inheritdoc />
    public bool TryReserve(out int token)
    {
        token = 0;
        if (_state != TailState.Available || !isActiveAndEnabled)
            return false;

        if (!TrySetState(TailState.Reserved))
            return false;

        token = ++_tokenCounter;
        _currentToken = token;
        return true;
    }

    /// <inheritdoc />
    public bool Confirm(int token)
    {
        if (_state != TailState.Reserved || token != _currentToken)
        {
            Debug.LogWarning($"[Tail] '{name}' Confirm ignored (state={_state}, tokenMatch={token == _currentToken}).", this);
            return false;
        }

        _currentToken = 0;
        TrySetState(TailState.Consumed);
        SetPresentationConsumed();
        OnTailConsumed?.Invoke(this);

        // Consumed is momentary: start the durable Regenerating state within the same call.
        _regenCompleteAtTime = Time.time + EffectiveRegenerationDelay;
        TrySetState(TailState.Regenerating);
        StartRegenerationRoutine();
        return true;
    }

    /// <inheritdoc />
    public bool Release(int token)
    {
        if (_state != TailState.Reserved || token != _currentToken)
        {
            Debug.LogWarning($"[Tail] '{name}' Release ignored (state={_state}, tokenMatch={token == _currentToken}).", this);
            return false;
        }

        _currentToken = 0;
        return TrySetState(TailState.Available);
    }

    /// <inheritdoc />
    public bool ForceRegenerate()
    {
        if (_state != TailState.Regenerating)
        {
            Debug.LogWarning($"[Tail] '{name}' ForceRegenerate ignored (state={_state}).", this);
            return false;
        }

        StopRegenerationRoutine();
        CompleteRegeneration();
        return true;
    }

    private static bool IsTransitionAllowed(TailState from, TailState to)
    {
        switch (from)
        {
            case TailState.Available: return to == TailState.Reserved;
            case TailState.Reserved: return to == TailState.Available || to == TailState.Consumed;
            case TailState.Consumed: return to == TailState.Regenerating;
            case TailState.Regenerating: return to == TailState.Available;
            default: return false;
        }
    }

    private bool TrySetState(TailState next)
    {
        if (!IsTransitionAllowed(_state, next))
        {
            Debug.LogWarning($"[Tail] '{name}' illegal transition {_state} -> {next} rejected.", this);
            return false;
        }

        TailState previous = _state;
        _state = next;
        StateChanged?.Invoke(this, previous, next);
        return true;
    }

    private void StartRegenerationRoutine()
    {
        StopRegenerationRoutine();
        if (isActiveAndEnabled)
            _regenRoutine = StartCoroutine(RegenerationRoutine());
    }

    private void StopRegenerationRoutine()
    {
        if (_regenRoutine == null)
            return;

        StopCoroutine(_regenRoutine);
        _regenRoutine = null;
    }

    private IEnumerator RegenerationRoutine()
    {
        float remaining = _regenCompleteAtTime - Time.time;
        while (remaining > 0f)
        {
            yield return new WaitForSeconds(remaining);
            remaining = _regenCompleteAtTime - Time.time;
        }

        _regenRoutine = null;
        CompleteRegeneration();
    }

    private void CompleteRegeneration()
    {
        if (_state != TailState.Regenerating)
            return;

        RestorePresentation();
        TrySetState(TailState.Available);
    }

    private void CachePresentation()
    {
        if (_autoCollectIfEmpty && IsNullOrEmpty(_visualRenderers) && IsNullOrEmpty(_colliders))
        {
            Debug.LogWarning($"[Tail] '{name}' has no renderer/collider references assigned; collecting from children.", this);
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
            Debug.LogWarning($"[Tail] '{name}' has nothing to toggle; consuming it will have no visible effect.", this);
    }

    private void SetPresentationConsumed()
    {
        for (int i = 0; i < _cachedRenderers.Length; i++)
            if (_cachedRenderers[i] != null) _cachedRenderers[i].enabled = false;

        for (int i = 0; i < _cachedColliders.Length; i++)
            if (_cachedColliders[i] != null) _cachedColliders[i].enabled = false;
    }

    private void RestorePresentation()
    {
        // Restore each component to its own cached value; never blanket-enable.
        for (int i = 0; i < _cachedRenderers.Length; i++)
            if (_cachedRenderers[i] != null) _cachedRenderers[i].enabled = _rendererOriginalEnabled[i];

        for (int i = 0; i < _cachedColliders.Length; i++)
            if (_cachedColliders[i] != null) _cachedColliders[i].enabled = _colliderOriginalEnabled[i];
    }

    private static bool IsNullOrEmpty<T>(T[] array) => array == null || array.Length == 0;
}
