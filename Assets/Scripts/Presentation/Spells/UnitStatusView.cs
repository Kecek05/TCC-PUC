using UnityEngine;

/// <summary>
/// The statuses one enemy is wearing, drawn on top of it and never on it: its body's colour is its armour, so a
/// status never repaints it (Spell Juice, "Worn, not painted"). Hasted (Rage): wings, speed streaks and an aura.
/// Slowed (Rift, Prism): two echoes trailing behind and a ripple at its feet. Armour broken (Ferrugem, Torniquete):
/// a split shield in its own armour colour, an acid glow, rust flakes falling off. Held (Ancora): a shackle of chain
/// closed round it and an anchor badge. Each arrives with a beat of its own and leaves as one.
/// </summary>
/// <remarks>
/// Pooled by <see cref="UnitStatusPresenter"/>. Animated by hand in LateUpdate (presence eased in and out,
/// flapping and pulsing from time), so a dozen dressed enemies cost a few renderers each and no tweens.
/// Drawn on the enemies' layer around the body (order 5): glows and echoes under it, the badges above its health bar.
/// <para/>
/// Everything is sized from the enemy it is on. The boards draw a troop 30px across with 19px wings and a 48px aura;
/// the match's troops are about half that size, and wings sized for the board's troop buried the real one. The
/// proportions are the design, so they scale with the body: a boss wears big wings, a swarm unit small ones.
/// </remarks>
public class UnitStatusView : MonoBehaviour
{
    private const string Layer = "Enemy";
    private const float FadeSpeed = 7f;

    /// <summary>The troop the boards' status sizes were drawn around (30px of a 56px world unit).</summary>
    private const float DesignBody = 0.536f;

    /// <summary>At most one announcing word and sound per status in this long: an aura a whole wave walks into ticks, it does not chatter.</summary>
    private const float AnnounceGap = 0.22f;

    private static float s_nextHaste, s_nextSlow, s_nextBreak, s_nextHold;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => s_nextHaste = s_nextSlow = s_nextBreak = s_nextHold = 0f;

    /// <summary>Nothing on and nothing still leaving: the view can go back to the pool.</summary>
    public bool IsIdle => !_rageOn && !_slowOn && !_brokenOn && !_heldOn
                          && _rage <= 0.001f && _slow <= 0.001f && _broken <= 0.001f && _held <= 0.001f;

    private EnemyManager _enemy;
    private SpriteRenderer _body;
    private bool _built;
    private float _k = 1f;

    private SpriteRenderer _aura, _wingL, _wingR, _streakL, _streakR;
    private SpriteRenderer _echo1, _echo2, _ripple;
    private SpriteRenderer _glow, _badgeBack, _badge, _badgeRing;
    private SpriteRenderer _chain, _holdBack, _holdRing, _holdIcon;

    private bool _rageOn, _slowOn, _brokenOn, _heldOn;
    private float _rage, _slow, _broken, _held;

    /// <summary>The haste and slow amounts last seen, so a second source stacking onto the first is a beat of
    /// its own: the number said is always the new total.</summary>
    private float _hasteAmount, _slowAmount;

    /// <summary>A stack has to move the printed percent to count: amounts arrive as whole percents.</summary>
    private const float StackStep = 0.005f;
    private Vector3 _lastPosition;
    private Vector3 _facing = Vector3.down;
    private float _nextFlake;
    private Color _armour = Color.white;

    public void Bind(EnemyManager enemy)
    {
        Build();
        _enemy = enemy;
        _body = VisibleBounds.BodyOf(enemy);
        // Floored so the smallest body (a Swarm unit, 0.6 of a standard one) still wears something legible.
        _k = Mathf.Clamp(VisibleBounds.Across(enemy, 0.3f) / DesignBody, 0.42f, 1.6f);
        _rageOn = _slowOn = _brokenOn = _heldOn = false;
        _rage = _slow = _broken = _held = 0f;
        _hasteAmount = _slowAmount = 0f;
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
        _rageOn = _slowOn = _brokenOn = _heldOn = false;
        _rage = _slow = _broken = _held = 0f;
        _hasteAmount = _slowAmount = 0f;
        ApplyPresence();
    }

    /// <summary>
    /// What this enemy is under now, each as a fraction (0 = not under it). A status switching on plays its arrival
    /// beat and says its size; switching off, its farewell. A haste or a slow that GROWS while it is already on
    /// (a second source stacking onto the first) plays the beat again and says the new total: the arrival alone
    /// used to speak, so a Prism's slow under a Rift, or a second Rage, changed the unit's speed and said nothing.
    /// </summary>
    public void SetStatuses(float hasted, float slowed, float broken, bool held)
    {
        if (_enemy == null) return;

        SpellFxSettingsSO fx = SpellFxSettingsSO.Instance;
        Vector3 at = _enemy.transform.position;
        Vector3 over = at + Vector3.up * (0.2f + 0.65f * _k);
        bool seen = CameraFeedback.IsOnScreen(at, 0.05f);
        float now = Time.unscaledTime;

        if (hasted > 0f && (!_rageOn || hasted > _hasteAmount + StackStep))
        {
            WorldFx.Ring(at, fx.Hues.Rage, 0.15f * _k, 0.78f * _k, 0.35f);
            if (seen && now >= s_nextHaste)
            {
                s_nextHaste = now + AnnounceGap;
                FloatingNumbers.Say(over, Percent(hasted, "+"), 0.9f, fx.Hues.RageSoft);
            }
        }

        if (slowed > 0f && (!_slowOn || slowed > _slowAmount + StackStep))
        {
            WorldFx.Ring(at, fx.Hues.Rift, 0.84f * _k, 0.18f * _k, 0.35f);
            if (seen && now >= s_nextSlow)
            {
                s_nextSlow = now + AnnounceGap;
                FloatingNumbers.Say(over, Percent(slowed, "-"), 0.9f, fx.Hues.RiftSoft);
            }
        }

        if (broken > 0f && !_brokenOn)
        {
            WorldFx.Ring(at, _armour, 0.18f * _k, 0.9f * _k, 0.35f);
            WorldFx.Debris(fx.Sprites.Flakes, at, 0.8f * _k, fx.Hues.Rust, 4, 0.26f * _k);
            if (seen && now >= s_nextBreak)
            {
                s_nextBreak = now + AnnounceGap;
                FloatingNumbers.Say(over + Vector3.up * 0.07f, "ARMOUR BREAK", 0.8f, fx.Hues.Acid);
                Juice.Sfx(SfxId.SpellShatter, 0, 0.7f);
            }
        }

        if (broken <= 0f && _brokenOn) WorldFx.Ring(at + Vector3.up * 0.85f * _k, Color.white, 0.09f * _k, 0.4f * _k, 0.25f);

        if (held && !_heldOn)
        {
            // The shackle snaps shut: a ring closing on it, sparks where the links meet, and the word.
            WorldFx.Ring(JuiceSprites.Outline, at, fx.Hues.Hold, 1.1f * _k, 0.5f * _k, 0.22f);
            WorldFx.Sparks(at, Color.white, 4, 0.7f * _k, 0.2f * _k);
            if (seen && now >= s_nextHold)
            {
                s_nextHold = now + AnnounceGap;
                FloatingNumbers.Say(over, "HELD", 0.85f, fx.Hues.Hold);
                Juice.Sfx(SfxId.Clink, -2, 0.8f);
            }
        }

        if (!held && _heldOn)
        {
            // Let go: the links fly apart.
            WorldFx.Ring(JuiceSprites.Outline, at, fx.Hues.Hold, 0.5f * _k, 1.0f * _k, 0.3f);
            WorldFx.Debris(at, 0.8f * _k, fx.Hues.Hold, 5);
            if (seen) Juice.Sfx(SfxId.Clink, 3, 0.5f);
        }

        _rageOn = hasted > 0f;
        _slowOn = slowed > 0f;
        _brokenOn = broken > 0f;
        _heldOn = held;

        // Followed down as well as up: a source leaving and coming back is a stack again.
        _hasteAmount = Mathf.Max(0f, hasted);
        _slowAmount = Mathf.Max(0f, slowed);
    }

    private void LateUpdate()
    {
        if (_enemy == null) return;

        float dt = Time.deltaTime;
        _rage = Mathf.MoveTowards(_rage, _rageOn ? 1f : 0f, dt * FadeSpeed);
        _slow = Mathf.MoveTowards(_slow, _slowOn ? 1f : 0f, dt * FadeSpeed);
        _broken = Mathf.MoveTowards(_broken, _brokenOn ? 1f : 0f, dt * FadeSpeed);
        _held = Mathf.MoveTowards(_held, _heldOn ? 1f : 0f, dt * FadeSpeed);

        Vector3 at = _enemy.transform.position;
        Vector3 moved = at - _lastPosition;
        if (moved.sqrMagnitude > 0.000004f) _facing = Vector3.Lerp(_facing, moved.normalized, 0.25f).normalized;
        _lastPosition = at;
        transform.position = at;

        ApplyPresence();
        bool calm = Juice.ReduceMotion;
        float time = Time.time;
        float k = _k;
        Vector3 back = -_facing;
        Vector3 side = new Vector3(-_facing.y, _facing.x, 0f);
        float facingAngle = Mathf.Atan2(_facing.y, _facing.x) * Mathf.Rad2Deg - 90f;

        if (_rage > 0f)
        {
            // The board's troop is 30px: a 48px aura under it, 19px wings at its sides, streaks behind.
            float pop = Pop(_rage);
            float pulse = calm ? 1f : 1f + Mathf.Sin(time * 9f) * 0.06f;
            _aura.transform.localScale = Vector3.one * 0.86f * k * pop * pulse / Size(_aura.sprite);

            float flap = calm ? 0f : Mathf.Sin(time * Mathf.PI * 2f * 5.5f) * 22f;
            PlaceWing(_wingR, at + side * 0.37f * k + _facing * 0.07f * k, facingAngle - 30f - flap, pop, false);
            PlaceWing(_wingL, at - side * 0.37f * k + _facing * 0.07f * k, facingAngle + 30f + flap, pop, true);

            float flicker = calm ? 0.8f : 0.55f + Mathf.PerlinNoise(time * 8f, 0.3f) * 0.45f;
            PlaceStreak(_streakL, at + back * 0.4f * k - side * 0.13f * k, back, 0.58f * k * pop, flicker * _rage);
            PlaceStreak(_streakR, at + back * 0.47f * k + side * 0.13f * k, back, 0.47f * k * pop, flicker * _rage * 0.8f);
        }

        if (_slow > 0f)
        {
            // Two echoes a third of a body apart, and a ripple spreading from its feet.
            CopyBody(_echo1, at + back * 0.18f * k, 0.42f * _slow);
            CopyBody(_echo2, at + back * 0.36f * k, 0.2f * _slow);

            float phase = calm ? 0.5f : Mathf.Repeat(time / 1.1f, 1f);
            _ripple.transform.localScale = Vector3.one * Mathf.Lerp(0.45f, 1.25f, phase) * k / Size(_ripple.sprite);
            SetAlpha(_ripple, (1f - phase) * 0.9f * _slow);
        }

        // Two badges share the space above the health bar: side by side when both are on.
        bool both = _broken > 0f && _held > 0f;
        Vector3 badgeAt = at + Vector3.up * (0.12f + 0.62f * k);

        if (_broken > 0f)
        {
            float pop = Pop(_broken);
            Vector3 badge = badgeAt + (both ? Vector3.left * 0.3f * k : Vector3.zero);
            PlaceBadge(_badgeBack, _badgeRing, _badge, badge, pop);

            float pulse = calm ? 0.8f : 0.65f + Mathf.Sin(time * 6f) * 0.25f;
            _glow.transform.localScale = Vector3.one * 1.35f * k / Size(_glow.sprite);
            SetAlpha(_glow, 0.38f * pulse * _broken);

            if (_brokenOn && time >= _nextFlake && !calm)
            {
                _nextFlake = time + Random.Range(0.45f, 0.8f);
                WorldFx.Debris(SpellFxSettingsSO.Instance.Sprites.Flakes, at, 0.4f * k, SpellFxSettingsSO.Instance.Hues.Rust, 1, 0.2f * k);
            }
        }

        if (_held > 0f)
        {
            // The shackle: closed tight round the body, straining a little, turning slowly against its struggle.
            float closing = Mathf.Lerp(1.5f, 1f, Pop(_held));
            float strain = calm ? 1f : 1f + Mathf.Sin(time * 14f) * 0.03f;
            Transform chain = _chain.transform;
            chain.localScale = Vector3.one * 1.05f * k * closing * strain / Size(_chain.sprite);
            if (!calm) chain.Rotate(0f, 0f, 40f * dt);

            Vector3 badge = badgeAt + (both ? Vector3.right * 0.3f * k : Vector3.zero);
            PlaceBadge(_holdBack, _holdRing, _holdIcon, badge, Pop(_held));
        }
    }

    private void PlaceBadge(SpriteRenderer back, SpriteRenderer ring, SpriteRenderer icon, Vector3 at, float pop)
    {
        float k = _k;
        back.transform.position = at;
        back.transform.localScale = Vector3.one * 0.5f * k * pop / Size(back.sprite);
        ring.transform.position = at;
        ring.transform.localScale = Vector3.one * 0.58f * k * pop / Size(ring.sprite);
        icon.transform.position = at;
        icon.transform.localScale = Vector3.one * 0.34f * k * pop / Size(icon.sprite);
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

        SetAlpha(_chain, _held);
        SetAlpha(_holdBack, 0.92f * _held);
        SetAlpha(_holdRing, _held);
        SetAlpha(_holdIcon, _held);
    }

    private void PlaceWing(SpriteRenderer wing, Vector3 position, float angle, float pop, bool mirrored)
    {
        Transform tr = wing.transform;
        tr.position = position;
        tr.rotation = Quaternion.Euler(0f, 0f, angle);
        wing.flipX = mirrored;
        tr.localScale = Vector3.one * 0.46f * _k * pop / Size(wing.sprite);
    }

    private void PlaceStreak(SpriteRenderer streak, Vector3 from, Vector3 back, float length, float alpha)
    {
        Transform tr = streak.transform;
        tr.position = from;
        tr.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(back.y, back.x) * Mathf.Rad2Deg - 90f);
        tr.localScale = new Vector3(0.09f * _k / 1.28f, Mathf.Max(0.001f, length) / 1.28f, 1f);
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
        _chain = Make("Chain", SpellFxSettingsSO.Or(fx.Sprites.ChainRing, JuiceSprites.DashedRing), fx.Hues.Hold, 7);
        _badgeBack = Make("BadgeBack", JuiceSprites.Disc, Juice.Tokens.Colors.Sheet, 9);
        _badgeRing = Make("BadgeRing", JuiceSprites.Ring, fx.Hues.Acid, 10);
        _badge = Make("Badge", SpellFxSettingsSO.Or(fx.Sprites.ShieldBreak, JuiceSprites.Cross), Color.white, 11);
        _holdBack = Make("HoldBack", JuiceSprites.Disc, Juice.Tokens.Colors.Sheet, 9);
        _holdRing = Make("HoldRing", JuiceSprites.Ring, fx.Hues.Hold, 10);
        _holdIcon = Make("HoldIcon", SpellFxSettingsSO.Or(fx.Sprites.Anchor, JuiceSprites.ArrowDown), fx.Hues.Hold, 11);
    }

    private SpriteRenderer Make(string name, Sprite sprite, Color color, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        SpriteRenderer renderer = JuiceSprites.Unlight(go.AddComponent<SpriteRenderer>());
        renderer.sprite = sprite;
        renderer.sortingLayerName = Layer;
        renderer.sortingOrder = order;
        color.a = 0f;
        renderer.color = color;
        return renderer;
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
