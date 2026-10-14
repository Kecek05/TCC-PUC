using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The statuses a tower wears (Spell Juice, "Worn, not painted"). Frozen: an ice block over it with a frost clock and
/// a snowflake badge; the ice cracks twice before the thaw and then shatters. Hasted: a spinning halo with two orbiting
/// sparks, and every shot leaves with a gold spark. The tower keeps its own colour through both, because its colour is
/// its attack colour: this replaces the flat #0000FF / #FFEB04 repaint.
/// </summary>
/// <remarks>
/// Added by <see cref="ClientTowerGFX"/> on its own object, so the overlays ride the spawn drop and every level-up
/// squash with the art. Both states arrive replicated (BaseServerTowerCombat.IsFrozen / IsHasted); the frost clock
/// and the moment to crack come from the Ice cast's zone (<see cref="SpellZoneRegistry"/>), which knows the scaled
/// duration the replicated bool does not. The first state replayed at spawn is set silently: the tower is not even
/// on screen yet.
/// </remarks>
public class TowerStatusView : MonoBehaviour
{
    private SpriteRenderer _art;
    private float _size = 0.9f;
    private bool _primed;

    /// <summary>The board's proportions around a 54px tower: an 80px block, a 78px frost clock, a 22px badge.</summary>
    private const float BlockSize = 1.48f, ClockSize = 1.44f, BadgeSize = 0.41f, BadgeOffset = 0.555f;

    private SpriteRenderer _block, _crackA, _crackB, _badgeBack, _badgeRing, _badge, _chill, _flash;
    private Tween _punch;
    private Image _clock;
    private bool _frozen;
    private Coroutine _thaw;

    private SpriteRenderer _haloGlow, _haloRing, _orbA, _orbB;
    private bool _hasted;
    private float _halo;

    public void Initialize(SpriteRenderer art)
    {
        _art = art;
    }

    // ---------- frozen ----------

    public void SetFrozen(bool frozen)
    {
        if (frozen == _frozen && _primed) return;
        bool silent = !_primed || !isActiveAndEnabled;
        _primed = true;
        _frozen = frozen;
        Build();

        if (frozen) Seal(silent);
        else Shatter(silent);
    }

    private void Seal(bool silent)
    {
        SpellFxSettingsSO fx = SpellFxSettingsSO.Instance;
        Vector3 at = transform.position;

        _block.gameObject.SetActive(true);
        _badgeBack.gameObject.SetActive(true);
        _chill.gameObject.SetActive(true);

        // The badge pops on a beat after the block, the way the board staggers them.
        Transform badge = _badgeBack.transform;
        Vector3 badgeRest = Vector3.one * (_size * BadgeSize / Size(_badgeBack.sprite));
        badge.DOKill();
        badge.localScale = silent ? badgeRest : Vector3.zero;
        if (!silent) badge.DOScale(badgeRest, Juice.Tokens.Durations.Pop).SetDelay(0.1f).SetEase(Juice.Tokens.Eases.Pop).SetLink(gameObject);
        _crackA.gameObject.SetActive(false);
        _crackB.gameObject.SetActive(false);

        Transform block = _block.transform;
        Vector3 rest = Vector3.one * (_size * BlockSize / Size(_block.sprite));
        block.DOKill();
        _block.DOKill();
        _block.color = WithAlpha(_block.color, 0.95f);

        if (silent)
        {
            block.localScale = rest;
        }
        else
        {
            block.localScale = rest * 1.35f;
            block.DOScale(rest, Juice.Tokens.Durations.Pop).SetEase(Juice.Tokens.Eases.Pop).SetLink(gameObject);
            WorldFx.Ring(at, fx.Hues.IceSoft, _size * 0.2f, _size * 0.8f, 0.3f);
            WorldFx.Sparks(at, Color.white, 4, _size * 0.6f, 0.13f);
            if (CameraFeedback.IsOnScreen(at, 0.05f)) Juice.Sfx(SfxId.Clink, 0, 0.6f);
        }

        // The frost clock runs to the thaw the Ice cast published; without one the block simply waits.
        if (_thaw != null) StopCoroutine(_thaw);
        _clock.gameObject.SetActive(false);
        if (SpellZoneRegistry.TryFindCovering(SpellType.Ice, at, out SpellZone zone))
        {
            float left = Mathf.Max(0.1f, zone.EndTime - Time.time);
            _clock.gameObject.SetActive(true);
            _clock.DOKill();
            _clock.fillAmount = 1f;
            _clock.color = WithAlpha(fx.Hues.IceSoft, 0.95f);
            _clock.DOFillAmount(0f, left).SetEase(Ease.Linear).SetLink(gameObject);
            _thaw = StartCoroutine(CrackBeforeThaw(left));
        }
    }

    /// <summary>Near the end the ice cracks twice, a punch each, so the thaw is seen coming.</summary>
    private IEnumerator CrackBeforeThaw(float left)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, left - 0.36f));
        Crack(_crackA);
        yield return new WaitForSeconds(0.18f);
        Crack(_crackB);
    }

    private void Crack(SpriteRenderer crack)
    {
        if (!_frozen || crack == null) return;
        crack.gameObject.SetActive(true);
        crack.color = WithAlpha(Color.white, 0.95f);
        _block.transform.DOPunchScale(Vector3.one * 0.06f * _block.transform.localScale.x, 0.14f, 6, 0.6f).SetLink(gameObject);
        if (CameraFeedback.IsOnScreen(transform.position, 0.05f)) Juice.Sfx(SfxId.Clink, 2, 0.5f);
    }

    private void Shatter(bool silent)
    {
        if (_thaw != null) { StopCoroutine(_thaw); _thaw = null; }
        _clock.DOKill();
        _clock.gameObject.SetActive(false);
        _crackA.gameObject.SetActive(false);
        _crackB.gameObject.SetActive(false);
        _badgeBack.transform.DOKill();
        _badgeBack.gameObject.SetActive(false);
        _chill.gameObject.SetActive(false);

        bool wasShown = _block.gameObject.activeSelf;
        _block.transform.DOKill();
        _block.gameObject.SetActive(false);
        if (silent || !wasShown) return;

        SpellFxSettingsSO fx = SpellFxSettingsSO.Instance;
        Vector3 at = transform.position;
        WorldFx.Debris(new[] { fx.Sprites.IceShard }, at, _size * 0.75f, fx.Hues.IceSoft, 7, _size * 0.24f);
        WorldFx.Ring(at, fx.Hues.IceSoft, _size * 0.15f, _size * 0.75f, 0.3f);
        WorldFx.Sparks(at, Color.white, 5, _size * 0.7f, 0.12f);
        if (CameraFeedback.IsOnScreen(at, 0.05f)) Juice.Sfx(SfxId.SpellShatter);

        // Free again: the tower punches back to size (punch-soft) as the shards fly.
        Punch(0.14f);
    }

    /// <summary>
    /// A punch of the whole tower that always lands back at its own size, even cut short: the drop, a level-up or
    /// another punch may take the transform over mid-way.
    /// </summary>
    public void Punch(float amount)
    {
        if (Juice.ReduceMotion) amount *= 0.5f;
        Transform tr = transform;
        if (DOTween.IsTweening(tr)) return;

        _punch?.Kill();
        _punch = tr.DOPunchScale(Vector3.one * amount, 0.28f, 5, 0.6f).SetLink(gameObject)
            .OnKill(() => { if (tr != null) tr.localScale = Vector3.one; });
    }

    /// <summary>
    /// Haste's zap arriving: the tower flashes pale gold in its own shape and punches (the board's 1.16), so each
    /// tower visibly takes the charge in turn.
    /// </summary>
    public void Zapped()
    {
        Build();
        if (_art != null && _art.sprite != null)
        {
            _flash.sprite = _art.sprite;
            _flash.transform.localPosition = _art.transform.localPosition;
            _flash.transform.localRotation = _art.transform.localRotation;
            _flash.transform.localScale = _art.transform.localScale;
            _flash.gameObject.SetActive(true);
            _flash.DOKill();
            _flash.color = new Color(1f, 0.965f, 0.76f, 0.95f);
            _flash.DOFade(0f, 0.12f).SetLink(gameObject).OnComplete(() => _flash.gameObject.SetActive(false));
        }
        Punch(0.16f);
    }

    // ---------- hasted ----------

    public void SetHasted(bool hasted)
    {
        if (hasted == _hasted && _primed) return;
        bool silent = !_primed || !isActiveAndEnabled;
        _primed = true;
        _hasted = hasted;
        Build();

        if (silent) { _halo = hasted ? 1f : 0f; return; }

        Vector3 at = transform.position;
        Color hasteColor = SpellFxSettingsSO.Instance.Hues.Haste;
        if (hasted) WorldFx.Ring(at, hasteColor, _size * 0.15f, _size * 0.7f, 0.3f);
        else WorldFx.Ring(at, SpellFxSettingsSO.Instance.Hues.HasteSoft, _size * 0.55f, _size * 0.8f, 0.25f);
    }

    /// <summary>A shot leaves a hasted tower with a gold spark: the faster fire is seen, not just counted.</summary>
    public void OnShot()
    {
        if (!_hasted || Juice.ReduceMotion) return;
        WorldFx.Sparks(transform.position, Juice.Tokens.Colors.Hasted, 2, _size * 0.35f, 0.1f);
    }

    private void LateUpdate()
    {
        if (_haloRing == null) return;

        float target = _hasted ? 1f : 0f;
        _halo = Mathf.MoveTowards(_halo, target, Time.deltaTime * 6f);
        bool on = _halo > 0.001f;
        _haloGlow.gameObject.SetActive(on);
        _haloRing.gameObject.SetActive(on);
        _orbA.gameObject.SetActive(on);
        _orbB.gameObject.SetActive(on);
        if (!on) return;

        float pop = Pop(_halo);
        float time = Time.time;
        bool calm = Juice.ReduceMotion;

        _haloGlow.transform.localScale = Vector3.one * (_size * 1.5f * pop / Size(_haloGlow.sprite));
        _haloGlow.color = WithAlpha(_haloGlow.color, 0.45f * _halo);

        Transform ring = _haloRing.transform;
        ring.localScale = Vector3.one * (_size * 1.45f * pop / Size(_haloRing.sprite));
        if (!calm) ring.Rotate(0f, 0f, -360f * Time.deltaTime);
        _haloRing.color = WithAlpha(_haloRing.color, _halo);

        float orbit = calm ? 0f : time * Mathf.PI * 2.5f;
        float r = _size * 0.68f * pop;
        _orbA.transform.position = transform.position + new Vector3(Mathf.Cos(orbit), Mathf.Sin(orbit), 0f) * r;
        _orbB.transform.position = transform.position - new Vector3(Mathf.Cos(orbit), Mathf.Sin(orbit), 0f) * r;
        _orbA.color = WithAlpha(_orbA.color, _halo);
        _orbB.color = WithAlpha(_orbB.color, _halo);
    }

    // ---------- building ----------

    private void Build()
    {
        if (_block != null) return;

        SpellFxSettingsSO fx = SpellFxSettingsSO.Instance;
        // Measured in this object's own space (the art is its child), not from world bounds: the first state arrives
        // at spawn, mid-drop, when this object is still stretched by the drop's start pose.
        // And from the shape the sprite draws, not its rect (VisibleBounds): the tower sprites differ in canvas, and
        // the rect blew some overlays up to twice the tower.
        if (_art != null && _art.sprite != null)
        {
            Vector2 extent = Vector2.Scale(VisibleBounds.SpriteSize(_art.sprite), _art.transform.localScale);
            float measured = Mathf.Max(extent.x, extent.y);
            if (measured > 0.05f && measured < 10f) _size = measured;
        }

        int layer = _art != null ? _art.sortingLayerID : SortingLayer.NameToID("Tower");
        int baseOrder = _art != null ? _art.sortingOrder : 0;

        _haloGlow = Make("HaloGlow", JuiceSprites.Glow, WithAlpha(fx.Hues.Haste, 0f), layer, baseOrder - 3);
        _haloRing = Make("HaloRing", JuiceSprites.DashedRing, fx.Hues.Haste, layer, baseOrder - 2);
        _orbA = Make("Orb", JuiceSprites.Glow, fx.Hues.HasteSoft, layer, baseOrder + 6);
        _orbB = Make("Orb", JuiceSprites.Glow, fx.Hues.HasteSoft, layer, baseOrder + 6);
        _orbA.transform.localScale = _orbB.transform.localScale = Vector3.one * (0.16f / Size(JuiceSprites.Glow));

        _block = Make("IceBlock", SpellFxSettingsSO.Or(fx.Sprites.IceBlock, JuiceSprites.Panel), new Color(0.84f, 0.965f, 1f, 0.96f), layer, baseOrder + 8);
        _crackA = Make("CrackA", SpellFxSettingsSO.Or(fx.Sprites.Crack, JuiceSprites.Ray), Color.white, layer, baseOrder + 9);
        _crackB = Make("CrackB", SpellFxSettingsSO.Or(fx.Sprites.Crack, JuiceSprites.Ray), Color.white, layer, baseOrder + 9);
        _crackA.transform.localScale = _crackB.transform.localScale = Vector3.one * (_size * 0.85f / Size(_crackA.sprite));
        _crackA.transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
        _crackB.transform.localRotation = Quaternion.Euler(0f, 0f, -55f);
        _crackB.transform.localPosition = new Vector3(_size * 0.12f, -_size * 0.06f, 0f);

        // The badge: the sheet's dark disc inside a 2px rim in the ice hue, on the block's top-right corner.
        _badgeBack = Make("FrozenBadge", JuiceSprites.Disc, Juice.Tokens.Colors.Sheet, layer, baseOrder + 11);
        _badgeBack.transform.localPosition = new Vector3(_size * BadgeOffset, _size * BadgeOffset, 0f);
        _badgeBack.transform.localScale = Vector3.one * (_size * BadgeSize / Size(_badgeBack.sprite));
        _badgeRing = Make("Rim", JuiceSprites.Disc, fx.Hues.Ice, layer, baseOrder + 10);
        _badgeRing.transform.SetParent(_badgeBack.transform, false);
        _badgeRing.transform.localScale = Vector3.one * 1.18f;

        // The chill under the block (the hue at 22%) and the pale flash a zap leaves on the tower's own shape.
        _chill = Make("Chill", JuiceSprites.Panel, WithAlpha(fx.Hues.Ice, 0.22f), layer, baseOrder + 7);
        _chill.drawMode = SpriteDrawMode.Sliced;
        _chill.size = Vector2.one * 1.28f;
        _chill.transform.localScale = Vector3.one * (_size * 1.22f / 1.28f);
        _chill.gameObject.SetActive(false);
        _flash = Make("ZapFlash", _art != null ? _art.sprite : JuiceSprites.Disc, Color.clear, layer, baseOrder + 5);
        _flash.gameObject.SetActive(false);
        _badge = Make("Snowflake", SpellFxSettingsSO.Or(fx.Sprites.Snowflake, JuiceSprites.Star), fx.Hues.IceSoft, layer, baseOrder + 12);
        _badge.transform.SetParent(_badgeBack.transform, false);
        _badge.transform.localScale = Vector3.one * (Size(_badgeBack.sprite) * 0.66f / Size(_badge.sprite));

        // Under the block: the board's clock is a hair smaller than the ice, so it shows as a rim running round it.
        _clock = BuildClock(layer, baseOrder + 6);

        _block.gameObject.SetActive(false);
        _crackA.gameObject.SetActive(false);
        _crackB.gameObject.SetActive(false);
        _badgeBack.gameObject.SetActive(false);
        _clock.gameObject.SetActive(false);
        _haloGlow.gameObject.SetActive(false);
        _haloRing.gameObject.SetActive(false);
        _orbA.gameObject.SetActive(false);
        _orbB.gameObject.SetActive(false);
    }

    private SpriteRenderer Make(string name, Sprite sprite, Color color, int layer, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        SpriteRenderer renderer = JuiceSprites.Unlight(go.AddComponent<SpriteRenderer>());
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingLayerID = layer;
        renderer.sortingOrder = order;
        return renderer;
    }

    /// <summary>A ring just outside the block, radially emptying toward the thaw (see <see cref="SpellVisual.BuildClock"/>).</summary>
    private Image BuildClock(int layer, int order)
    {
        GameObject go = new GameObject("FrostClock", typeof(RectTransform), typeof(Canvas), typeof(Image));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);

        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingLayerID = layer;
        canvas.sortingOrder = order;

        float diameter = _size * ClockSize / 0.95f;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(diameter, diameter);
        rect.localPosition = Vector3.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        Image image = go.GetComponent<Image>();
        image.sprite = JuiceSprites.Outline;
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Radial360;
        image.fillOrigin = (int)Image.Origin360.Top;
        image.fillClockwise = true;
        image.raycastTarget = false;
        return image;
    }

    private static float Size(Sprite sprite)
    {
        if (sprite == null) return 1f;
        Vector3 extent = sprite.bounds.size;
        return Mathf.Max(0.0001f, Mathf.Max(extent.x, extent.y));
    }

    private static float Pop(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float x = t - 1f;
        return Mathf.Max(0f, 1f + c3 * x * x * x + c1 * x * x);
    }

    private static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }
}
