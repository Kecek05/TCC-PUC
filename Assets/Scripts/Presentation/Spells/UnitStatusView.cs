using UnityEngine;

/// <summary>
/// The statuses one enemy is wearing, drawn on top of it and never on it: its body's colour is its armour, so a
/// status never repaints it (Spell Juice, "Worn, not painted"). Raged: wings, speed streaks and an aura. Slowed:
/// two echoes trailing behind and a ripple at its feet. Armour broken: a split shield in its own armour colour, an
/// acid glow, rust flakes falling off. Each arrives with a beat of its own and leaves as one.
/// </summary>
/// <remarks>
/// Pooled by <see cref="SpellStatusPresenter"/>. Animated by hand in LateUpdate (presence eased in and out,
/// flapping and pulsing from time), so a dozen dressed enemies cost a few renderers each and no tweens.
/// Drawn on the enemies' layer around the body (order 5): glows and echoes under it, the badge above its health bar.
/// </remarks>
public class UnitStatusView : MonoBehaviour
{
    private const string Layer = "Enemy";
    private const float FadeSpeed = 7f;

    /// <summary>Set by the presenter's scan; a view nothing evaluated takes its statuses off.</summary>
    public bool Evaluated { get; set; }

    /// <summary>Nothing on and nothing still leaving: the view can go back to the pool.</summary>
    public bool IsIdle => !_rageOn && !_slowOn && !_brokenOn && _rage <= 0.001f && _slow <= 0.001f && _broken <= 0.001f;

    private EnemyManager _enemy;
    private SpriteRenderer _body;
    private bool _built;

    private SpriteRenderer _aura, _wingL, _wingR, _streakL, _streakR;
    private SpriteRenderer _echo1, _echo2, _ripple;
    private SpriteRenderer _glow, _badgeBack, _badge, _badgeRing;

    private bool _rageOn, _slowOn, _brokenOn;
    private float _rage, _slow, _broken;
    private Vector3 _lastPosition;
    private Vector3 _facing = Vector3.down;
    private float _nextFlake;
    private Color _armour = Color.white;

    public void Bind(EnemyManager enemy)
    {
        Build();
        _enemy = enemy;
        _body = FindBody(enemy);
        _rageOn = _slowOn = _brokenOn = false;
        _rage = _slow = _broken = 0f;
        _lastPosition = enemy.transform.position;
        // The armour-broken badge wears the enemy's own armour colour: it is that armour that broke.
        _armour = SpellFxSettingsSO.Instance.ArmorTint(enemy.Data != null ? enemy.Data.ArmorColor : ArmorColor.None);
        _badge.color = new Color(_armour.r, _armour.g, _armour.b, 0f);
        gameObject.SetActive(true);
        LateUpdate();
    }

    public void Unbind()
    {
        _enemy = null;
        _body = null;
        gameObject.SetActive(false);
    }

    /// <summary>The enemy is gone: everything off at once, no farewell beat on an empty spot.</summary>
    public void Clear()
    {
        _rageOn = _slowOn = _brokenOn = false;
        _rage = _slow = _broken = 0f;
        ApplyPresence();
    }

    /// <summary>Which zones dress this enemy now (null: none). A status switching on plays its arrival beat.</summary>
    public void SetStatuses(SpellZone rage, SpellZone rift, SpellZone rust)
    {
        Evaluated = true;
        if (_enemy == null) return;

        SpellFxSettingsSO fx = SpellFxSettingsSO.Instance;
        Vector3 at = _enemy.transform.position;
        bool seen = CameraFeedback.IsOnScreen(at, 0.05f);

        if (rage != null && !_rageOn)
        {
            WorldFx.Ring(at, fx.Hues.Rage, 0.08f, 0.42f, 0.35f);
            if (seen) FloatingNumbers.Say(at + Vector3.up * 0.55f, Percent(rage.Magnitude, "+"), 0.9f, fx.Hues.RageSoft);
        }

        if (rift != null && !_slowOn)
        {
            WorldFx.Ring(at, fx.Hues.Rift, 0.45f, 0.1f, 0.35f);
            if (seen) FloatingNumbers.Say(at + Vector3.up * 0.55f, Percent(rift.Magnitude, "-"), 0.9f, fx.Hues.RiftSoft);
        }

        if (rust != null && !_brokenOn)
        {
            WorldFx.Ring(at, _armour, 0.1f, 0.48f, 0.35f);
            WorldFx.Debris(fx.Sprites.Flakes, at, 0.45f, fx.Hues.Rust, 4, 0.14f);
            if (seen)
            {
                FloatingNumbers.Say(at + Vector3.up * 0.62f, "ARMOUR BREAK", 0.8f, fx.Hues.Acid);
                Juice.Sfx(SfxId.SpellShatter, 0, 0.7f);
            }
        }

        if (rust == null && _brokenOn) WorldFx.Ring(at + Vector3.up * 0.45f, Color.white, 0.05f, 0.22f, 0.25f);

        _rageOn = rage != null;
        _slowOn = rift != null;
        _brokenOn = rust != null;
    }

    private void LateUpdate()
    {
        if (_enemy == null) return;

        float dt = Time.deltaTime;
        _rage = Mathf.MoveTowards(_rage, _rageOn ? 1f : 0f, dt * FadeSpeed);
        _slow = Mathf.MoveTowards(_slow, _slowOn ? 1f : 0f, dt * FadeSpeed);
        _broken = Mathf.MoveTowards(_broken, _brokenOn ? 1f : 0f, dt * FadeSpeed);

        Vector3 at = _enemy.transform.position;
        Vector3 moved = at - _lastPosition;
        if (moved.sqrMagnitude > 0.000004f) _facing = Vector3.Lerp(_facing, moved.normalized, 0.25f).normalized;
        _lastPosition = at;
        transform.position = at;

        ApplyPresence();
        bool calm = Juice.ReduceMotion;
        float time = Time.time;
        Vector3 back = -_facing;
        Vector3 side = new Vector3(-_facing.y, _facing.x, 0f);
        float facingAngle = Mathf.Atan2(_facing.y, _facing.x) * Mathf.Rad2Deg - 90f;

        if (_rage > 0f)
        {
            float pop = Pop(_rage);
            float pulse = calm ? 1f : 1f + Mathf.Sin(time * 9f) * 0.06f;
            _aura.transform.localScale = Vector3.one * 0.62f * pop * pulse / Size(_aura.sprite);

            float flap = calm ? 0f : Mathf.Sin(time * Mathf.PI * 2f * 5.5f) * 22f;
            PlaceWing(_wingR, at + side * 0.2f + _facing * 0.04f, facingAngle - 30f - flap, pop, false);
            PlaceWing(_wingL, at - side * 0.2f + _facing * 0.04f, facingAngle + 30f + flap, pop, true);

            float flicker = calm ? 0.8f : 0.55f + Mathf.PerlinNoise(time * 8f, 0.3f) * 0.45f;
            PlaceStreak(_streakL, at + back * 0.22f - side * 0.07f, back, 0.32f * pop, flicker * _rage);
            PlaceStreak(_streakR, at + back * 0.26f + side * 0.07f, back, 0.26f * pop, flicker * _rage * 0.8f);
        }

        if (_slow > 0f)
        {
            CopyBody(_echo1, at + back * 0.17f, 0.42f * _slow);
            CopyBody(_echo2, at + back * 0.34f, 0.2f * _slow);

            float phase = calm ? 0.5f : Mathf.Repeat(time / 1.1f, 1f);
            _ripple.transform.localScale = Vector3.one * Mathf.Lerp(0.25f, 0.7f, phase) / Size(_ripple.sprite);
            SetAlpha(_ripple, (1f - phase) * 0.9f * _slow);
        }

        if (_broken > 0f)
        {
            float pop = Pop(_broken);
            Vector3 badge = at + Vector3.up * 0.46f;
            _badgeBack.transform.position = badge;
            _badgeBack.transform.localScale = Vector3.one * 0.3f * pop / Size(_badgeBack.sprite);
            _badgeRing.transform.position = badge;
            _badgeRing.transform.localScale = Vector3.one * 0.34f * pop / Size(_badgeRing.sprite);
            _badge.transform.position = badge;
            _badge.transform.localScale = Vector3.one * 0.2f * pop / Size(_badge.sprite);

            float pulse = calm ? 0.8f : 0.65f + Mathf.Sin(time * 6f) * 0.25f;
            _glow.transform.localScale = Vector3.one * 0.75f / Size(_glow.sprite);
            SetAlpha(_glow, 0.38f * pulse * _broken);

            if (_brokenOn && time >= _nextFlake && !calm)
            {
                _nextFlake = time + Random.Range(0.45f, 0.8f);
                WorldFx.Debris(SpellFxSettingsSO.Instance.Sprites.Flakes, at, 0.22f, SpellFxSettingsSO.Instance.Hues.Rust, 1, 0.11f);
            }
        }
    }

    private void ApplyPresence()
    {
        if (!_built) return;
        SetAlpha(_aura, 0.85f * _rage);
        SetAlpha(_wingL, _rage);
        SetAlpha(_wingR, _rage);
        if (_rage <= 0f) { SetAlpha(_streakL, 0f); SetAlpha(_streakR, 0f); }

        if (_slow <= 0f) { SetAlpha(_echo1, 0f); SetAlpha(_echo2, 0f); SetAlpha(_ripple, 0f); }

        SetAlpha(_badgeBack, 0.92f * _broken);
        SetAlpha(_badgeRing, _broken);
        SetAlpha(_badge, _broken);
        if (_broken <= 0f) SetAlpha(_glow, 0f);
    }

    private void PlaceWing(SpriteRenderer wing, Vector3 position, float angle, float pop, bool mirrored)
    {
        Transform tr = wing.transform;
        tr.position = position;
        tr.rotation = Quaternion.Euler(0f, 0f, angle);
        wing.flipX = mirrored;
        tr.localScale = Vector3.one * 0.34f * pop / Size(wing.sprite);
    }

    private static void PlaceStreak(SpriteRenderer streak, Vector3 from, Vector3 back, float length, float alpha)
    {
        Transform tr = streak.transform;
        tr.position = from;
        tr.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(back.y, back.x) * Mathf.Rad2Deg - 90f);
        tr.localScale = new Vector3(0.05f / 1.28f, Mathf.Max(0.001f, length) / 1.28f, 1f);
        SetAlpha(streak, alpha);
    }

    private void CopyBody(SpriteRenderer echo, Vector3 position, float alpha)
    {
        if (_body == null) { SetAlpha(echo, 0f); return; }
        echo.sprite = _body.sprite;
        Transform tr = echo.transform;
        tr.position = position;
        tr.rotation = _body.transform.rotation;
        tr.localScale = _body.transform.lossyScale;
        SetAlpha(echo, alpha);
    }

    private void Build()
    {
        if (_built) return;
        _built = true;

        SpellFxSettingsSO fx = SpellFxSettingsSO.Instance;
        _glow = Make("Glow", JuiceSprites.Glow, fx.Hues.Acid, 2);
        _aura = Make("Aura", JuiceSprites.Ring, fx.Hues.Rage, 3);
        _ripple = Make("Ripple", JuiceSprites.Ring, fx.Hues.Rift, 3);
        _echo1 = Make("Echo1", null, fx.Hues.RiftSoft, 4);
        _echo2 = Make("Echo2", null, fx.Hues.RiftSoft, 4);
        _wingL = Make("WingL", SpellFxSettingsSO.Or(fx.Sprites.Wing, JuiceSprites.Star), fx.Hues.RageSoft, 4);
        _wingR = Make("WingR", SpellFxSettingsSO.Or(fx.Sprites.Wing, JuiceSprites.Star), fx.Hues.RageSoft, 4);
        _streakL = Make("StreakL", JuiceSprites.Ray, fx.Hues.RageSoft, 4);
        _streakR = Make("StreakR", JuiceSprites.Ray, fx.Hues.RageSoft, 4);
        _badgeBack = Make("BadgeBack", JuiceSprites.Disc, Juice.Tokens.Colors.Sheet, 9);
        _badgeRing = Make("BadgeRing", JuiceSprites.Ring, fx.Hues.Acid, 10);
        _badge = Make("Badge", SpellFxSettingsSO.Or(fx.Sprites.ShieldBreak, JuiceSprites.Cross), Color.white, 11);
    }

    private SpriteRenderer Make(string name, Sprite sprite, Color color, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = Layer;
        renderer.sortingOrder = order;
        color.a = 0f;
        renderer.color = color;
        return renderer;
    }

    private static SpriteRenderer FindBody(EnemyManager enemy)
    {
        SpriteRenderer fallback = null;
        foreach (SpriteRenderer renderer in enemy.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer.name.Contains("Health")) continue;
            if (renderer.name == "EnemyGFX") return renderer;
            fallback ??= renderer;
        }
        return fallback;
    }

    private static string Percent(float fraction, string sign) =>
        sign + Mathf.RoundToInt(Mathf.Abs(fraction) * 100f) + "%";

    /// <summary>A presence of 0..1 read as a pop: it overshoots a little on its way in (ease-pop's shape).</summary>
    private static float Pop(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float x = t - 1f;
        return Mathf.Max(0f, 1f + c3 * x * x * x + c1 * x * x);
    }

    private static float Size(Sprite sprite)
    {
        if (sprite == null) return 1f;
        Vector3 extent = sprite.bounds.size;
        return Mathf.Max(0.0001f, Mathf.Max(extent.x, extent.y));
    }

    private static void SetAlpha(SpriteRenderer renderer, float alpha)
    {
        if (renderer == null) return;
        Color c = renderer.color;
        c.a = Mathf.Clamp01(alpha);
        renderer.color = c;
    }
}
