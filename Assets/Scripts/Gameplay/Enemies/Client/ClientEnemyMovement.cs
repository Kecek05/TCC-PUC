using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Client-only: reads PathProgress from the server, samples the shared WaypointPath, applies MapTranslator
/// coordinate conversion, and places the enemy's root in this client's view. Exposes
/// <see cref="DisplayProgress"/>, the progress the enemy is DRAWN at, which <see cref="TroopMotion"/> builds the
/// visible route on.
/// </summary>
/// <remarks>
/// <b>Remote clients extrapolate.</b> The server only syncs PathProgress once it has moved half a percent of the
/// lane (about 0.12 units), so chasing the raw value stepped: a lerp toward a target that jumps every few frames
/// pulses in speed and trails ~0.2 units behind. The client instead advances its own progress at the already
/// replicated <see cref="ServerEnemyMovement.CurrentSpeed"/> (zero while the enemy spawns in, is held, or the match
/// is not running) and eases toward the server's value plus the time since it was heard. It never steps backwards on
/// jitter; a real pull back (an Ancora) is drawn as a drag, and a large error snaps.
/// <para/>
/// <b>The host is untouched:</b> its server writes the exact position every frame, so it draws
/// <see cref="ServerEnemyMovement.Progress"/> directly, and its root keeps the behaviour it always had, because on the
/// host the root is also what every range check measures.
/// </remarks>
public class ClientEnemyMovement : NetworkBehaviour
{
    [SerializeField] private float interpolationSpeed = 10f;
    [SerializeField] private EntityTeam entityTeam;

    private ServerEnemyMovement _serverMovement;
    private WaypointPath _path;
    private bool _initialized;

    private BaseMapTranslator _mapTranslator;
    private BaseGameFlowManager _gameFlowManager;

    // Remote-client prediction.
    private float _displayProgress;
    private float _heardProgress;
    private float _heardAt;

    /// <summary>The enemy has been placed where it belongs in this client's view. Before this, on the mirrored
    /// Blue client, its transform still holds the server-space spawn point.</summary>
    public event System.Action OnVisualReady;

    /// <summary>True once <see cref="OnVisualReady"/> has been raised for this life.</summary>
    public bool IsVisualReady => _initialized;

    /// <summary>The lane this enemy walks, once assigned (null before).</summary>
    public WaypointPath Path => _path;

    /// <summary>
    /// The progress (0..1, in this enemy's own walking direction) this peer draws the enemy at: exact on the host,
    /// smoothly predicted on a remote client.
    /// </summary>
    public float DisplayProgress => IsServer && _serverMovement != null ? _serverMovement.Progress : _displayProgress;

    /// <summary>
    /// Called after spawn to assign the path reference on the client.
    /// The path must be the same WaypointPath instance the server uses.
    /// </summary>
    public void Initialize(WaypointPath path)
    {
        _path = path;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer && !IsClient)
        {
            enabled = false;
            return;
        }

        // Pooled: a recycled instance must wait for its new path and placement again.
        _initialized = false;
        _serverMovement = GetComponent<ServerEnemyMovement>();
        StartCoroutine(WaitForInitialization());
    }

    private IEnumerator WaitForInitialization()
    {
        _mapTranslator = ServiceLocator.Get<BaseMapTranslator>();
        ServiceLocator.TryGet(out _gameFlowManager);

        // Wait until the path has been assigned and MapTranslator is ready
        yield return new WaitUntil(() =>
            _path != null &&
            _mapTranslator != null &&
            _mapTranslator.IsInitialized);

        // Snap to initial position
        float progress = _serverMovement.PathProgress.Value;
        _displayProgress = _heardProgress = progress;
        _heardAt = Time.time;
        transform.position = LocalPositionAt(progress);
        _initialized = true;

        OnVisualReady?.Invoke();
    }

    private Vector3 LocalPositionAt(float progress)
    {
        float sampleT = _serverMovement.Reversed.Value ? 1f - progress : progress;
        Vector3 serverPos = _path.SamplePosition(sampleT);
        return _mapTranslator.ServerToLocal(serverPos, entityTeam.GetTeamType());
    }

    private void Update()
    {
        if (!_initialized || _path == null) return;

        if (IsServer)
        {
            // Host: as it always was. The server writes the exact root every frame for tower targeting; this lerp
            // is kept so the gameplay position on the host stays bit-for-bit what it was.
            Vector3 hostTarget = LocalPositionAt(_serverMovement.PathProgress.Value);
            transform.position = Vector3.Lerp(transform.position, hostTarget, interpolationSpeed * Time.deltaTime);
            return;
        }

        PredictProgress(Time.deltaTime);
        transform.position = LocalPositionAt(_displayProgress);
    }

    /// <summary>Remote client: advance at the replicated speed, ease toward what the server last said plus the time since.</summary>
    private void PredictProgress(float dt)
    {
        TroopMotionSettingsSO settings = TroopMotionSettingsSO.Instance;
        float length = _path.TotalLength;
        if (length <= 0f) return;

        float heard = _serverMovement.PathProgress.Value;
        if (heard != _heardProgress)
        {
            _heardProgress = heard;
            _heardAt = Time.time;
        }

        // The cases where the server holds progress still although CurrentSpeed says otherwise.
        bool walking = !_serverMovement.Invincible.Value
                       && !_serverMovement.Held.Value
                       && (_gameFlowManager == null || _gameFlowManager.CurrentGameState.Value == GameState.InMatch);
        float rate = walking ? _serverMovement.CurrentSpeed.Value / length : 0f;

        // Time standing still is not time walked. Without this, the first frame after the spawn-in (or a release
        // from a hold) extrapolated across the whole stop: the body lurched ~0.4 units ahead and was dragged back
        // the moment the server's first update arrived (measured with MPPM, 2026-11-11).
        if (!walking) _heardAt = Time.time;

        float since = Mathf.Min(Time.time - _heardAt, settings.MaxExtrapolation);
        float predicted = Mathf.Min(1f, _heardProgress + rate * since);

        float next = _displayProgress + rate * dt;
        float errorUnits = (predicted - next) * length;
        if (Mathf.Abs(errorUnits) > settings.SnapDistance)
        {
            next = predicted;
        }
        else if (errorUnits >= -settings.BackwardTolerance)
        {
            next += (predicted - next) * (1f - Mathf.Exp(-settings.CorrectionSharpness * dt));
            // Running slightly ahead: wait for the server instead of visibly stepping back.
            next = Mathf.Max(next, _displayProgress);
        }
        else
        {
            // A real pull back: drawn as a quick drag rather than a jump.
            next += (predicted - next) * (1f - Mathf.Exp(-settings.CorrectionSharpness * 2f * dt));
        }

        _displayProgress = Mathf.Clamp01(next);
    }
}
