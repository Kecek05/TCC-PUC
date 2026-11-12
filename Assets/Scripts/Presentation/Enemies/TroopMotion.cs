using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// How a troop is DRAWN walking: its own route inside the lane (a seeded offset, a slow sway, rounded corners,
/// room made for its neighbours) and the body language on top of it (a spawn pop, a stepping gait, a lean into
/// turns, dust on landing).
/// </summary>
/// <remarks>
/// <b>Visual only, by construction.</b> The root keeps the gameplay position exactly where it always was: on the
/// server it is the lane centreline every range check, aura and splash measures to. Everything here is written to
/// two children:
/// <list type="bullet">
/// <item><c>Motion</c> carries the drawn position (route, stride surge, steering). It is the parent of <c>GFX</c>, so
/// the hit-stop pin and the shove in <see cref="EnemyFeedbackPresenter"/>, which move <c>GFX</c> relative to its
/// parent, keep working on top of the route.</item>
/// <item><c>GFX</c> gets its rotation (facing, lean, waddle) and scale (pop, hop, squash and stretch) from here.
/// Its position stays the presenter's. The heavy-hit squash lives one level further down, on the body sprite, so
/// the two scales compose rather than fight.</item>
/// </list>
/// <para/>
/// <b>Identical on every peer.</b> All randomness comes from a seed hashed from <see cref="NetworkObject.NetworkObjectId"/>,
/// which the server assigns and every client shares, so no byte is added to the wire. The route is a pure function of
/// that seed and the troop's progress (sway is laid out per world unit walked, not per second), so host and client
/// draw the same line. Steering is the one stateful term, and the only one that can differ by a hair between peers:
/// each peer pushes apart the troops it sees, and a remote client sees them a few frames late.
/// <para/>
/// No Update of its own: <see cref="TroopCrowd"/> ticks every troop in three passes (read progress, push apart, draw)
/// so steering sees the whole lane at once.
/// </remarks>
public class TroopMotion : NetworkBehaviour
{
    /// <summary>A standard troop body's radius (0.37 across), the size the profiles' distances are tuned for.</summary>
    private const float StandardRadius = 0.185f;

    /// <summary>The most a sideways drift may turn the body off the lane's direction, in degrees.</summary>
    private const float MaxDriftAngle = 45f;

    [Title("References")]
    [SerializeField, Required] private EnemyManager enemyManager;
    [SerializeField, Required] private ClientEnemyMovement clientMovement;
    [Tooltip("Carries the drawn position. Must be the parent of GFX and a direct child of the root.")]
    [SerializeField, Required] private Transform motion;
    [Tooltip("Turned and scaled here. Its position belongs to EnemyFeedbackPresenter (hit-stop, shove).")]
    [SerializeField, Required] private Transform gfx;

    private static readonly Dictionary<Transform, TroopMotion> ByRoot = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => ByRoot.Clear();

    /// <summary>
    /// Where <paramref name="unit"/> is drawn: a troop's body, anything else's own position. For effects that land
    /// ON a unit (a bullet, a bracket, a status). Rules that mirror gameplay (who a spell hits) must keep reading the
    /// root, which is where the server measures.
    /// </summary>
    public static Vector3 DrawnPosition(Component unit)
    {
        if (unit == null) return Vector3.zero;
        Transform root = unit.transform;
        return ByRoot.TryGetValue(root, out TroopMotion troop) && troop != null ? troop.gfx.position : root.position;
    }

    /// <summary>The transform to follow to stay on <paramref name="root"/>'s drawn body (a homing bullet).</summary>
    public static Transform DrawnTransform(Transform root)
    {
        if (root == null) return null;
        return ByRoot.TryGetValue(root, out TroopMotion troop) && troop != null ? troop.gfx : root;
    }

    // ---------- read by TroopCrowd ----------

    /// <summary>Ready, on a known lane, and placed this frame: takes part in steering.</summary>
    internal bool Active;
    internal WaypointPath Path;
    internal int Lane;
    /// <summary>Distance along the lane centreline from the path's first waypoint, world units.</summary>
    internal float S;
    /// <summary>+1 walking the path forward, -1 for a sent troop walking it backwards.</summary>
    internal float Sign;
    /// <summary>World units per second along the lane, smoothed.</summary>
    internal float Speed;
    /// <summary>Lateral offset from the centreline this frame (route + steering), before the lane clamp.</summary>
    internal float Lateral;
    /// <summary>Steering's offset along the lane, in the path's own direction.</summary>
    internal float Along;
    internal float Radius;
    /// <summary>Seeded tie-break: which way two exactly stacked troops step apart.</summary>
    internal float Tie;
    /// <summary>Accumulated by TroopCrowd each frame: how hard the neighbours push.</summary>
    internal float PushLateral;
    internal float PushAlong;

    // ---------- per life ----------

    private TroopMotionProfileSO _profile;
    private LaneCurve _curve;
    private Unity.Mathematics.Random _rng;
    private bool _listening;
    private bool _ready;
    private float _popStart;
    private float _baseSpeed;
    private float _sizeK = 1f;

    // Seeded route.
    private float _baseLateral;
    private float _swayX;
    private float _swayY;
    private float _stride;
    private float _phaseOffset;

    // Live state.
    private bool _reversed;
    private float _lastProgress;
    private float _walked;
    private float _routeLateral;
    private float _steerLateral;
    private float _steerAlong;
    private float _moveBlend;
    private float _phase;
    private float _prevLateral;
    private float _targetHeading;
    private float _heading;
    private float _headingVelocity;
    private float _turnRate;

#if UNITY_EDITOR
    private const int TrailLength = 64;
    private readonly Vector3[] _trail = new Vector3[TrailLength];
    private int _trailHead;
    private int _trailCount;
    private float _nextTrailAt;
    private float _gizmoHue;
    private BaseMapTranslator _gizmoTranslator;
#endif

    public override void OnNetworkSpawn()
    {
        // A dedicated server has nobody to draw for.
        if (IsServer && !IsClient)
        {
            enabled = false;
            return;
        }

        _profile = TroopMotionSettingsSO.Instance.ProfileFor(enemyManager.Data);
        _baseSpeed = enemyManager.Data != null ? enemyManager.Data.MoveSpeed : 1f;
        Seed(NetworkObjectId);

        // Measured with GFX at rest (a pooled body comes back with whatever its last life left on it).
        motion.localPosition = Vector3.zero;
        gfx.localRotation = Quaternion.identity;
        gfx.localScale = Vector3.one;
        Radius = 0.5f * VisibleBounds.Across(enemyManager, StandardRadius * 2f);
        _sizeK = Radius / StandardRadius;

        // Hidden until ClientEnemyMovement has placed the root in this client's view: on the mirrored Blue client
        // the transform still holds the server-space spawn point until then.
        gfx.localScale = Vector3.zero;
        _ready = false;
        Active = false;
        Path = null;
        _curve = null;

        ByRoot[transform] = this;
        TroopCrowd.Register(this);
        clientMovement.OnVisualReady += HandleVisualReady;
        _listening = true;
        if (clientMovement.IsVisualReady) HandleVisualReady();
    }

    public override void OnNetworkDespawn()
    {
        if (!_listening) return;
        _listening = false;

        clientMovement.OnVisualReady -= HandleVisualReady;
        ByRoot.Remove(transform);
        TroopCrowd.Unregister(this);

        // Pooled: the next life starts from a body at rest.
        _ready = false;
        Active = false;
        motion.localPosition = Vector3.zero;
        gfx.localRotation = Quaternion.identity;
        gfx.localScale = Vector3.one;
    }

    private void Seed(ulong networkObjectId)
    {
        uint hash = Hash((uint)networkObjectId ^ ((uint)(networkObjectId >> 32) * 0x9E3779B9u));
        _rng = new Unity.Mathematics.Random(hash == 0u ? 0x6E624EB7u : hash);

        _baseLateral = _rng.NextFloat(-_profile.OffsetRange, _profile.OffsetRange);
        _swayX = _rng.NextFloat(0f, 512f);
        _swayY = _rng.NextFloat(0f, 512f);
        _stride = _profile.StrideLength * (1f + _rng.NextFloat(-_profile.StrideJitter, _profile.StrideJitter));
        _phaseOffset = _rng.NextFloat();
        Tie = _rng.NextFloat();
#if UNITY_EDITOR
        _gizmoHue = _rng.NextFloat();
#endif
    }

    /// <summary>A 32-bit integer finaliser (lowbias32): consecutive ids land far apart.</summary>
    private static uint Hash(uint x)
    {
        x ^= x >> 16;
        x *= 0x7feb352du;
        x ^= x >> 15;
        x *= 0x846ca68bu;
        x ^= x >> 16;
        return x;
    }

    private void HandleVisualReady()
    {
        if (_ready) return;

        Path = clientMovement.Path;
        _curve = LaneCurve.For(Path);
        _reversed = enemyManager.ServerMovement.Reversed.Value;
        _lastProgress = clientMovement.DisplayProgress;
        _phase = _phaseOffset;
        _moveBlend = 0f;
        Speed = 0f;
        _walked = 0f;
        _steerLateral = 0f;
        _steerAlong = 0f;
        _headingVelocity = 0f;
        _turnRate = 0f;

        // Face down the lane from the first frame, standing on its own route.
        if (_curve != null && _curve.Length > 0f)
        {
            _routeLateral = RouteLateralAt(_lastProgress);
            float physical = _reversed ? 1f - _lastProgress : _lastProgress;
            _curve.Sample(physical, out _, out Vector2 tangent);
            _prevLateral = SoftClamp(_routeLateral, _profile.MaxLateral);
            Vector2 travel = _reversed ? -tangent : tangent;
            _targetHeading = _heading = Mathf.Atan2(travel.y, travel.x) * Mathf.Rad2Deg;
        }

#if UNITY_EDITOR
        _trailHead = _trailCount = 0;
        _nextTrailAt = 0f;
#endif
        _popStart = Time.time;
        _ready = true;
    }

    /// <summary>The seeded part of the route at <paramref name="progress"/>: offset plus sway, laid out per world unit.</summary>
    private float RouteLateralAt(float progress)
    {
        float walked = progress * (_curve != null ? _curve.Length : 0f);
        // Perlin rarely leaves 0.2..0.8; stretched so the authored amplitude is actually reached.
        float noise = Mathf.Clamp((Mathf.PerlinNoise(_swayX + walked * _profile.SwayFrequency, _swayY) - 0.5f) * 2.6f, -1f, 1f);
        return _baseLateral + _profile.SwayAmplitude * noise;
    }

    /// <summary>Pass 1: where along the lane this troop is, how fast it is going, where its route wants it.</summary>
    internal void Prepare(float dt)
    {
        Active = false;
        if (!_ready) return;

        WaypointPath path = clientMovement.Path;
        if (path == null) return;
        if (path != Path)
        {
            Path = path;
            _curve = LaneCurve.For(path);
        }
        if (_curve == null || _curve.Length <= 0f) return;

        float length = _curve.Length;
        float progress = clientMovement.DisplayProgress;
        _reversed = enemyManager.ServerMovement.Reversed.Value;
        Sign = _reversed ? -1f : 1f;
        S = (_reversed ? 1f - progress : progress) * length;

        // A pull back (Ancora) is a drag, not a walk: no steps for it.
        float walked = (progress - _lastProgress) * length;
        _lastProgress = progress;
        _walked = Mathf.Max(0f, walked);
        float rawSpeed = dt > 0f ? _walked / dt : 0f;
        Speed = Mathf.Lerp(Speed, rawSpeed, 1f - Mathf.Exp(-12f * dt));

        _routeLateral = RouteLateralAt(progress);
        Lateral = _routeLateral + _steerLateral;
        Along = _steerAlong;
        PushLateral = 0f;
        PushAlong = 0f;
        Active = true;
    }

    /// <summary>Pass 3, after <see cref="TroopCrowd"/> has summed the pushes: place, turn and shape the body.</summary>
    internal void Apply(float dt, BaseMapTranslator translator, TroopMotionSettingsSO settings, float time)
    {
        if (!Active) return;

        float calm = Juice.ReduceMotion ? 0.5f : 1f;
        float length = _curve.Length;

        // Steering: pushed apart by the crowd, drawn back onto the seeded route when there is room.
        float maxLateral = _profile.MaxLateral;
        _steerLateral += (PushLateral * settings.PushSpeed - _steerLateral * settings.ReturnRate) * dt;
        _steerAlong += (PushAlong * settings.PushSpeed - _steerAlong * settings.ReturnRate) * dt;
        _steerLateral = Mathf.Clamp(_steerLateral, -1.5f * maxLateral, 1.5f * maxLateral);
        _steerAlong = Mathf.Clamp(_steerAlong, -settings.MaxAlong, settings.MaxAlong);
        // Never ahead of the lane's end or behind its mouth: a crowd at the spawn spreads forward, not off the board.
        _steerAlong = Mathf.Clamp(_steerAlong, -S, length - S);

        float lateral = SoftClamp(_routeLateral + _steerLateral, maxLateral);

        // Gait: one step per stride walked, strides lengthening when the troop lunges (a dash, Rage).
        float speedRatio = _baseSpeed > 0.01f ? Speed / _baseSpeed : 0f;
        _moveBlend = Mathf.MoveTowards(_moveBlend, Speed > 0.06f ? 1f : 0f, dt * 5f);
        float cadence = 1f + 0.35f * Mathf.Max(0f, speedRatio - 1f);
        float previousPhase = _phase;
        _phase += _walked / (_stride * cadence);
        float step = Mathf.Sin(_phase * Mathf.PI); // alternates sign each step: left foot, right foot
        float hop = Mathf.Abs(step);                // 0 on a landing, 1 mid-step

        // Position: the rounded lane, the route's lateral, steering along it, then the zero-mean stride surge.
        _curve.Sample((S + _steerAlong) / length, out Vector2 centre, out Vector2 tangent);
        Vector2 normal = new Vector2(-tangent.y, tangent.x);
        Vector2 route = centre + normal * lateral;
        // A sine of amplitude A over a stride L varies the speed by 2*pi*A/L; solved for the authored share.
        float surgeAmplitude = _profile.SurgeShare * _stride * cadence / (2f * Mathf.PI);
        float surge = surgeAmplitude * calm * Mathf.Sin(_phase * 2f * Mathf.PI) * _moveBlend;
        Vector2 travel = tangent * Sign;
        Vector2 drawn = route + travel * surge;

        TeamType team = enemyManager.Team.GetTeamType();
        Vector3 local = translator != null ? translator.ServerToLocal(drawn, team) : (Vector3)drawn;
        local.z = transform.position.z;
        motion.position = local;

        // Facing: down the lane, tilted toward the side the body is drifting (sway, a sidestep) while it walks.
        // Built from the forward distance walked and the sideways drift rather than from the frame's raw motion:
        // a crowd's along-lane push can move a body backwards for a moment, and facing that would spin it round.
        float drift = lateral - _prevLateral;
        _prevLateral = lateral;
        float desired = Mathf.Atan2(travel.y, travel.x) * Mathf.Rad2Deg;
        if (_moveBlend > 0.01f && _walked > 1e-5f)
            desired += Sign * Mathf.Clamp(Mathf.Atan2(drift, _walked) * Mathf.Rad2Deg, -MaxDriftAngle, MaxDriftAngle);
        desired = _targetHeading + Mathf.DeltaAngle(_targetHeading, desired);
        float rate = dt > 0f ? (desired - _targetHeading) / dt : 0f;
        _turnRate = Mathf.Lerp(_turnRate, rate, 1f - Mathf.Exp(-10f * dt));
        _targetHeading = desired;

        float lean = Mathf.Clamp(_turnRate * _profile.LeanPerTurnRate, -_profile.MaxLean, _profile.MaxLean) * calm;
        StepHeading(_targetHeading + lean, dt);
        float waddle = _profile.WaddleAngle * calm * step * _moveBlend;
        gfx.rotation = Quaternion.Euler(0f, 0f, _heading + waddle - 90f);

        // Shape: squash on each landing, stretch mid-step, a lunge at speed, a hop toward the camera, a breath at rest.
        float stretch = _profile.StepStretch * calm * (2f * hop - 1f) * _moveBlend;
        float lunge = _profile.SpeedStretch * calm * Mathf.Clamp(speedRatio - 1.1f, 0f, 2.5f);
        float along = (1f + stretch) * (1f + lunge);
        float across = (1f - stretch * 0.7f) * (1f - lunge * 0.45f);
        float uniform = 1f + _profile.HopScale * calm * hop * _moveBlend;
        if (!Juice.ReduceMotion)
            uniform *= 1f + _profile.IdleBreath * (1f - _moveBlend)
                * Mathf.Sin((time * _profile.IdleBreathRate + _phaseOffset) * 2f * Mathf.PI);
        float pop = PopScale(time, calm);
        gfx.localScale = new Vector3(across * uniform * pop, along * uniform * pop, 1f);

        if (_moveBlend > 0.5f && Mathf.Floor(_phase) > Mathf.Floor(previousPhase))
            Land(local, travel, normal, settings);

#if UNITY_EDITOR
        if (time >= _nextTrailAt)
        {
            _nextTrailAt = time + 0.05f;
            _trail[_trailHead] = local;
            _trailHead = (_trailHead + 1) % TrailLength;
            if (_trailCount < TrailLength) _trailCount++;
        }
        _gizmoTranslator = translator;
#endif
    }

    /// <summary>A damped spring on the heading: slightly under-damped, so a turn overshoots a touch and settles.</summary>
    private void StepHeading(float goal, float dt)
    {
        float omega = 2f * Mathf.PI * _profile.TurnFrequency;
        float damping = 2f * _profile.TurnDamping * omega;
        // Sub-stepped: semi-implicit Euler on a stiff spring is only stable for small steps.
        float remaining = Mathf.Min(dt, 0.1f);
        while (remaining > 0f)
        {
            float h = Mathf.Min(remaining, 1f / 90f);
            _headingVelocity += (omega * omega * (goal - _heading) - damping * _headingVelocity) * h;
            _heading += _headingVelocity * h;
            remaining -= h;
        }
    }

    /// <summary>easeOutBack from 0 to 1 over the profile's pop, overshoot halved under Reduce motion.</summary>
    private float PopScale(float time, float calm)
    {
        float t = Mathf.Clamp01((time - _popStart) / _profile.SpawnPopDuration);
        if (t >= 1f) return 1f;
        float c1 = _profile.SpawnPopOvershoot * calm;
        float c3 = c1 + 1f;
        float x = t - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    /// <summary>Linear inside 70% of the limit, then eased into it, so a pushed troop slides to the lane edge instead of hitting a wall.</summary>
    private static float SoftClamp(float value, float limit)
    {
        if (limit <= 0f) return 0f;
        float knee = 0.7f * limit;
        float magnitude = Mathf.Abs(value);
        if (magnitude <= knee) return value;
        float room = limit - knee;
        return Mathf.Sign(value) * (knee + room * (float)System.Math.Tanh((magnitude - knee) / room));
    }

    /// <summary>A foot comes down: sometimes a puff of dust behind it, within the board's budget, only where seen.</summary>
    private void Land(Vector3 local, Vector2 travel, Vector2 normal, TroopMotionSettingsSO settings)
    {
        float roll = _rng.NextFloat();
        float side = _rng.NextFloat(-1f, 1f);
        if (roll > _profile.DustChance) return;
        if (!CameraFeedback.IsOnScreen(local, 0.05f)) return;
        if (!TroopCrowd.TryTakeDust()) return;

        Vector3 back = -(Vector3)travel;
        Vector3 at = local + back * (0.6f * Radius);
        Vector3 drift = back * (0.12f * _sizeK) + (Vector3)(normal * (0.05f * side));
        WorldFx.GroundPuff(at, settings.DustColor, _profile.DustSize * _sizeK, drift, 0.45f);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Scene view, play mode: the trail this troop actually walked (solid) and the route its seed lays out ahead of
    /// it (dashed, without steering, which depends on the neighbours it will meet).
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !_ready || _curve == null || _profile == null) return;
        if (!TroopMotionSettingsSO.Instance.DrawRouteGizmos) return;

        Color hue = Color.HSVToRGB(_gizmoHue, 0.8f, 1f);
        Gizmos.color = hue;
        for (int i = 1; i < _trailCount; i++)
        {
            Vector3 newer = _trail[(_trailHead - i + TrailLength) % TrailLength];
            Vector3 older = _trail[(_trailHead - i - 1 + TrailLength) % TrailLength];
            Gizmos.DrawLine(older, newer);
        }

        hue.a = 0.6f;
        Gizmos.color = hue;
        float length = _curve.Length;
        Vector3 previous = RoutePointAt(_lastProgress);
        for (int k = 1; k <= 60; k++)
        {
            float progress = _lastProgress + k * 0.06f / length;
            if (progress > 1f) break;
            Vector3 next = RoutePointAt(progress);
            if ((k & 1) == 0) Gizmos.DrawLine(previous, next);
            previous = next;
        }

        Gizmos.DrawWireSphere(motion.position, Radius);
    }

    private Vector3 RoutePointAt(float progress)
    {
        float physical = _reversed ? 1f - progress : progress;
        _curve.Sample(physical, out Vector2 centre, out Vector2 tangent);
        Vector2 point = centre + new Vector2(-tangent.y, tangent.x) * SoftClamp(RouteLateralAt(progress), _profile.MaxLateral);
        return _gizmoTranslator != null
            ? _gizmoTranslator.ServerToLocal(point, enemyManager.Team.GetTeamType())
            : (Vector3)point;
    }
#endif
}
