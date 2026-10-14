using DG.Tweening;
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

    [Title("Level Badge")]
    [Tooltip("The tower's in-match tier as a small numbered badge on its top-right corner, from tier 2 on " +
             "(TowerLevelUp). It sits inside the tower's own cell, so it never covers a neighbour; tier 1 shows " +
             "nothing, the art itself being the base.")]
    [SerializeField] private bool showLevelBadge = true;
    [SerializeField, Min(0.05f)] private float badgeSize = 0.24f;
    [Tooltip("Where the badge's centre sits, as a share of the way from the tower's centre to the corner of the " +
             "shape it draws. Under 1 it overlaps the art.")]
    [SerializeField, Range(0.3f, 1.2f)] private float badgeInset = 0.72f;

    [Tooltip("How long the attention ring takes to close in on a levelling tower before it bursts.")]
    [SerializeField, Min(0f)] private float levelUpCharge = 0.2f;

    private BaseClientTowerCombat _clientCombat;
    private ClientTowerGFX _gfx;
    private TowerManager _tower;
    private Transform _badge;
    private SpriteRenderer _badgeFill;
    private SpriteRenderer _badgeRing;
    private TMPro.TextMeshPro _badgeLabel;
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
        _tower = GetComponent<TowerManager>();
        _gfx = GetComponentInChildren<ClientTowerGFX>(true);
        if (_gfx != null) _gfx.OnSpawnLanded += HandleSpawnLanded;

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
        if (_gfx != null) _gfx.OnSpawnLanded -= HandleSpawnLanded;
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

            // A tower that drops in lands a moment later; its dust and thud wait for the touchdown.
            if (_gfx == null || !_gfx.DropsOnSpawn) Land(own, position);
            else if (CameraFeedback.IsOnScreen(position)) Juice.Sfx(SfxId.Whoosh, 0, 0.6f);
        }

        if (_pendingUpgrade)
        {
            _pendingUpgrade = false;
            PlayLevelUp(own, position);
        }
    }

    /// <summary>
    /// TowerLevelUp: an attention ring closes in on the tower (the charge), then it bursts: a white flash, a
    /// shockwave in the tower's own tint, sparkle, the level-up chime, a small shake and, for the own tower, the
    /// Success buzz. The new level's pip lights and the reach ring grows out to the new range. The next level's
    /// art nesting in stays ClientTowerGFX's authored feedback, which the tutorial measures.
    /// </summary>
    private void PlayLevelUp(bool own, Vector3 position)
    {
        JuiceTokensSO t = Juice.Tokens;
        int level = _level;

        if (CameraFeedback.IsOnScreen(position))
        {
            WorldFx.Ring(position, t.Colors.Attention, 1.4f, 0.35f, levelUpCharge);
            Juice.Sfx(SfxId.Charge, 0, 0.7f);
        }

        DOVirtual.DelayedCall(levelUpCharge, () =>
        {
            if (this == null) return;

            Vector3 at = transform.position;
            SpawnVfx(settings.UpgradeSparkle, at);
            if (own && ownUpgradedFeedback != null) ownUpgradedFeedback.PlayFeedbacks();
            ShowBadge(level, pop: true);
            if (!CameraFeedback.IsOnScreen(at)) return;

            WorldFx.Flash(at, Color.white, 0.9f, t.Durations.Pop);
            WorldFx.Ring(at, TowerTint, 0.3f, 1.6f);
            WorldFx.Sparks(at, t.Colors.Sparkle, 8, 1.1f);
            GrowReach(at, level);
            Juice.Sfx(SfxId.LevelUp);
            JuiceCamera.Shake(t.Shake.Small, t.Durations.Shake);
        }).SetLink(gameObject);
    }

    /// <summary>The reach ring grows from the old range out to the new one, then fades.</summary>
    private void GrowReach(Vector3 at, int level)
    {
        if (_tower == null || _tower.Data == null || level <= 1) return;

        float before = _tower.Data.GetRangeByLevel(level - 1);
        float after = _tower.Data.GetRangeByLevel(level);
        if (after <= 0f) return;

        Color ink = Juice.Tokens.Colors.Ink;
        ink.a = 0.6f;
        WorldFx.Ring(at, ink, Mathf.Max(0.1f, before), Mathf.Max(before, after) * 1.02f, Juice.Tokens.Durations.Settle);
    }

    private Color TowerTint => _gfx != null ? _gfx.Tint : Juice.Tokens.Colors.Attention;

    /// <summary>
    /// The tier badge: the level number on a disc in the tower's own tint, ringed dark, or gold once the tower is at
    /// its top tier. Hidden at tier 1. It appears with a pop and punches on each further level. Built on first use,
    /// under the tower's root, so the spawn drop and a level's squash never move it.
    /// </summary>
    private void ShowBadge(int level, bool pop)
    {
        if (!showLevelBadge || _tower == null || _tower.Data == null) return;
        if (level < 2)
        {
            if (_badge != null) _badge.gameObject.SetActive(false);
            return;
        }

        if (_badge == null) BuildBadge();

        JuiceTokensSO t = Juice.Tokens;
        Color tint = TowerTint;
        tint.a = 1f;
        _badgeFill.color = tint;
        _badgeRing.color = level >= _tower.Data.MaxLevel ? t.Colors.Attention : t.Colors.Sheet;
        _badgeLabel.text = level.ToString();

        bool appearing = !_badge.gameObject.activeSelf;
        _badge.gameObject.SetActive(true);
        if (!pop || Juice.ReduceMotion) return;

        if (appearing) JuiceFx.PopIn(_badge, 0f, 1f, -1f, unscaled: false);
        else JuiceFx.Punch(_badge, t.Scales.PunchHard, -1f, unscaled: false);
    }

    private void BuildBadge()
    {
        SpriteRenderer art = _gfx != null ? _gfx.ArtRenderer : null;
        Vector2 extents = new(0.29f, 0.29f);
        if (art != null && art.sprite != null)
        {
            // Half of the shape the tower draws, not of its sprite's rect: a tower whose canvas is roomier than its
            // shape (Dart) threw the badge a whole cell away.
            float k = art.transform.lossyScale.x / Mathf.Max(0.0001f, transform.lossyScale.x);
            extents = VisibleBounds.SpriteSize(art.sprite) * 0.5f * k;
        }

        int layer = art != null ? art.sortingLayerID : 0;
        int order = art != null ? art.sortingOrder : 0;

        // Sitting ON the art's top-right corner, overlapping it, so it reads as part of the tower and never as a
        // thing beside it.
        GameObject root = new GameObject("LevelBadge");
        _badge = root.transform;
        _badge.SetParent(transform, false);
        _badge.localPosition = new Vector3(extents.x * badgeInset, extents.y * badgeInset, 0f);

        _badgeRing = BadgePart("Ring", badgeSize * 1.25f, layer, order + 2);
        _badgeFill = BadgePart("Fill", badgeSize, layer, order + 3);

        GameObject label = new GameObject("Label", typeof(RectTransform));
        label.transform.SetParent(_badge, false);
        _badgeLabel = label.AddComponent<TMPro.TextMeshPro>();
        RefusalCopySO copy = Resources.Load<RefusalCopySO>(RefusalCopySO.ResourcePath);
        if (copy != null && copy.Font != null) _badgeLabel.font = copy.Font;
        // The game's own number: Fredoka Bold in ink inside its black outline, like a card's mana cost. (In the nav
        // colour the outline swallowed the digit and the disc with it.)
        _badgeLabel.fontSize = badgeSize * 11f;
        _badgeLabel.fontStyle = TMPro.FontStyles.Normal;
        // At this size the font's full outline leaves a grey sliver of a digit; a lighter one keeps the face white.
        Material badgeMaterial = BadgeMaterial(_badgeLabel.font);
        if (badgeMaterial != null) _badgeLabel.fontSharedMaterial = badgeMaterial;
        _badgeLabel.alignment = TMPro.TextAlignmentOptions.Center;
        _badgeLabel.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        _badgeLabel.color = Juice.Tokens.Colors.Ink;
        _badgeLabel.rectTransform.sizeDelta = new Vector2(badgeSize * 2f, badgeSize * 1.5f);
        _badgeLabel.sortingLayerID = layer;
        _badgeLabel.sortingOrder = order + 4;

        root.SetActive(false);
    }

    private static Material s_badgeMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetBadgeMaterial() => s_badgeMaterial = null;

    /// <summary>One material for every tower's badge: the game font's own, with a thinner outline.</summary>
    private static Material BadgeMaterial(TMPro.TMP_FontAsset font)
    {
        if (s_badgeMaterial != null || font == null) return s_badgeMaterial;
        s_badgeMaterial = new Material(font.material) { name = "TierBadgeLabel", hideFlags = HideFlags.DontSave };
        s_badgeMaterial.SetFloat(TMPro.ShaderUtilities.ID_OutlineWidth, 0.3f);
        return s_badgeMaterial;
    }

    private SpriteRenderer BadgePart(string name, float size, int layer, int order)
    {
        GameObject go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(_badge, false);
        go.transform.localScale = Vector3.one * (size / 1.28f);
        SpriteRenderer part = go.GetComponent<SpriteRenderer>();
        part.sprite = JuiceSprites.Disc;
        part.sortingLayerID = layer;
        part.sortingOrder = order;
        return part;
    }

    /// <summary>The drop touched down.</summary>
    private void HandleSpawnLanded()
    {
        if (!IsPresenting()) return;
        Land(IsOwnTower(), transform.position);
    }

    /// <summary>
    /// TowerPlacement landing: dust, the place thud and, for the own tower, the camera kick and buzz; its tier badge
    /// shows if it already has one (a late join), and its reach pulses once so the player sees what it covers.
    /// </summary>
    private void Land(bool own, Vector3 position)
    {
        SpawnVfx(settings.PlacedDust, position);
        if (own && ownPlacedFeedback != null) ownPlacedFeedback.PlayFeedbacks();
        ShowBadge(_level, pop: false);

        if (!CameraFeedback.IsOnScreen(position)) return;
        Juice.Sfx(SfxId.Place);
        Color dust = Juice.Tokens.Colors.Dust;
        dust.a = 0.45f;
        WorldFx.Ring(position, dust, 0.25f, 0.95f, Juice.Tokens.Durations.Settle);

        float reach = _tower != null && _tower.Data != null ? _tower.Data.GetRangeByLevel(Mathf.Max(1, _level)) : 0f;
        if (reach <= 0f) return;
        Color ink = Juice.Tokens.Colors.Ink;
        ink.a = 0.55f;
        WorldFx.Ring(position, ink, reach * 0.85f, reach * 1.02f, Juice.Tokens.Durations.Settle);
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
        if (IsOwnTower()) Juice.Sfx(SfxId.Mana);
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
