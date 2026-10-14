using System.Collections;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Rift (Spell Juice board "Rift"): a violet seed drops and the ground tears open across the lane with a rumble (dust,
/// rock, a shake); rings and motes drift into the fissure for its whole life so it reads as a pull, and when the
/// clock runs out it zips shut. Enemies that touch it wade on trailing echoes (<see cref="UnitStatusView"/>) until
/// it seals, as RiftExecutor keeps them slowed until then.
/// </summary>
public class RiftVisual : SpellVisual
{
    private Transform _fissure;
    private SpriteRenderer _rim, _fill;
    private float _landedAt = -1f;

    protected override void OnPlay()
    {
        float travel = Mathf.Max(0.05f, Cast.TravelTime);
        Vector3 from = Cast.Position + new Vector3(0.4f, 1.9f, 0f);
        SpriteRenderer seed = Sprite("Seed", JuiceSprites.Glow, Fx.Hues.RiftSoft, 0.45f, false, 34);
        seed.transform.position = from;
        seed.transform.DOMove(Cast.Position, travel).SetEase(T.Eases.Fall).SetLink(gameObject).OnComplete(() =>
        {
            Destroy(seed.gameObject);
            Land();
        });
        Sfx(SfxId.Whoosh, 0, 0.5f);
    }

    private void Land()
    {
        float radius = Mathf.Max(0.3f, Cast.Range);
        float duration = Mathf.Max(0.8f, Cast.Duration);
        Vector3 at = Cast.Position;
        _landedAt = Time.time;

        // The board's zone: the void at 40% in the middle, the hue gathering from 14% to 26% at the rim. No edge
        // line: the pull rings and the clock are what draw its reach.
        _fill = Sprite("Fill", JuiceSprites.AimDisc, WithAlpha(Hue, 0.26f), radius * 2f, true, -15);
        Sprite("Void", JuiceSprites.Glow, WithAlpha(Fx.Hues.RiftVoid, 0.4f), radius * 1.3f, true, -14);

        // Across the lane: the fissure lies perpendicular to the path nearest the drop.
        float angle = SpellLane.TryLaneDirection(at, Cast.FieldTeam, out Vector3 lane)
            ? Mathf.Atan2(lane.y, lane.x) * Mathf.Rad2Deg + 90f
            : 0f;

        _fissure = new GameObject("Fissure").transform;
        _fissure.SetParent(transform, false);
        _fissure.rotation = Quaternion.Euler(0f, 0f, angle);
        Sprite riftSprite = SpellFxSettingsSO.Or(Fx.Sprites.Rift, JuiceSprites.Ray);
        _rim = Sprite("Rim", riftSprite, Hue, radius * 2.1f, true, -12, _fissure);
        SpriteRenderer voidRenderer = Sprite("Void", riftSprite, Fx.Hues.RiftVoid, radius * 1.85f, true, -11, _fissure);
        voidRenderer.transform.localScale = new Vector3(voidRenderer.transform.localScale.x, voidRenderer.transform.localScale.y * 0.55f, 1f);

        // Torn open: snaps wide across, overshoots, settles.
        _fissure.localScale = new Vector3(0.1f, 1f, 1f);
        DOTween.Sequence().SetLink(gameObject)
            .Append(_fissure.DOScale(new Vector3(1.1f, 1.15f, 1f), 0.25f).SetEase(T.Eases.Snap))
            .Append(_fissure.DOScale(Vector3.one, 0.12f));

        UnityEngine.UI.Image clock = BuildClock(radius, -10);
        clock.DOFillAmount(0f, duration).SetEase(Ease.Linear).SetLink(gameObject);

        Shake(T.Shake.Small, T.Durations.ShakeLong);
        Buzz(HapticRole.Medium);
        Sfx(SfxId.SpellRumble);
        WorldFx.Ring(at, Hue, radius * 0.2f, radius * 1.15f, 0.45f);
        WorldFx.Debris(at, radius * 0.8f, new Color(0.165f, 0.133f, 0.22f));
        for (int i = 0; i < (Juice.ReduceMotion ? 1 : 3); i++)
        {
            Vector3 p = at + (Vector3)(Random.insideUnitCircle * radius * 0.5f);
            WorldFx.Puff(Fx.Sprites.Smoke, p, new Color(0.56f, 0.53f, 0.66f, 0.45f), 0.8f, Vector3.up * 0.3f, 1f);
        }

        float slow = Cast.Data is SpellSlowDataSO data ? Mathf.Clamp01(data.SlowPercent * Cast.Scale.EffectBonus) : 0f;
        RegisterZone(0f, slow);

        StartCoroutine(Pull(duration));
        StartCoroutine(Seal(duration));
    }

    private void Update()
    {
        if (_rim == null || _landedAt < 0f) return;
        float pulse = Juice.ReduceMotion ? 0.85f : 0.75f + Mathf.Sin(Time.time * 4.6f) * 0.25f;
        _rim.color = WithAlpha(_rim.color, pulse);
    }

    /// <summary>Rings contracting into the fissure and motes sucked in from its edge: a pull, not a puddle.</summary>
    private IEnumerator Pull(float duration)
    {
        float end = _landedAt + duration;
        float nextRing = 0f;
        float radius = Cast.Range;

        while (Time.time < end)
        {
            if (Time.time >= nextRing)
            {
                nextRing = Time.time + 0.45f;
                WorldFx.Ring(JuiceSprites.Outline, Cast.Position, WithAlpha(Color.Lerp(Hue, Color.white, 0.2f), 0.7f), radius * 1.05f, radius * 0.15f, 1.4f);
            }

            if (!Juice.ReduceMotion)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                Vector3 edge = Cast.Position + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius * 0.95f;
                WorldFx.Mote(edge, Fx.Hues.RiftSoft, 0.12f, (Cast.Position - edge) * 0.85f, 0.9f);
            }

            yield return new WaitForSeconds(0.2f);
        }
    }

    /// <summary>The clock runs out and the fissure zips shut from both ends with a puff.</summary>
    private IEnumerator Seal(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (_fissure == null) yield break;

        float radius = Cast.Range;
        _rim = null;    // the breathing stops; every renderer fades from where it is
        _fissure.DOScale(new Vector3(0.04f, 0.5f, 1f), 0.25f).SetEase(T.Eases.In).SetLink(gameObject);

        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>())
            if (renderer != null) renderer.DOFade(0f, 0.3f).SetLink(gameObject);

        UnityEngine.UI.Image clock = GetComponentInChildren<UnityEngine.UI.Image>();
        if (clock != null) clock.DOFade(0f, 0.2f).SetLink(gameObject);

        DOVirtual.DelayedCall(0.22f, () =>
        {
            WorldFx.Ring(Cast.Position, Fx.Hues.RiftSoft, 0.1f, radius * 0.6f, 0.3f);
            WorldFx.Puff(Fx.Sprites.Smoke, Cast.Position, new Color(0.56f, 0.53f, 0.66f, 0.4f), 0.6f, Vector3.up * 0.25f, 0.8f);
            WorldFx.Sparks(Cast.Position, Fx.Hues.RiftSoft, 4, radius * 0.6f, 0.14f);
            Sfx(SfxId.SpellSeal);
        }, false).SetLink(gameObject);

        Destroy(gameObject, 0.5f);
    }
}
