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
            return;
        }

        SpawnVfx(settings.LeakBurst, position);
    }

    // After ClientEnemyMovement has placed the enemy in this client's view — on the mirrored Blue client the
    // spawn position is still server space until then.
    private void HandleVisualReady() => SpawnVfx(settings.SpawnPuff, body.transform.position);

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
