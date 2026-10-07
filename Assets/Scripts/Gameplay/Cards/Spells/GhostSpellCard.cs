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
    private readonly Dictionary<Component, TextMeshPro> _tags = new();
    private Tween _lockFade;
    private readonly List<Component> _scratch = new();
    private bool _targetsTowers;
    private readonly Stack<SpriteRenderer> _freeBrackets = new();
    private readonly Stack<TextMeshPro> _freeTags = new();

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
    private TextMeshPro _noTarget;
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

        gfxSprite.color = WithAlpha(_hue, SpellFxSettingsSO.Instance.Aim.DiscAlpha);
        EnsureRing();
        _ring.color = WithAlpha(_hue, ringColor.a);
        HideStrip();
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
        _ring = go.GetComponent<SpriteRenderer>();
        _ring.sprite = JuiceSprites.DashedRing;
        _ring.color = ringColor;
        _ring.sortingLayerID = gfxSprite.sortingLayerID;
        _ring.sortingOrder = gfxSprite.sortingOrder + 1;
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

    private void FollowTags()
    {
        float lift = SpellFxSettingsSO.Instance.Aim.TagLift;
        foreach (KeyValuePair<Component, TextMeshPro> pair in _tags)
        {
            if (pair.Value == null) continue;
            if (pair.Key == null) pair.Value.gameObject.SetActive(false);
            else pair.Value.transform.position = pair.Key.transform.position + Vector3.up * lift;
        }
    }

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

        if (_noTarget != null) _noTarget.gameObject.SetActive(false);
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

    private void ShowTag(Component target)
    {
        string text = TagFor(target, out Color color);
        if (string.IsNullOrEmpty(text)) return;

        TextMeshPro tag = TakeTag();
        tag.text = text;
        tag.color = color;
        tag.transform.position = target.transform.position + Vector3.up * SpellFxSettingsSO.Instance.Aim.TagLift;
        tag.gameObject.SetActive(true);

        Transform tr = tag.transform;
        tr.DOKill();
        tr.localScale = Vector3.one * 0.6f;
        tr.DOScale(1f, Juice.Tokens.Durations.Select).SetEase(Juice.Tokens.Eases.Pop).SetUpdate(true).SetLink(tag.gameObject);
        _tags[target] = tag;
    }

    /// <summary>A KO can appear mid-aim as towers chip at a target, so the tag is re-read on every scan.</summary>
    private void RefreshTag(Component target)
    {
        if (!_tags.TryGetValue(target, out TextMeshPro tag) || tag == null) return;
        string text = TagFor(target, out Color color);
        if (tag.text == text) return;
        tag.text = text;
        tag.color = color;
        tag.transform.DOPunchScale(Vector3.one * 0.25f, Juice.Tokens.Durations.Select, 4, 0.6f).SetUpdate(true).SetLink(tag.gameObject);
    }

    /// <summary>
    /// What the spell will do to this target, in words a glance can read: a damage spell says KO when the hit is
    /// lethal and the damage otherwise; every other spell says its effect at the caster's level.
    /// </summary>
    private string TagFor(Component target, out Color color)
    {
        SpellFxSettingsSO fx = SpellFxSettingsSO.Instance;
        JuiceTokensSO t = Juice.Tokens;
        color = fx.SoftHueFor(_spell);
        SpellDataSO data = _card != null ? _card.SpellData : null;

        switch (data)
        {
            case SpellOffensiveDataSO offensive when target is EnemyManager enemy:
            {
                float damage = offensive.Damage * _scale.Damage;
                float health = enemy.ServerHealth != null ? enemy.ServerHealth.CurrentHealth.Value : float.MaxValue;
                if (health <= damage)
                {
                    color = t.Colors.DamageHeavy;
                    return "KO";
                }
                color = t.Colors.Damage;
                return "-" + Mathf.RoundToInt(damage);
            }
            case SpellBuffDataSO buff:
                return "+" + Pct(buff.AttackSpeedBonus * _scale.EffectBonus);
            case SpellRageDataSO rage:
                return "+" + Pct(rage.MoveSpeedBonus * _scale.EffectBonus);
            case SpellSlowDataSO slow:
                return "-" + Pct(Mathf.Clamp01(slow.SlowPercent * _scale.EffectBonus));
            case SpellResistDataSO resist:
                return "-" + Pct(Mathf.Clamp01(resist.ResistClearPercent * _scale.EffectBonus)) + " armour";
            case SpellEffectDataSO effect when _spell == SpellType.Ice:
                return "Freeze " + (effect.Duration * _scale.Duration).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " s";
            default:
                return null;
        }
    }

    private static string Pct(float fraction) => Mathf.RoundToInt(fraction * 100f) + "%";

    private TextMeshPro TakeTag()
    {
        TextMeshPro tag = _freeTags.Count > 0 ? _freeTags.Pop() : null;
        if (tag != null) return tag;

        GameObject go = new GameObject("AimTag");
        go.transform.SetParent(transform, false);
        tag = go.AddComponent<TextMeshPro>();
        SpellFxSettingsSO fx = SpellFxSettingsSO.Instance;
        TMP_FontAsset font = fx.Aim.TagFont != null ? fx.Aim.TagFont : TMP_Settings.defaultFontAsset;
        if (font != null) tag.font = font;
        tag.fontSize = fx.Aim.TagSize;
        tag.alignment = TextAlignmentOptions.Center;
        tag.textWrappingMode = TextWrappingModes.NoWrap;
        tag.rectTransform.sizeDelta = new Vector2(3f, 0.6f);
        tag.sortingLayerID = gfxSprite.sortingLayerID;
        tag.sortingOrder = gfxSprite.sortingOrder + 3;
        return tag;
    }

    private void ReleaseTag(Component target)
    {
        if (!_tags.TryGetValue(target, out TextMeshPro tag)) return;
        _tags.Remove(target);
        if (tag == null) return;
        tag.transform.DOKill();
        tag.gameObject.SetActive(false);
        _freeTags.Push(tag);
    }

    private void ClearTags()
    {
        foreach (TextMeshPro tag in _tags.Values)
        {
            if (tag == null) continue;
            tag.transform.DOKill();
            tag.gameObject.SetActive(false);
            _freeTags.Push(tag);
        }
        _tags.Clear();
    }

    // ---------- Lance's strip ----------

    private void DrawStrip()
    {
        if (_stripBand == null)
        {
            _stripBand = StripLine("LanceBand", 0.78f, gfxSprite.sortingOrder);
            _stripSpine = StripLine("LanceSpine", 0.05f, gfxSprite.sortingOrder + 1);
        }

        Vector3[] points = _band.ToArray();
        _stripBand.positionCount = _stripSpine.positionCount = points.Length;
        _stripBand.SetPositions(points);
        _stripSpine.SetPositions(points);
        _stripBand.startColor = _stripBand.endColor = WithAlpha(_hue, 0.24f);
        _stripSpine.startColor = _stripSpine.endColor = WithAlpha(_hue, 0.95f);
        _stripBand.gameObject.SetActive(true);
        _stripSpine.gameObject.SetActive(true);
    }

    private LineRenderer StripLine(string name, float width, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = gfxSprite.sharedMaterial;
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
            GameObject go = new GameObject("LanceLock");
            go.transform.SetParent(transform, false);
            _lock = go.AddComponent<SpriteRenderer>();
            _lock.sprite = JuiceSprites.Bracket;
            _lock.sortingLayerID = gfxSprite.sortingLayerID;
            _lock.sortingOrder = gfxSprite.sortingOrder + 3;
        }

        bool fresh = !_lock.gameObject.activeSelf;
        _lock.color = _hue;
        _lock.transform.SetPositionAndRotation(_anchor.transform.position, Quaternion.Euler(0f, 0f, 45f));
        _lock.gameObject.SetActive(true);
        if (!fresh) return;

        Transform tr = _lock.transform;
        tr.DOKill();
        tr.localScale = Vector3.one * 0.95f;
        tr.DOScale(0.62f, Juice.Tokens.Durations.Select).SetEase(Juice.Tokens.Eases.Pop).SetUpdate(true).SetLink(_lock.gameObject);
    }

    private void ShowNoTarget()
    {
        if (_lock != null) _lock.gameObject.SetActive(false);
        if (_noTarget == null) _noTarget = TakeTag();
        _noTarget.text = "No target";
        _noTarget.color = Juice.Tokens.Colors.InkMuted;
        _noTarget.transform.position = transform.position + Vector3.up * (_radius + 0.25f);
        _noTarget.gameObject.SetActive(true);
    }

    private void HideStrip()
    {
        if (_stripBand != null) _stripBand.gameObject.SetActive(false);
        if (_stripSpine != null) _stripSpine.gameObject.SetActive(false);
        if (_lock != null) _lock.gameObject.SetActive(false);
        if (_noTarget != null) _noTarget.gameObject.SetActive(false);
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
            bracket = go.GetComponent<SpriteRenderer>();
            bracket.sprite = JuiceSprites.Bracket;
            bracket.sortingLayerID = gfxSprite.sortingLayerID;
            bracket.sortingOrder = gfxSprite.sortingOrder + 2;
        }

        // Sized off the target's own sprite, so a tank and a runner are each framed snugly.
        SpriteRenderer body = target.GetComponentInChildren<SpriteRenderer>();
        float size = body != null ? Mathf.Max(body.bounds.size.x, body.bounds.size.y) : 0.6f;
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
