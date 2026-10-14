using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

/// <summary>
/// Where a dragged spell will land (SpellCast's aim): a disc the size of the spell's reach in the spell's own hue,
/// ringed by a slowly turning dashed outline, an attention bracket on everything inside it the spell will touch, and a
/// tag on each saying what will happen to it (Spell Juice: "the aim tells the truth"). A tick and a buzz each time
/// something new comes into the circle.
/// </summary>
/// <remarks>
/// Pure presentation. Unscaled time throughout: spells are aimed while the tutorial holds the clock. Enemies are
/// found by a short periodic scan of the scene rather than the server's <see cref="EnemyRegistry"/>, which a
/// joining client does not fill; both the aim and the enemies are in the local view, so the distances agree.
/// <para/>
/// What it brackets is what the spell touches: enemies for a damage spell, the towers for Haste and Ice (Ice freezes
/// towers, so bracketing the troops walking past was a lie), only armoured enemies for Ferrugem and none that ignore
/// slows for Rift. Lance's area is not a circle at all: it locks the nearest enemy and lays its strip along the lane.
/// </remarks>
public class GhostSpellCard : MonoBehaviour
{
    private enum AimTargets { Enemies, Towers, ArmouredEnemies, SlowableEnemies }

    [Title("References")]
    [SerializeField] private GameObject gfxObject;
    [SerializeField] private SpriteRenderer gfxSprite;

    [Title("Aim")]
    [Tooltip("The disc under the dashed radius when no spell has configured it (the design's tint-plain at 14%).")]
    [SerializeField] private Color fillColor = new(1f, 1f, 1f, 0.14f);
    [SerializeField] private Color ringColor = new(1f, 1f, 1f, 0.9f);
    [SerializeField] private float ringSpinDegreesPerSecond = 60f;

    [Title("Targets")]
    [SerializeField] private float targetScanSeconds = 0.12f;
    [Tooltip("A bracket's size as a multiple of the enemy's sprite.")]
    [SerializeField] private float bracketScale = 1.5f;

    private SpriteRenderer _ring;
    private float _radius;
    private bool _visible;
    private float _nextScan;
    private Tween _appear;

    private readonly Dictionary<Component, SpriteRenderer> _brackets = new();
    private readonly Dictionary<Component, SpriteRenderer> _locked = new();
    private readonly Dictionary<Component, AimTag> _tags = new();
    private Tween _lockFade;
    private readonly List<Component> _scratch = new();
    private bool _targetsTowers;
    private readonly Stack<SpriteRenderer> _freeBrackets = new();
    private readonly Stack<AimTag> _freeTags = new();

    // The disc in the spell's hue (thin in the middle, gathering at the rim) and Fireball's centre pip.
    private SpriteRenderer _disc;
    private SpriteRenderer _pip, _pipRim;
    private readonly List<SpriteRenderer> _frost = new();

    // Set per drag by Configure: which spell this is aiming, at which level, on whose field.
    private SpellType _spell = SpellType.None;
    private SpellCardDataSO _card;
    private CardLevelScale _scale = CardLevelScale.One;
    private TeamType _fieldTeam = TeamType.None;
    private AimTargets _targets = AimTargets.Enemies;
    private Color _hue = Color.white;

    // Lance: a strip along the lane, a lock on its anchor, a no-target state.
    private bool _strip;
    private LineRenderer _stripBand, _stripSpine;
    private SpriteRenderer _lock;
    private AimTag _noTarget;
    private EnemyManager _anchor;
    private readonly List<Vector3> _band = new();

    /// <summary>World size of a 256px JuiceSprites shape at scale 1, and where the dashed line sits in it.</summary>
    private const float RingSpriteRadius = 1.28f * 0.95f;

    private void Awake()
    {
        SetVisible(false);
    }

    private void OnDisable()
    {
        ClearBrackets();
        ReleaseLocked();
    }

    /// <summary>
    /// The spell being aimed, set on each drag: its hue, what it touches, its card level (for the tags) and the field
    /// it lands on. Called after <see cref="SetSprite"/> and <see cref="SetScale"/>, so it can restyle what they set.
    /// </summary>
    public void Configure(SpellCardDataSO card, CardLevelScale scale, TeamType fieldTeam)
    {
        ClearBrackets();
        _card = card;
        _spell = card != null ? card.SpellType : SpellType.None;
        _scale = scale;
        _fieldTeam = fieldTeam;
        _hue = SpellFxSettingsSO.Instance.HueFor(_spell);

        _targets = _spell switch
        {
            SpellType.Haste or SpellType.Ice => AimTargets.Towers,
            SpellType.Ferrugem => AimTargets.ArmouredEnemies,
            SpellType.Rift => AimTargets.SlowableEnemies,
            _ => AimTargets.Enemies
        };
        _targetsTowers = _targets == AimTargets.Towers;

        _strip = _spell == SpellType.Lance;
        if (_strip && card != null && card.SpellData != null)
        {
            // Lance's real reach: the anchor is searched for within 1.5x its half-width (LanceExecutor).
            SetScale(card.SpellData.Range * scale.Range * 3f);
        }

        EnsureRing();
        StyleDisc();
        _ring.color = WithAlpha(_strip ? Juice.Tokens.Colors.InkMuted : _hue, ringColor.a);
        HideStrip();
    }

    /// <summary>
    /// The disc under the ring: the spell's hue, thin in the middle so what stands in it stays readable and
    /// gathering toward the rim. Lance's is barely there (its real area is the strip); Fireball marks the exact
    /// point of impact with a pip; Ice frosts its disc with six hairlines.
    /// </summary>
    private void StyleDisc()
    {
        if (_disc == null)
        {
            _disc = Child("AimDisc", JuiceSprites.AimDisc, gfxSprite.sortingOrder);
            _pipRim = Child("AimPipRim", JuiceSprites.Disc, gfxSprite.sortingOrder + 1);
            _pip = Child("AimPip", JuiceSprites.Disc, gfxSprite.sortingOrder + 2);
            for (int i = 0; i < 3; i++) _frost.Add(Child("AimFrost", JuiceSprites.Disc, gfxSprite.sortingOrder));
        }

        // The authored sprite steps aside for the gradient one; it still sizes the aim (SetScale).
        gfxSprite.color = Color.clear;
        float edge = SpellFxSettingsSO.Instance.Aim.DiscAlpha;
        bool gradient = !_strip;
        _disc.sprite = gradient ? JuiceSprites.AimDisc : JuiceSprites.Disc;
        _disc.color = gradient ? WithAlpha(_hue, edge) : new Color(1f, 1f, 1f, 0.05f);
        _disc.transform.localScale = Vector3.one * (_radius * 2f / (gradient ? 2.56f : 1.28f));

        bool pip = _spell == SpellType.Fireball;
        _pip.gameObject.SetActive(pip);
        _pipRim.gameObject.SetActive(pip);
        _pip.color = _hue;
        _pipRim.color = new Color(0f, 0f, 0f, 0.35f);
        _pip.transform.localScale = Vector3.one * (0.143f / 1.28f);
        _pipRim.transform.localScale = Vector3.one * (0.215f / 1.28f);

        bool frost = _spell == SpellType.Ice;
        for (int i = 0; i < _frost.Count; i++)
        {
            SpriteRenderer line = _frost[i];
            line.gameObject.SetActive(frost);
            line.color = new Color(1f, 1f, 1f, 0.1f);
            line.transform.localRotation = Quaternion.Euler(0f, 0f, 15f + i * 60f);
            line.transform.localScale = new Vector3(_radius * 2f * 0.96f / 1.28f, 0.03f / 1.28f, 1f);
        }
    }

    private SpriteRenderer Child(string name, Sprite sprite, int order)
    {
        GameObject go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(gfxSprite.transform, false);
        SpriteRenderer renderer = JuiceSprites.Unlight(go.GetComponent<SpriteRenderer>());
        renderer.sprite = sprite;
        renderer.sortingLayerID = gfxSprite.sortingLayerID;
        renderer.sortingOrder = order;
        return renderer;
    }

    /// <summary>
    /// The cast went out (SpellCast): the brackets stay on what they framed while the spell is in the air, and fade
    /// a beat after it lands, so the player sees exactly what it hit. The aim itself goes at once, tags with it.
    /// </summary>
    /// <param name="seconds">How long until they fade: the spell's travel time and a beat (scaled, like the flight).</param>
    public void LockTargets(float seconds)
    {
        ReleaseLocked();
        ClearTags();
        HideStrip();
        foreach (KeyValuePair<Component, SpriteRenderer> pair in _brackets) _locked[pair.Key] = pair.Value;
        _brackets.Clear();
        if (_locked.Count == 0) return;

        _lockFade = DOVirtual.DelayedCall(Mathf.Max(0f, seconds), () =>
        {
            float fade = Juice.Tokens.Durations.Select;
            foreach (SpriteRenderer bracket in _locked.Values)
                if (bracket != null) bracket.DOFade(0f, fade).SetEase(Juice.Tokens.Eases.In);
            _lockFade = DOVirtual.DelayedCall(fade, ReleaseLocked);
        }).SetLink(gameObject);
    }

    private void ReleaseLocked()
    {
        _lockFade?.Kill();
        _lockFade = null;
        foreach (SpriteRenderer bracket in _locked.Values) Release(bracket);
        _locked.Clear();
    }

    public void SetPosition(Vector2 position)
    {
        transform.SetPositionAndRotation(position, Quaternion.identity);
    }

    public void SetVisible(bool visible)
    {
        if (visible == _visible && gfxObject.activeSelf == visible) return;
        _visible = visible;
        gfxObject.SetActive(visible);

        if (!visible)
        {
            _appear?.Kill();
            ClearBrackets();
            HideStrip();
            return;
        }

        // The aim pops in under the finger rather than appearing.
        EnsureRing();
        _nextScan = 0f;
        Transform aim = gfxObject.transform;
        _appear?.Kill();
        aim.localScale = Vector3.one * 0.85f;
        _appear = aim.DOScale(1f, Juice.Tokens.Durations.Select).SetEase(Juice.Tokens.Eases.Pop).SetUpdate(true)
            .SetLink(gameObject).OnKill(() => aim.localScale = Vector3.one);
    }

    /// <summary>A spell that buffs towers brackets the towers it would reach; every other spell, the enemies.</summary>
    public void SetTargetsTowers(bool towers)
    {
        if (_targetsTowers == towers) return;
        _targetsTowers = towers;
        ClearBrackets();
    }

    public void SetSprite(Sprite sprite)
    {
        gfxSprite.sprite = sprite;
        gfxSprite.color = fillColor;
        if (_disc != null) _disc.color = Color.clear;
    }

    public void SetScale(float diameter)
    {
        _radius = diameter * 0.5f;
        gfxSprite.size = new Vector2(diameter, diameter);

        EnsureRing();
        _ring.transform.localScale = Vector3.one * (_radius / RingSpriteRadius);
    }

    private void EnsureRing()
    {
        if (_ring != null) return;

        GameObject go = new GameObject("AimRing", typeof(SpriteRenderer));
        go.transform.SetParent(gfxSprite.transform, false);
        _ring = JuiceSprites.Unlight(go.GetComponent<SpriteRenderer>());
        _ring.sprite = JuiceSprites.DashedRing;
        _ring.color = ringColor;
        _ring.sortingLayerID = gfxSprite.sortingLayerID;
        _ring.sortingOrder = gfxSprite.sortingOrder + 3;
    }

    private void Update()
    {
        if (!_visible) return;

        if (_ring != null && !Juice.ReduceMotion)
            _ring.transform.Rotate(0f, 0f, -ringSpinDegreesPerSecond * Time.unscaledDeltaTime);

        if (Time.unscaledTime >= _nextScan)
        {
            _nextScan = Time.unscaledTime + targetScanSeconds;
            if (_strip) ScanStrip();
            else ScanTargets();
        }
    }

    private void LateUpdate()
    {
        // Brackets and tags ride their targets: between scans while aiming, and through the flight once locked.
        if (_visible)
        {
            Follow(_brackets);
            FollowTags();
            if (_lock != null && _lock.gameObject.activeSelf && _anchor != null) _lock.transform.position = _anchor.transform.position;
        }
        Follow(_locked);
    }

    private static void Follow(Dictionary<Component, SpriteRenderer> brackets)
    {
        foreach (KeyValuePair<Component, SpriteRenderer> pair in brackets)
        {
            if (pair.Value == null) continue;
            if (pair.Key == null) pair.Value.gameObject.SetActive(false);
            else pair.Value.transform.position = pair.Key.transform.position;
        }
    }

    private readonly List<Rect> _placedTags = new();

    /// <summary>
    /// Each tag rides above its target; one that would land on a tag already placed this frame is lifted clear of it
    /// instead, so a tight column of enemies reads as a stack of tags rather than one smear. Lowest targets first:
    /// their tags stay nearest them and the ones behind climb.
    /// </summary>
    private void FollowTags()
    {
        _placedTags.Clear();
        _tagOrder.Clear();
        foreach (KeyValuePair<Component, AimTag> pair in _tags)
        {
            if (pair.Value == null || pair.Value.Root == null) continue;
            if (pair.Key == null) { pair.Value.Root.gameObject.SetActive(false); continue; }
            _tagOrder.Add(pair);
        }

        _tagOrder.Sort((a, b) =>
        {
            int byHeight = a.Key.transform.position.y.CompareTo(b.Key.transform.position.y);
            return byHeight != 0 ? byHeight : a.Key.GetInstanceID().CompareTo(b.Key.GetInstanceID());
        });

        foreach (KeyValuePair<Component, AimTag> pair in _tagOrder)
        {
            AimTag tag = pair.Value;
            Vector2 size = new Vector2(tag.Back.size.x, tag.Back.size.y) * tag.Back.transform.localScale.x;
            Vector3 at = pair.Key.transform.position + Vector3.up * tag.Lift;
            Rect rect = new Rect(at.x - size.x * 0.5f, at.y - size.y * 0.5f, size.x, size.y);

            for (int guard = 0; guard < 8; guard++)
            {
                bool clear = true;
                foreach (Rect placed in _placedTags)
                {
                    if (!placed.Overlaps(rect)) continue;
                    rect.y = placed.yMax + size.y * 0.12f;
                    clear = false;
                }
                if (clear) break;
            }

            _placedTags.Add(rect);
            tag.Root.position = new Vector3(rect.center.x, rect.center.y, at.z);
        }
    }

    private readonly List<KeyValuePair<Component, AimTag>> _tagOrder = new();

    /// <summary>What is inside the circle now: new ones are bracketed and tagged with a tick, leavers lose theirs.</summary>
    private void ScanTargets()
    {
        Vector2 centre = transform.position;
        float reach = _radius * _radius;
        bool gained = false;

        _scratch.Clear();
        IEnumerable<Component> candidates = _targetsTowers
            ? ClientTowerRegistry.ActiveTowers
            : FindObjectsByType<EnemyManager>(FindObjectsSortMode.None);

        foreach (Component target in candidates)
        {
            if (target == null || !target.gameObject.activeInHierarchy) continue;
            if (((Vector2)target.transform.position - centre).sqrMagnitude > reach) continue;
            if (!Touches(target)) continue;

            _scratch.Add(target);
            if (_brackets.ContainsKey(target))
            {
                RefreshTag(target);
                continue;
            }

            _brackets[target] = ShowBracket(target);
            ShowTag(target);
            gained = true;
        }

        DropLeavers();

        if (!gained) return;
        Juice.Sfx(SfxId.Tick);
        Juice.Haptic(HapticRole.Select);
    }

    /// <summary>
    /// Lance: the enemy nearest the finger (LanceExecutor's anchor) gets a lock, the strip is laid along its lane for
    /// the band the strike will run, and everything inside the band is bracketed. Nothing near: the aim greys out and
    /// says so, rather than promising a hit that will not happen.
    /// </summary>
    private void ScanStrip()
    {
        float halfWidth = _card != null && _card.SpellData != null ? _card.SpellData.Range * _scale.Range : 0.45f;
        List<EnemyManager> enemies = new(FindObjectsByType<EnemyManager>(FindObjectsSortMode.None));
        _anchor = SpellLane.FindAnchor(transform.position, _fieldTeam, halfWidth * 1.5f, enemies);

        _scratch.Clear();
        bool gained = false;

        if (_anchor == null || !SpellLane.TryGetPath(_anchor, out WaypointPath path))
        {
            _anchor = null;
            HideStrip();
            _ring.color = WithAlpha(Juice.Tokens.Colors.InkMuted, 0.9f);
            ShowNoTarget();
            DropLeavers();
            return;
        }

        if (_noTarget != null) _noTarget.Root.gameObject.SetActive(false);
        _ring.color = WithAlpha(_hue, ringColor.a);

        float halfBand = halfWidth * 3f / path.TotalLength;
        float center = _anchor.ServerMovement.PathProgress.Value;
        float physical = SpellLane.PhysicalProgress(_anchor);
        SpellLane.Sample(path, physical - halfBand, physical + halfBand, _anchor.Team.GetTeamType(), _band);
        DrawStrip();

        foreach (EnemyManager enemy in enemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled || enemy.Team == null || enemy.PathAssignment == null) continue;
            if (enemy.Team.GetTeamType() != _fieldTeam) continue;
            if (enemy.PathAssignment.TargetMap != _anchor.PathAssignment.TargetMap) continue;
            if (Mathf.Abs(enemy.ServerMovement.PathProgress.Value - center) > halfBand) continue;

            _scratch.Add(enemy);
            if (_brackets.ContainsKey(enemy)) { RefreshTag(enemy); continue; }

            _brackets[enemy] = ShowBracket(enemy);
            ShowTag(enemy);
            gained = true;
        }

        DropLeavers();
        ShowLock();

        if (!gained) return;
        Juice.Sfx(SfxId.Tick);
        Juice.Haptic(HapticRole.Select);
    }

    private void DropLeavers()
    {
        List<Component> gone = null;
        foreach (Component bracketed in _brackets.Keys)
            if (bracketed == null || !_scratch.Contains(bracketed)) (gone ??= new List<Component>()).Add(bracketed);

        if (gone == null) return;
        foreach (Component target in gone)
        {
            Release(_brackets[target]);
            _brackets.Remove(target);
            ReleaseTag(target);
        }
    }

    /// <summary>Whether this spell actually does something to <paramref name="target"/>.</summary>
    private bool Touches(Component target)
    {
        if (target is not EnemyManager enemy) return true;
        EnemyDataSO data = enemy.Data;
        return _targets switch
        {
            AimTargets.ArmouredEnemies => data != null && data.ArmorColor != ArmorColor.None,
            AimTargets.SlowableEnemies => data == null || !data.ImmuneToSlow,
            _ => true
        };
    }

    // ---------- tags ----------

    /// <summary>What a tag says and how it is dressed: the board's pill, its outline, an icon in front of the words.</summary>
    private struct TagSpec
    {
        public string Text;
        public Color TextColor;
        public Color Outline;
        public Sprite Icon;
        public Color IconColor;
        public float IconEm;
        public bool IsValid => !string.IsNullOrEmpty(Text);
    }

    /// <summary>
    /// One pooled tag: a pill in the sheet colour with a hairline outline, an optional icon and the words, sized in
    /// ems of the tag font so the whole thing scales from one number (the board's 11px text in a 19px pill).
    /// </summary>
    private sealed class AimTag
    {
        public Transform Root;
        public SpriteRenderer Outline, Back, Icon;
        public TextMeshPro Label;
        public string Text;
        public float Lift;
    }

    private const float PillEm = 1.76f, PadEm = 0.64f, OutlineEm = 0.14f, GapEm = 0.36f;

    /// <summary>How far above a target's centre its tag sits: clear of its sprite and its health bar.</summary>
    private float LiftFor(Component target)
    {
        // Off the shape it draws, not its sprite's rect: the tower art does not agree on padding (VisibleBounds).
        float half = Mathf.Min(VisibleBounds.Across(target, 0.6f), 1.2f) * 0.5f;
        return half + SpellFxSettingsSO.Instance.Aim.TagLift;
    }

    private void ShowTag(Component target)
    {
        TagSpec spec = TagFor(target);
        if (!spec.IsValid) return;

        AimTag tag = TakeTag();
        Dress(tag, spec);
        tag.Lift = LiftFor(target);
        tag.Root.position = target.transform.position + Vector3.up * tag.Lift;
        tag.Root.gameObject.SetActive(true);
        Pop(tag, 0.6f);
        _tags[target] = tag;
    }

    private static void Pop(AimTag tag, float from)
    {
        Transform tr = tag.Root;
        tr.DOKill();
        tr.localScale = Vector3.one * from;
        tr.DOScale(1f, Juice.Tokens.Durations.Select).SetEase(Juice.Tokens.Eases.Pop).SetUpdate(true).SetLink(tr.gameObject);
    }

    /// <summary>A KO can appear mid-aim as towers chip at a target, so the tag is re-read on every scan.</summary>
    private void RefreshTag(Component target)
    {
        if (!_tags.TryGetValue(target, out AimTag tag) || tag == null || tag.Root == null) return;
        TagSpec spec = TagFor(target);
        if (!spec.IsValid || tag.Text == spec.Text) return;
        Dress(tag, spec);
        tag.Root.DOKill();
        tag.Root.localScale = Vector3.one;
        tag.Root.DOPunchScale(Vector3.one * 0.25f, Juice.Tokens.Durations.Select, 4, 0.6f).SetUpdate(true).SetLink(tag.Root.gameObject);
    }

    /// <summary>
    /// What the spell will do to this target, in words a glance can read: a damage spell says KO when the hit is
    /// lethal and the damage otherwise; every other spell says its effect at the caster's level.
    /// </summary>
    private TagSpec TagFor(Component target)
    {
        SpellFxSettingsSO fx = SpellFxSettingsSO.Instance;
        JuiceTokensSO t = Juice.Tokens;
        TagSpec spec = new TagSpec { TextColor = fx.SoftHueFor(_spell), Outline = _hue, IconColor = _hue, IconEm = 1f };
        SpellDataSO data = _card != null ? _card.SpellData : null;

        switch (data)
        {
            case SpellOffensiveDataSO offensive when target is EnemyManager enemy:
            {
                float damage = offensive.Damage * _scale.Damage;
                float health = enemy.ServerHealth != null ? enemy.ServerHealth.CurrentHealth.Value : float.MaxValue;
                if (health <= damage)
                {
                    spec.Text = "KO";
                    spec.TextColor = spec.Outline = t.Colors.DamageHeavy;
                    return spec;
                }
                spec.Text = "-" + Mathf.RoundToInt(damage);
                spec.TextColor = t.Colors.Ink;
                spec.Outline = new Color(1f, 1f, 1f, 0.5f);
                return spec;
            }
            case SpellBuffDataSO buff:
                spec.Text = "+" + Pct(buff.AttackSpeedBonus * _scale.EffectBonus) + " speed";
                return spec;
            case SpellRageDataSO rage:
                spec.Text = "+" + Pct(rage.MoveSpeedBonus * _scale.EffectBonus) + " speed";
                return spec;
            case SpellSlowDataSO slow:
                spec.Text = "-" + Pct(Mathf.Clamp01(slow.SlowPercent * _scale.EffectBonus)) + " speed";
                return spec;
            case SpellResistDataSO resist:
                spec.Text = "Armour -" + Pct(Mathf.Clamp01(resist.ResistClearPercent * _scale.EffectBonus));
                spec.TextColor = _hue;
                spec.Icon = fx.Sprites.ShieldBreak;
                return spec;
            case SpellEffectDataSO effect when _spell == SpellType.Ice:
                spec.Text = "Freeze " + (effect.Duration * _scale.Duration).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " s";
                spec.Icon = fx.Sprites.Snowflake;
                return spec;
            default:
                return spec;
        }
    }

    private static string Pct(float fraction) => Mathf.RoundToInt(fraction * 100f) + "%";

    /// <summary>Lays a tag out around its words: icon, gap, text, centred in a pill that hugs them.</summary>
    private void Dress(AimTag tag, TagSpec spec)
    {
        float em = SpellFxSettingsSO.Instance.Aim.TagSize * 0.1f;
        tag.Text = spec.Text;
        tag.Label.text = spec.Text;
        tag.Label.color = spec.TextColor;

        float textWidth = tag.Label.GetPreferredValues(spec.Text).x;
        float iconWidth = spec.Icon != null ? spec.IconEm * em : 0f;
        float gap = spec.Icon != null ? GapEm * em : 0f;
        float content = iconWidth + gap + textWidth;
        float height = PillEm * em;
        float width = Mathf.Max(height, content + PadEm * em * 2f);

        SizePill(tag.Back, width, height);
        tag.Back.color = Juice.Tokens.Colors.Sheet;
        bool outlined = spec.Outline.a > 0.01f;
        tag.Outline.gameObject.SetActive(outlined);
        if (outlined)
        {
            SizePill(tag.Outline, width + OutlineEm * em * 2f, height + OutlineEm * em * 2f);
            tag.Outline.color = spec.Outline;
        }

        tag.Icon.gameObject.SetActive(spec.Icon != null);
        if (spec.Icon != null)
        {
            tag.Icon.sprite = spec.Icon;
            tag.Icon.color = spec.IconColor;
            Vector3 extent = spec.Icon.bounds.size;
            tag.Icon.transform.localScale = Vector3.one * (iconWidth / Mathf.Max(0.0001f, Mathf.Max(extent.x, extent.y)));
            tag.Icon.transform.localPosition = new Vector3(-content * 0.5f + iconWidth * 0.5f, 0f, 0f);
        }

        tag.Label.rectTransform.sizeDelta = new Vector2(textWidth + em, height);
        tag.Label.rectTransform.localPosition = new Vector3(-content * 0.5f + iconWidth + gap + textWidth * 0.5f, 0f, 0f);
    }

    /// <summary>
    /// A sliced SpriteRenderer does not shrink its round ends to fit, so the pill is drawn exactly ends-high and
    /// scaled down to the height asked for (the same trick as the tower's upgrade chip).
    /// </summary>
    private static void SizePill(SpriteRenderer pill, float width, float height)
    {
        const float native = 0.66f;
        pill.size = new Vector2(width * native / height, native);
        pill.transform.localScale = Vector3.one * (height / native);
    }

    private AimTag TakeTag()
    {
        AimTag tag = _freeTags.Count > 0 ? _freeTags.Pop() : null;
        if (tag != null && tag.Root != null) return tag;

        SpellFxSettingsSO fx = SpellFxSettingsSO.Instance;
        int order = gfxSprite.sortingOrder + 6;
        tag = new AimTag { Root = new GameObject("AimTag").transform };
        tag.Root.SetParent(transform, false);

        tag.Outline = TagPill("Outline", tag.Root, order);
        tag.Back = TagPill("Back", tag.Root, order + 1);

        GameObject icon = new GameObject("Icon", typeof(SpriteRenderer));
        icon.transform.SetParent(tag.Root, false);
        tag.Icon = JuiceSprites.Unlight(icon.GetComponent<SpriteRenderer>());
        tag.Icon.sortingLayerID = gfxSprite.sortingLayerID;
        tag.Icon.sortingOrder = order + 2;

        GameObject label = new GameObject("Label", typeof(RectTransform));
        label.transform.SetParent(tag.Root, false);
        tag.Label = label.AddComponent<TextMeshPro>();
        TMP_FontAsset font = fx.Aim.TagFont != null ? fx.Aim.TagFont : TMP_Settings.defaultFontAsset;
        if (font != null) tag.Label.font = font;
        tag.Label.fontSize = fx.Aim.TagSize;
        tag.Label.fontStyle = FontStyles.Normal;
        tag.Label.alignment = TextAlignmentOptions.Center;
        tag.Label.textWrappingMode = TextWrappingModes.NoWrap;
        // The board's tags are plain bold words on the dark pill, without the game text's black outline.
        tag.Label.outlineWidth = 0f;
        tag.Label.sortingLayerID = gfxSprite.sortingLayerID;
        tag.Label.sortingOrder = order + 2;
        return tag;
    }

    private SpriteRenderer TagPill(string name, Transform parent, int order)
    {
        GameObject go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(parent, false);
        SpriteRenderer pill = JuiceSprites.Unlight(go.GetComponent<SpriteRenderer>());
        pill.sprite = JuiceSprites.Pill;
        pill.drawMode = SpriteDrawMode.Sliced;
        pill.sortingLayerID = gfxSprite.sortingLayerID;
        pill.sortingOrder = order;
        return pill;
    }

    private void ReleaseTag(Component target)
    {
        if (!_tags.TryGetValue(target, out AimTag tag)) return;
        _tags.Remove(target);
        Free(tag);
    }

    private void Free(AimTag tag)
    {
        if (tag == null || tag.Root == null) return;
        tag.Root.DOKill();
        tag.Root.gameObject.SetActive(false);
        _freeTags.Push(tag);
    }

    private void ClearTags()
    {
        foreach (AimTag tag in _tags.Values) Free(tag);
        _tags.Clear();
    }

    // ---------- Lance's strip ----------

    private void DrawStrip()
    {
        if (_stripBand == null)
        {
            // The board's strip: a band 0.88 of the lane's width at 26% under a 2.5px spine at 95%. The match's
            // corridors are about 0.7 units wide, so the band is 0.62: any wider and it spills onto the ground at
            // every bend.
            _stripBand = StripLine("LanceBand", 0.62f, gfxSprite.sortingOrder + 1);
            _stripSpine = StripLine("LanceSpine", 0.045f, gfxSprite.sortingOrder + 2);
        }

        Vector3[] points = _band.ToArray();
        _stripBand.positionCount = _stripSpine.positionCount = points.Length;
        _stripBand.SetPositions(points);
        _stripSpine.SetPositions(points);
        _stripBand.startColor = _stripBand.endColor = WithAlpha(_hue, 0.26f);
        _stripSpine.startColor = _stripSpine.endColor = WithAlpha(_hue, 0.95f);
        _stripBand.gameObject.SetActive(true);
        _stripSpine.gameObject.SetActive(true);
    }

    private LineRenderer StripLine(string name, float width, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = JuiceSprites.Unlit != null ? JuiceSprites.Unlit : gfxSprite.sharedMaterial;
        line.useWorldSpace = true;
        line.widthMultiplier = width;
        line.numCapVertices = 6;
        line.numCornerVertices = 4;
        line.sortingLayerID = gfxSprite.sortingLayerID;
        line.sortingOrder = order;
        return line;
    }

    private void ShowLock()
    {
        if (_anchor == null) return;
        if (_lock == null)
        {
            // The board's lock: a rounded square on its corner (a diamond), 30px across with a 2.5px line.
            GameObject go = new GameObject("LanceLock");
            go.transform.SetParent(transform, false);
            _lock = JuiceSprites.Unlight(go.AddComponent<SpriteRenderer>());
            _lock.sprite = JuiceSprites.DashedRoundRect(120, 120, 22, 100000, 0, 6f);
            _lock.sortingLayerID = gfxSprite.sortingLayerID;
            _lock.sortingOrder = gfxSprite.sortingOrder + 5;
        }

        bool fresh = !_lock.gameObject.activeSelf;
        _lock.color = _hue;
        _lock.transform.SetPositionAndRotation(_anchor.transform.position, Quaternion.Euler(0f, 0f, 45f));
        _lock.gameObject.SetActive(true);
        if (!fresh) return;

        // As big as what it locks (the board's lock is the size of the enemy inside it), within sane bounds.
        float across = VisibleBounds.Across(_anchor, 0.33f) * 1.35f;
        float rest = Mathf.Clamp(across, 0.36f, 0.6f) / 1.2f;
        Transform tr = _lock.transform;
        tr.DOKill();
        tr.localScale = Vector3.one * rest * 1.8f;
        tr.DOScale(rest, Juice.Tokens.Durations.Select).SetEase(Juice.Tokens.Eases.Pop).SetUpdate(true).SetLink(_lock.gameObject);
        Juice.Sfx(SfxId.Tick, 2);
    }

    /// <summary>Off the lane or with nothing under the finger: said plainly, with the deny dot, over the grey ring.</summary>
    private void ShowNoTarget()
    {
        if (_lock != null) _lock.gameObject.SetActive(false);
        bool fresh = _noTarget == null || !_noTarget.Root.gameObject.activeSelf;
        if (_noTarget == null)
        {
            _noTarget = TakeTag();
            Dress(_noTarget, new TagSpec
            {
                Text = "No target · needs an enemy under it",
                TextColor = Juice.Tokens.Colors.Ink,
                Outline = Color.clear,
                Icon = JuiceSprites.Disc,
                IconColor = Juice.Tokens.Colors.Deny,
                IconEm = 0.73f
            });
        }

        _noTarget.Root.position = KeepOnScreen(transform.position + Vector3.up * (_radius + 0.25f), _noTarget);
        _noTarget.Root.gameObject.SetActive(true);
        if (fresh) Pop(_noTarget, 0.7f);
    }

    /// <summary>A long tag near the edge of the field is slid back inside the screen rather than cut off.</summary>
    private static Vector3 KeepOnScreen(Vector3 at, AimTag tag)
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return at;
        float half = tag.Back.size.x * tag.Back.transform.localScale.x * 0.5f + 0.1f;
        float reach = cam.orthographicSize * cam.aspect - half;
        if (reach <= 0f) return at;
        at.x = Mathf.Clamp(at.x, cam.transform.position.x - reach, cam.transform.position.x + reach);
        return at;
    }

    private void HideStrip()
    {
        if (_stripBand != null) _stripBand.gameObject.SetActive(false);
        if (_stripSpine != null) _stripSpine.gameObject.SetActive(false);
        if (_lock != null) _lock.gameObject.SetActive(false);
        if (_noTarget != null) _noTarget.Root.gameObject.SetActive(false);
        _anchor = null;
    }

    // ---------- brackets ----------

    private SpriteRenderer ShowBracket(Component target)
    {
        SpriteRenderer bracket = _freeBrackets.Count > 0 ? _freeBrackets.Pop() : null;
        if (bracket == null)
        {
            GameObject go = new GameObject("TargetBracket", typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            bracket = JuiceSprites.Unlight(go.GetComponent<SpriteRenderer>());
            bracket.sprite = JuiceSprites.Bracket;
            bracket.sortingLayerID = gfxSprite.sortingLayerID;
            bracket.sortingOrder = gfxSprite.sortingOrder + 4;
        }

        // Sized off the shape the target draws (never its sprite's padded rect), so a tank, a runner and a tower
        // with a roomy canvas are each framed snugly.
        float size = VisibleBounds.Across(target, 0.6f);
        float scale = Mathf.Min(size, 1.2f) * bracketScale / 1.28f;

        Transform tr = bracket.transform;
        tr.position = target.transform.position;
        bracket.gameObject.SetActive(true);
        bracket.color = Juice.Tokens.Colors.Attention;

        tr.DOKill();
        tr.localScale = Vector3.one * scale * 1.4f;
        tr.DOScale(scale, Juice.Tokens.Durations.Select).SetEase(Juice.Tokens.Eases.Pop).SetUpdate(true).SetLink(bracket.gameObject);
        return bracket;
    }

    private void Release(SpriteRenderer bracket)
    {
        if (bracket == null) return;
        bracket.transform.DOKill();
        bracket.gameObject.SetActive(false);
        _freeBrackets.Push(bracket);
    }

    private void ClearBrackets()
    {
        foreach (SpriteRenderer bracket in _brackets.Values) Release(bracket);
        _brackets.Clear();
        ClearTags();
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
