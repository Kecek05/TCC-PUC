using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// How an enemy reads under fire: a flash and a number on every hit, a squash and a brief freeze on a heavy
/// one, a puff when it arrives and a burst when it dies.
/// </summary>
/// <remarks>
/// Client-side and event-driven. Hits come from the replicated health (a drop is a hit, and the drop is its
/// damage — no extra traffic), deaths and leaks from <see cref="EnemyFeedbackRelay"/>. A NetworkBehaviour only
/// for the spawn/despawn hooks: enemies are pooled, so every life has to start from a clean body, and
/// OnNetworkSpawn/OnNetworkDespawn are the hooks a recycled instance actually gets.
/// <para/>
/// The heavy-hit freeze pins the <c>GFX</c> child in world space and lets the root move on underneath it,
/// then eases it back. It has to be the child: on the host the server moves the root every frame for tower
/// targeting, so holding the root would either do nothing or pause the simulation for both players.
/// <para/>
/// The flash and the pin are driven here rather than by DOTween or an MMF_Player because hits are the most
/// frequent event in a match and both of those allocate per play. <see cref="MonoBehaviour.enabled"/> is only
/// on while one of them is running, so an enemy nobody is shooting costs nothing per frame.
/// </remarks>
public class EnemyFeedbackPresenter : NetworkBehaviour
{
    private const float SettledDistanceSqr = 0.0001f;

    [Title("References")]
    [SerializeField, Required] private EnemyManager enemyManager;
    [SerializeField, Required] private EnemyFeedbackRelay relay;
    [SerializeField, Required] private ClientEnemyMovement clientMovement;
    [Tooltip("The visual container that the heavy-hit freeze pins in place (GFX).")]
    [SerializeField, Required] private Transform visual;
    [Tooltip("The enemy's body sprite (not the health bar), tinted by its armour.")]
    [SerializeField, Required] private SpriteRenderer body;
    [SerializeField, Required] private HitFeedbackSettingsSO settings;

    [Title("Feedbacks")]
    [Tooltip("Squash on a heavy hit. Scaled time, so it slows with the end cutscene like the hit itself.")]
    [SerializeField] private MMF_Player heavyHitFeedback;

    [Title("Hit-stop")]
    [Tooltip("How fast the pinned visual catches back up with the moving enemy, per second.")]
    [SerializeField, Min(1f)] private float catchUpSharpness = 30f;

    /// <summary>Kills close together climb a semitone each (EnemyHit: death, laddered), so a wave cleared fast sings.</summary>
    private static float s_lastKillAt = -10f;
    private static int s_killStep;

    /// <summary>One INCOMING warning per column of sent troops, not one per troop.</summary>
    private static float s_lastIncomingAt = -10f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_lastKillAt = -10f;
        s_killStep = 0;
        s_lastIncomingAt = -10f;
    }

    private BaseVfxPool _vfx;
    private Color _bodyColor;
    private float _lastHealth;
    private float _flashLeft;
    private float _pinLeft;
    private Vector3 _pinnedPosition;
    private bool _catchingUp;
    private bool _listening;

    public override void OnNetworkSpawn()
    {
        // A dedicated server has nobody to show any of this to.
        if (IsServer && !IsClient)
        {
            enabled = false;
            return;
        }

        ServiceLocator.TryGet(out _vfx);

        _bodyColor = body.color;
        _lastHealth = enemyManager.ServerHealth.CurrentHealth.Value;
        ResetVisualState();

        enemyManager.ServerHealth.CurrentHealth.OnValueChanged += HandleHealthChanged;
        relay.OnExit += HandleExit;
        clientMovement.OnVisualReady += HandleVisualReady;
        _listening = true;
    }

    public override void OnNetworkDespawn()
    {
        if (!_listening) return;
        _listening = false;

        enemyManager.ServerHealth.CurrentHealth.OnValueChanged -= HandleHealthChanged;
        relay.OnExit -= HandleExit;
        clientMovement.OnVisualReady -= HandleVisualReady;

        // Pooled: the next life must not inherit a half-faded flash, a pinned visual or a mid-squash scale.
        if (heavyHitFeedback != null && heavyHitFeedback.IsPlaying) heavyHitFeedback.StopFeedbacks();
        ResetVisualState();
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        bool busy = false;

        if (_flashLeft > 0f)
        {
            _flashLeft -= dt;
            body.color = _flashLeft > 0f
                ? Color.Lerp(_bodyColor, settings.FlashColor, _flashLeft / settings.FlashDuration)
                : _bodyColor;
            busy |= _flashLeft > 0f;
        }

        if (_pinLeft > 0f)
        {
            // After the movement scripts have placed the root this frame, so the pin wins.
            _pinLeft -= dt;
            visual.position = _pinnedPosition;
            _catchingUp = _pinLeft <= 0f;
            busy = true;
        }
        else if (_catchingUp)
        {
            visual.localPosition = Vector3.Lerp(visual.localPosition, Vector3.zero, 1f - Mathf.Exp(-catchUpSharpness * dt));
            _catchingUp = visual.localPosition.sqrMagnitude > SettledDistanceSqr;
            if (!_catchingUp) visual.localPosition = Vector3.zero;
            busy |= _catchingUp;
        }

        enabled = busy;
    }

    private void HandleHealthChanged(float previous, float current)
    {
        float damage = previous - current;
        _lastHealth = current;
        if (damage <= 0.01f) return;

        Flash();

        float max = enemyManager.ServerHealth.MaxHealth.Value > 0f
            ? enemyManager.ServerHealth.MaxHealth.Value
            : enemyManager.Data.MaxHealth;
        bool heavy = damage >= settings.HeavyHitFraction * max;

        // Heard only where it is seen: the other field fights on unheard. The sound service's per-cue cooldown
        // is what keeps a busy wave from turning into noise.
        if (CameraFeedback.IsOnScreen(body.transform.position)) Juice.Sfx(heavy ? SfxId.Heavy : SfxId.Hit);

        if (settings.ShowDamageNumbers)
        {
            FloatingNumbers.Damage(body.transform.position + settings.NumberOffset, damage,
                heavy ? settings.HeavyNumberIntensity : 1f,
                heavy ? settings.HeavyNumberColor : settings.NumberColor);
        }

        if (!heavy) return;

        Pin(settings.HitStopSeconds);
        if (heavyHitFeedback != null) heavyHitFeedback.PlayFeedbacks();
    }

    private void HandleExit(EnemyExitKind kind)
    {
        Vector3 position = body.transform.position;

        if (kind == EnemyExitKind.Killed)
        {
            // A remote client never sees the killing blow replicate (see EnemyFeedbackRelay), so the health it
            // last knew is what that blow took. On the host it has already been shown and this is 0.
            if (_lastHealth > 0.01f && settings.ShowDamageNumbers)
                FloatingNumbers.Damage(position + settings.NumberOffset, _lastHealth, 1f, settings.NumberColor);
            _lastHealth = 0f;

            SpawnVfx(settings.DeathBurst, position);
            if (CameraFeedback.IsOnScreen(position)) Juice.Sfx(SfxId.Death, NextKillStep());
            return;
        }

        SpawnVfx(settings.LeakBurst, position);
        if (!CameraFeedback.IsOnScreen(position)) return;

        // BaseDamage's leak: a ring in the deny colour and sparks where it got through, then the leak sound.
        JuiceTokensSO t = Juice.Tokens;
        WorldFx.Ring(position, t.Colors.Deny, 0.25f, 0.9f, t.Durations.Settle);
        WorldFx.Sparks(position, Color.Lerp(t.Colors.Deny, t.Colors.Sparkle, 0.3f), 7, 0.75f, 0.16f);
        Juice.Sfx(SfxId.Leak);
    }

    private static int NextKillStep()
    {
        float now = Time.unscaledTime;
        s_killStep = now - s_lastKillAt < 0.7f ? Mathf.Min(s_killStep + 1, 8) : 0;
        s_lastKillAt = now;
        return s_killStep;
    }

    // After ClientEnemyMovement has placed the enemy in this client's view — on the mirrored Blue client the
    // spawn position is still server space until then.
    private void HandleVisualReady()
    {
        Vector3 position = body.transform.position;
        SpawnVfx(settings.SpawnPuff, position);
        WarnIfIncoming(position);
    }

    /// <summary>
    /// TroopSend, the defender's half: troops the opponent sent arrive on this player's lane (a reversed enemy
    /// attacking the local map). They sit invincible and still through their spawn-in (SpawnDuration), which is
    /// exactly the warning window: a horn, a red ring at the gate and INCOMING! over it, once per column.
    /// The server's schedule is untouched; the warning only fills a pause the troops already had.
    /// </summary>
    private void WarnIfIncoming(Vector3 position)
    {
        if (!enemyManager.ServerMovement.Reversed.Value) return;
        if (!ServiceLocator.TryGet(out BaseTeamManager teams) || !teams.HasLocalTeamBeenAssigned()) return;
        if (enemyManager.Team == null || enemyManager.Team.GetTeamType() != teams.GetLocalTeam()) return;

        float now = Time.unscaledTime;
        if (now - s_lastIncomingAt < 1.5f) return;
        s_lastIncomingAt = now;

        JuiceTokensSO t = Juice.Tokens;
        WorldFx.Ring(position, t.Colors.Deny, 0.2f, 1.3f);
        FloatingNumbers.Say(position + Vector3.up * 0.7f, "INCOMING!", 1.6f, t.Colors.Deny);
        Juice.Sfx(SfxId.Horn);
        Juice.Haptic(HapticRole.Light);
    }

    private void Flash()
    {
        if (settings.FlashDuration <= 0f) return;

        _flashLeft = settings.FlashDuration;
        body.color = settings.FlashColor;
        enabled = true;
    }

    private void Pin(float seconds)
    {
        if (seconds <= 0f) return;

        // A pin already running keeps its spot; a fresh one starts from wherever the visual is now.
        if (_pinLeft <= 0f) _pinnedPosition = visual.position;
        _pinLeft = Mathf.Max(_pinLeft, seconds);
        enabled = true;
    }

    private void ResetVisualState()
    {
        _flashLeft = 0f;
        _pinLeft = 0f;
        _catchingUp = false;
        body.color = _bodyColor;
        body.transform.localScale = Vector3.one;
        visual.localPosition = Vector3.zero;
        enabled = false;
    }

    private void SpawnVfx(PooledVfxSO vfx, Vector3 position)
    {
        if (vfx == null || _vfx == null) return;
        _vfx.Spawn(vfx, position, _bodyColor);
    }
}
