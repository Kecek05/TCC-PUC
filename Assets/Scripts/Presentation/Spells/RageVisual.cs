using System.Collections;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Rage (Spell Juice board "Rage"): a gust bursts out of the drop (a ring, speed streaks flung outward, a puff of
/// feathers) and a fuchsia zone blooms with wind turning in it and feathers drifting up. Troops inside sprout wings
/// and lunge ahead (<see cref="UnitStatusView"/>); when the clock runs out the zone draws in a breath and exhales.
/// </summary>
/// <remarks>
/// The zone is published with its 1 s linger (<see cref="SpellRageDataSO.LingerDuration"/>), and the visual stays
/// alive until that has run out, since the statuses read the zone for exactly that long.
/// </remarks>
public class RageVisual : SpellVisual
{
    private Transform _zone;
    private Transform _swirl;
    private float _landedAt = -1f;

    protected override void OnPlay()
    {
        DOVirtual.DelayedCall(Mathf.Max(0f, Cast.TravelTime), Land, false).SetLink(gameObject);
    }

    private void Land()
    {
        float radius = Mathf.Max(0.3f, Cast.Range);
        float duration = Mathf.Max(0.8f, Cast.Duration);
        float linger = Cast.Data is SpellRageDataSO rage ? rage.LingerDuration : 1f;
        float bonus = Cast.Data is SpellRageDataSO r ? r.MoveSpeedBonus * Cast.Scale.EffectBonus : 0f;
        Vector3 at = Cast.Position;
        _landedAt = Time.time;

        _zone = new GameObject("Zone").transform;
        _zone.SetParent(transform, false);
        Sprite("Fill", JuiceSprites.Glow, WithAlpha(Hue, 0.36f), radius * 2.3f, true, -14, _zone);
        Sprite("Rim", JuiceSprites.Outline, WithAlpha(Hue, 0.75f), radius * 2f / 0.95f, true, -13, _zone);
        _swirl = Sprite("Wind", JuiceSprites.DashedRing, WithAlpha(Color.white, 0.3f), radius * 1.7f / 0.95f, true, -13, _zone).transform;

        Vector3 rest = _zone.localScale;
        _zone.localScale = rest * 0.4f;
        _zone.DOScale(rest, T.Durations.Pop).SetEase(T.Eases.Pop).SetLink(gameObject);

        UnityEngine.UI.Image clock = BuildClock(radius, -11);
        clock.DOFillAmount(0f, duration).SetEase(Ease.Linear).SetLink(gameObject);

        // The gust: streaks flung outward, a ring, feathers.
        int streaks = Juice.ReduceMotion ? 5 : 10;
        for (int i = 0; i < streaks; i++)
        {
            float a = (i + Random.Range(-0.2f, 0.2f)) * Mathf.PI * 2f / streaks;
            Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            WorldFx.Streak(at + dir * radius * 0.25f, at + dir * radius * 1.25f, WithAlpha(Color.white, 0.85f), 0.1f, 0.3f);
        }
        WorldFx.Ring(at, Hue, radius * 0.2f, radius * 1.15f, 0.45f);
        WorldFx.Burst(Fx.Sprites.Wing, at, Fx.Hues.RageSoft, 6, radius, 0.26f, false, 0.55f);

        Sfx(SfxId.SpellGust);
        Sfx(SfxId.Whoosh, 0, 0.6f);
        Buzz(HapticRole.Light);

        RegisterZone(linger, bonus);
        StartCoroutine(Feathers(duration));
        StartCoroutine(Exhale(duration, linger));
    }

    private void Update()
    {
        if (_swirl != null && !Juice.ReduceMotion) _swirl.Rotate(0f, 0f, 150f * Time.deltaTime);
    }

    private IEnumerator Feathers(float duration)
    {
        float end = _landedAt + duration;
        while (Time.time < end)
        {
            yield return new WaitForSeconds(Juice.ReduceMotion ? 0.8f : 0.35f);
            Vector3 p = Cast.Position + (Vector3)(Random.insideUnitCircle * Cast.Range * 0.8f);
            WorldFx.Mote(p, WithAlpha(Fx.Hues.RageSoft, 0.9f), 0.2f,
                new Vector3(Random.Range(-0.15f, 0.15f), 0.45f, 0f), 1.2f, Fx.Sprites.Wing);
        }
    }

    /// <summary>The end: a breath drawn in (ease-anticipate), then let out in a ring and a last puff of feathers.</summary>
    private IEnumerator Exhale(float duration, float linger)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, duration - 0.25f));
        if (_zone == null) yield break;

        Vector3 rest = _zone.localScale;
        float radius = Cast.Range;
        DOTween.Sequence().SetLink(gameObject)
            .Append(_zone.DOScale(rest * 0.86f, 0.25f).SetEase(T.Eases.Anticipate))
            .AppendCallback(() =>
            {
                WorldFx.Ring(Cast.Position, Fx.Hues.RageSoft, radius * 0.5f, radius * 1.3f, 0.4f);
                WorldFx.Burst(Fx.Sprites.Wing, Cast.Position, Fx.Hues.RageSoft, 6, radius * 1.1f, 0.24f, false, 0.5f);
                Sfx(SfxId.Whoosh, 0, 0.5f);
            })
            .Append(_zone.DOScale(rest * 1.14f, 0.3f).SetEase(T.Eases.Out))
            .Join(DOVirtual.Float(1f, 0f, 0.3f, a => FadeZone(a)));

        // The statuses read the zone through its linger; it has to outlive it.
        Destroy(gameObject, 0.3f + linger + 0.3f);
    }

    private void FadeZone(float alpha)
    {
        if (_zone == null) return;
        foreach (SpriteRenderer renderer in _zone.GetComponentsInChildren<SpriteRenderer>())
        {
            Color c = renderer.color;
            c.a = Mathf.Min(c.a, alpha * 0.8f);
            renderer.color = c;
        }

        UnityEngine.UI.Image clock = GetComponentInChildren<UnityEngine.UI.Image>();
        if (clock != null) clock.color = WithAlpha(clock.color, Mathf.Min(clock.color.a, alpha));
    }
}
