using System.Collections;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Ferrugem (Spell Juice board "Ferrugem"): a glob of acid splats into a bubbling puddle that squashes and settles,
/// droplets fly and it hisses. Its whole value is invisible in health (it changes numbers, not hit points), so the
/// break itself is made loud on every armoured enemy that touches it (<see cref="UnitStatusView"/>: a crack, a split
/// shield in that armour's colour, rust falling off), and when the clock runs out the puddle dries away.
/// </summary>
public class FerrugemVisual : SpellVisual
{
    private Transform _pool;
    private Vector3 _poolRest;
    private float _landedAt = -1f;

    protected override void OnPlay()
    {
        float travel = Mathf.Max(0.05f, Cast.TravelTime);
        Vector3 from = Cast.Position + new Vector3(0.6f, 2f, 0f);
        SpriteRenderer glob = Sprite("Glob", JuiceSprites.Glow, Hue, 0.42f, false, 34);
        glob.transform.position = from;
        glob.transform.DOMove(Cast.Position, travel).SetEase(T.Eases.Fall).SetLink(gameObject).OnComplete(() =>
        {
            Destroy(glob.gameObject);
            Land();
        });
        Sfx(SfxId.Whoosh, 0, 0.4f);
    }

    private void Land()
    {
        float radius = Mathf.Max(0.3f, Cast.Range);
        float duration = Mathf.Max(0.8f, Cast.Duration);
        Vector3 at = Cast.Position;
        _landedAt = Time.time;

        _pool = new GameObject("Puddle").transform;
        _pool.SetParent(transform, false);
        Sprite("Pool", SpellFxSettingsSO.Or(Fx.Sprites.Corrosion, JuiceSprites.Disc), WithAlpha(Hue, 0.55f), radius * 2.2f, true, -13, _pool);
        Sprite("Rust", JuiceSprites.Glow, WithAlpha(Fx.Hues.Rust, 0.3f), radius * 1.2f, true, -12, _pool);
        _pool.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        // The splat: squashed wide, then up, then settling (ease-settle on the squash only).
        _poolRest = Vector3.one;
        _pool.localScale = new Vector3(1.4f, 0.55f, 1f);
        DOTween.Sequence().SetLink(gameObject)
            .Append(_pool.DOScale(new Vector3(0.94f, 1.06f, 1f), 0.12f).SetEase(T.Eases.Out))
            .Append(_pool.DOScale(Vector3.one, T.Durations.Settle).SetEase(T.Eases.Settle, T.Eases.SettleAmplitude, T.Eases.SettlePeriod));

        UnityEngine.UI.Image clock = BuildClock(radius, -10);
        clock.DOFillAmount(0f, duration).SetEase(Ease.Linear).SetLink(gameObject);

        WorldFx.Ring(at, Hue, radius * 0.2f, radius * 1.1f, 0.4f);
        WorldFx.Debris(at, radius * 0.8f, Hue, 8);
        for (int i = 0; i < 2; i++)
        {
            Vector3 p = at + (Vector3)(Random.insideUnitCircle * radius * 0.4f);
            WorldFx.Puff(Fx.Sprites.Smoke, p, new Color(0.59f, 0.67f, 0.35f, 0.45f), 0.8f, Vector3.up * 0.4f, 1.1f);
        }

        Sfx(SfxId.SpellHiss);
        Buzz(HapticRole.Light);
        Shake(T.Shake.Micro, T.Durations.Shake);

        float clear = Cast.Data is SpellResistDataSO data ? Mathf.Clamp01(data.ResistClearPercent * Cast.Scale.EffectBonus) : 0f;
        RegisterZone(0f, clear);

        StartCoroutine(Bubbles(duration));
        StartCoroutine(Dry(duration));
    }

    private void Update()
    {
        if (_pool == null || _landedAt < 0f || Time.time - _landedAt < 0.6f || Juice.ReduceMotion) return;
        float w = Mathf.Sin(Time.time * 3f) * 0.025f;
        _pool.localScale = new Vector3(_poolRest.x * (1f + w), _poolRest.y * (1f - w), 1f);
    }

    private IEnumerator Bubbles(float duration)
    {
        float end = _landedAt + duration;
        Color bubble = Color.Lerp(Hue, Color.white, 0.3f);
        while (Time.time < end)
        {
            yield return new WaitForSeconds(Juice.ReduceMotion ? 0.6f : 0.22f);
            Vector3 p = Cast.Position + (Vector3)(Random.insideUnitCircle * Cast.Range * 0.75f);
            WorldFx.Ring(JuiceSprites.Outline, p, bubble, 0.03f, Random.Range(0.08f, 0.12f), 0.6f);
        }
    }

    /// <summary>The clock runs out and the puddle dries: it shrinks and fades with a last, quieter hiss.</summary>
    private IEnumerator Dry(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (_pool == null) yield break;

        _landedAt = -1f;    // the wobble lets go of the scale; the drying owns it now
        _pool.DOScale(Vector3.one * 0.55f, 0.5f).SetEase(T.Eases.In).SetLink(gameObject);
        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>())
            renderer.DOFade(0f, 0.5f).SetEase(T.Eases.In).SetLink(gameObject);

        UnityEngine.UI.Image clock = GetComponentInChildren<UnityEngine.UI.Image>();
        if (clock != null) clock.DOFade(0f, 0.25f).SetLink(gameObject);

        Sfx(SfxId.SpellHiss, 0, 0.45f);
        Destroy(gameObject, 0.6f);
    }
}
