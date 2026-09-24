using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// The tower-side half of game feel: dust where a tower lands, a sparkle when it levels, a kick away from the
/// target on every shot, and a "+N" when a Fonte pays out. The camera punch and the haptic only play for the
/// local player's own towers — the opponent's are on the other field, usually off screen.
/// </summary>
/// <remarks>
/// Sits beside <see cref="ClientTowerGFX"/>, which still owns the authored spawn/upgrade/shoot animations; this
/// adds to them rather than replacing them. Subscribes in Awake for the same reason ClientTowerGFX does:
/// <see cref="BaseClientTowerCombat"/> replays the starting level inside OnNetworkSpawn, before Start.
/// Placement and upgrade effects are held to the next frame, because on the mirrored Blue client the tower is
/// only moved into this client's view after that replay.
/// </remarks>
public class TowerFeedbackPresenter : MonoBehaviour
{
    [Title("References")]
    [SerializeField, Required] private TowerFeedbackSettingsSO settings;
    [Tooltip("Position spring on the tower's art (GFX), kicked on every shot. Optional: support towers never fire.")]
    [SerializeField] private MMSpringPosition recoilSpring;

    [Title("Own Tower Feedbacks")]
    [Tooltip("Played when the local player's own tower lands: camera punch + haptic.")]
    [SerializeField] private MMF_Player ownPlacedFeedback;
    [Tooltip("Played when the local player's own tower levels up: haptic.")]
    [SerializeField] private MMF_Player ownUpgradedFeedback;

    [Title("Fonte")]
    [Tooltip("Only on the Fonte: its payouts are shown as \"+N\" over the tower.")]
    [SerializeField] private ManaGrantFeedbackRelay manaRelay;

    private BaseClientTowerCombat _clientCombat;
    private EntityTeam _entityTeam;
    private int _level;
    private bool _pendingPlaced;
    private bool _pendingUpgrade;

    private void Awake()
    {
        // Looked up rather than serialized: tower variants swap in their own client combat, so a reference
        // authored on BaseTower would point at a component the variant removed.
        _clientCombat = GetComponent<BaseClientTowerCombat>();
        _entityTeam = GetComponent<EntityTeam>();

        if (_clientCombat != null)
        {
            _clientCombat.OnTowerLevelChanged += HandleLevelChanged;
            _clientCombat.OnBulletFiredAt += HandleFiredAt;
        }
        if (manaRelay != null) manaRelay.OnManaGranted += HandleManaGranted;

        enabled = false;
    }

    private void OnDestroy()
    {
        if (_clientCombat != null)
        {
            _clientCombat.OnTowerLevelChanged -= HandleLevelChanged;
            _clientCombat.OnBulletFiredAt -= HandleFiredAt;
        }
        if (manaRelay != null) manaRelay.OnManaGranted -= HandleManaGranted;
    }

    // Runs for one frame after a level change, then switches itself off.
    private void Update()
    {
        enabled = false;
        if (!IsPresenting()) return;

        bool own = IsOwnTower();
        Vector3 position = transform.position;

        if (_pendingPlaced)
        {
            _pendingPlaced = false;
            SpawnVfx(settings.PlacedDust, position);
            if (own && ownPlacedFeedback != null) ownPlacedFeedback.PlayFeedbacks();
        }

        if (_pendingUpgrade)
        {
            _pendingUpgrade = false;
            SpawnVfx(settings.UpgradeSparkle, position);
            if (own && ownUpgradedFeedback != null) ownUpgradedFeedback.PlayFeedbacks();
        }
    }

    private void HandleLevelChanged(int level)
    {
        if (level <= 0) return;

        if (_level == 0) _pendingPlaced = true;
        else if (level > _level) _pendingUpgrade = true;

        _level = level;
        enabled = _pendingPlaced || _pendingUpgrade;
    }

    private void HandleFiredAt(Vector3 targetPosition)
    {
        if (recoilSpring == null) return;

        Vector3 away = transform.position - targetPosition;
        away.z = 0f;
        if (away.sqrMagnitude < 0.0001f) return;

        recoilSpring.Bump(away.normalized * settings.RecoilKick);
    }

    private void HandleManaGranted(float amount)
    {
        if (!IsPresenting()) return;

        FloatingNumbers.Gain(transform.position + settings.ManaPopupOffset, amount, settings.ManaPopupIntensity,
            settings.ManaPopupColor);
    }

    private bool IsOwnTower()
    {
        if (_entityTeam == null) return false;
        if (!ServiceLocator.TryGet(out BaseTeamManager teams) || !teams.HasLocalTeamBeenAssigned()) return false;
        return _entityTeam.GetTeamType() == teams.GetLocalTeam();
    }

    // A dedicated server has no screen.
    private static bool IsPresenting()
    {
        NetworkManager network = NetworkManager.Singleton;
        return network == null || network.IsClient;
    }

    private static void SpawnVfx(PooledVfxSO vfx, Vector3 position)
    {
        if (vfx == null || !ServiceLocator.TryGet(out BaseVfxPool pool)) return;
        pool.Spawn(vfx, position);
    }
}
