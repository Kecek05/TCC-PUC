using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Where a dragged spell will land (SpellCast's aim): a faint disc the size of the spell's reach, ringed by a slowly
/// turning dashed outline, and an attention bracket on everything inside it the spell will touch (the enemies, or
/// for a tower buff the towers), so the player sees exactly what it does before letting go. A tick and a buzz each
/// time something new comes into the circle.
/// </summary>
/// <remarks>
/// Pure presentation. Unscaled time throughout: spells are aimed while the tutorial holds the clock. Enemies are
/// found by a short periodic scan of the scene rather than the server's <see cref="EnemyRegistry"/>, which a
/// joining client does not fill; both the aim and the enemies are in the local view, so the distances agree.
/// </remarks>
public class GhostSpellCard : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private GameObject gfxObject;
    [SerializeField] private SpriteRenderer gfxSprite;

    [Title("Aim")]
    [Tooltip("The disc under the dashed radius (the design's tint-plain at 14%).")]
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
    private Tween _lockFade;
    private readonly List<Component> _scratch = new();
    private bool _targetsTowers;
    private readonly Stack<SpriteRenderer> _freeBrackets = new();

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
    /// The cast went out (SpellCast): the brackets stay on what they framed while the spell is in the air, and fade
    /// a beat after it lands, so the player sees exactly what it hit. The aim itself goes at once.
    /// </summary>
    /// <param name="seconds">How long until they fade: the spell's travel time and a beat (scaled, like the flight).</param>
    public void LockTargets(float seconds)
    {
        ReleaseLocked();
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
            ScanTargets();
        }
    }

    private void LateUpdate()
    {
        // Brackets ride their targets: between scans while aiming, and through the flight once locked.
        if (_visible) Follow(_brackets);
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

    /// <summary>What is inside the circle now: new ones are bracketed with a tick, leavers lose theirs.</summary>
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

            _scratch.Add(target);
            if (_brackets.ContainsKey(target)) continue;

            _brackets[target] = ShowBracket(target);
            gained = true;
        }

        // Leavers: everything bracketed that was not found inside this time.
        List<Component> gone = null;
        foreach (Component bracketed in _brackets.Keys)
            if (bracketed == null || !_scratch.Contains(bracketed)) (gone ??= new List<Component>()).Add(bracketed);

        if (gone != null)
            foreach (Component target in gone)
            {
                Release(_brackets[target]);
                _brackets.Remove(target);
            }

        if (!gained) return;
        Juice.Sfx(SfxId.Tick);
        Juice.Haptic(HapticRole.Select);
    }

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
    }
}
