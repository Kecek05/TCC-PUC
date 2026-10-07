using System.Collections;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Fireball (Spell Juice board "Fireball"): a meteor thrown in from above and to the side, growing as it falls,
/// shedding embers and smoke, its shadow darkening and a ring closing on the spot so the landing is predictable to
/// the frame; then the slammed door: flash, two shockwaves, fire licking outward, debris and smoke, and a scorch that
/// stays long enough to see where it hit. Two kills or more stamp ×N over the crater.
/// </summary>
public class FireballVisual : SpellVisual
{
    private const float SpawnHeight = 5f;
    private const float EntrySide = 0.45f;
    private const float EntryScale = 0.6f;
    private const float MeteorLength = 1.75f;

    /// <summary>Where the meteor's head sits along its sprite, from the bottom (the art's white-hot rock).</summary>
    private const float HeadFromBottom = 0.12f;

    private Transform _meteor;
    private SpriteRenderer _reticle;
    private SpriteRenderer _shadow;

    protected override void OnPlay()
    {
        float travel = Mathf.Max(0.05f, Cast.TravelTime);
        float radius = Mathf.Max(0.3f, Cast.Range);
        Vector3 at = Cast.Position;
        Vector3 from = at + new Vector3(SpawnHeight * EntrySide, SpawnHeight, 0f);

        // The shadow on the ground grows and darkens as the meteor comes down (ease-fall over the flight).
        _shadow = Sprite("Shadow", JuiceSprites.Disc, new Color(0f, 0f, 0f, 0f), radius * 1.6f, true, -10);
        Transform shadow = _shadow.transform;
        Vector3 shadowFull = new Vector3(shadow.localScale.x, shadow.localScale.y * 0.42f, 1f);
        shadow.localScale = shadowFull * 0.3f;
        shadow.DOScale(shadowFull, travel).SetEase(T.Eases.Fall).SetLink(gameObject);
        _shadow.DOFade(0.38f, travel).SetEase(T.Eases.Fall).SetLink(gameObject);

        // The ring that closes: 1.35x the radius down to the radius, landing exactly when the meteor does.
        _reticle = Sprite("Reticle", JuiceSprites.Outline, WithAlpha(Hue, 0f), radius * 2f * 1.35f / 0.95f, false, 20);
        Transform reticle = _reticle.transform;
        Vector3 reticleRest = reticle.localScale / 1.35f;
        DOTween.Sequence().SetLink(gameObject)
            .Join(_reticle.DOFade(0.95f, T.Durations.Select))
            .Join(reticle.DOScale(reticleRest, travel).SetEase(Ease.Linear))
            .Join(reticle.DORotate(new Vector3(0f, 0f, -90f), travel).SetEase(Ease.Linear));

        // The meteor: head down, tail up, turned to its flight.
        _meteor = new GameObject("Meteor").transform;
        _meteor.SetParent(transform, false);
        _meteor.position = from;
        Vector3 travelDir = (at - from).normalized;
        _meteor.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-travelDir.y, -travelDir.x) * Mathf.Rad2Deg - 90f);

        SpriteRenderer body = Sprite("Body", SpellFxSettingsSO.Or(Fx.Sprites.Meteor, JuiceSprites.Glow), Hue, MeteorLength, false, 34, _meteor);
        body.transform.localPosition = Vector3.up * MeteorLength * (0.5f - HeadFromBottom);
        Sprite("Core", JuiceSprites.Glow, WithAlpha(Fx.Hues.FireCore, 0.95f), 0.62f, false, 35, _meteor);

        _meteor.localScale = Vector3.one * EntryScale;
        _meteor.DOScale(1f, travel).SetEase(T.Eases.Fall).SetLink(gameObject);
        _meteor.DOMove(at, travel).SetEase(T.Eases.Fall).SetLink(gameObject).OnComplete(Impact);

        StartCoroutine(Trail(travel));

        Sfx(SfxId.SpellIgnite);
        Sfx(SfxId.Whoosh, 0, 0.8f);
    }

    /// <summary>Embers and smoke shed along the fall, thickest as it comes in.</summary>
    private IEnumerator Trail(float travel)
    {
        float end = Time.time + travel;
        float nextSmoke = 0f;
        Color[] embers = { Fx.Hues.Fire, Fx.Hues.Ember, Juice.Tokens.Colors.Sparkle };
        int i = 0;

        while (Time.time < end && _meteor != null)
        {
            Vector3 p = _meteor.position + (Vector3)Random.insideUnitCircle * 0.12f;
            WorldFx.Mote(p, embers[i++ % embers.Length], Random.Range(0.12f, 0.22f),
                new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0.15f, 0.4f), 0f), 0.5f, Fx.Sprites.Flame);

            if (Time.time >= nextSmoke)
            {
                nextSmoke = Time.time + 0.12f;
                WorldFx.Puff(Fx.Sprites.Smoke, p, WithAlpha(Fx.Hues.Smoke, 0.35f), 0.45f, Vector3.up * 0.3f, 0.8f);
            }

            yield return new WaitForSeconds(Juice.ReduceMotion ? 0.09f : 0.045f);
        }
    }

    private void Impact()
    {
        Vector3 at = Cast.Position;
        float radius = Mathf.Max(0.3f, Cast.Range);

        if (_meteor != null) Destroy(_meteor.gameObject);
        if (_reticle != null) Destroy(_reticle.gameObject);
        if (_shadow != null) Destroy(_shadow.gameObject);

        Wash(Juice.Tokens.Colors.Blast, 0.35f);
        Shake(T.Shake.Small, T.Durations.ShakeLong);
        Buzz(HapticRole.Medium);
        Sfx(SfxId.Boom);

        WorldFx.Flash(at, Color.Lerp(Fx.Hues.FireCore, Color.white, 0.4f), radius * 0.95f);
        WorldFx.Ring(at, Hue, radius * 0.2f, radius * 1.1f, 0.45f);
        DOVirtual.DelayedCall(0.08f, () => WorldFx.Ring(at, Juice.Tokens.Colors.Sparkle, radius * 0.3f, radius * 1.3f, 0.55f), false)
            .SetLink(gameObject);
        WorldFx.Burst(Fx.Sprites.Flame, at, Hue, 8, radius * 0.75f, 0.42f, true, 0.4f);
        WorldFx.Sparks(at, Juice.Tokens.Colors.Sparkle, 8, radius * 1.1f);
        WorldFx.Debris(at, radius, Juice.Tokens.Colors.Blast);

        for (int i = 0; i < (Juice.ReduceMotion ? 2 : 5); i++)
        {
            Vector3 p = at + (Vector3)(Random.insideUnitCircle * radius * 0.4f);
            WorldFx.Puff(Fx.Sprites.Smoke, p, WithAlpha(Fx.Hues.Smoke, 0.6f), Random.Range(0.75f, 1f),
                new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.5f, 0.8f), 0f), 1.3f);
        }

        // The scorch: burnt ground with dying embers, held a beat and faded over a couple of seconds.
        WorldFx.Decal(Fx.Sprites.Scorch, at, WithAlpha(Fx.Hues.Smoke, 0.85f), radius * 2.2f, 0.4f, 2.1f, Random.Range(0f, 360f));
        for (int i = 0; i < 6; i++)
        {
            Vector3 p = at + (Vector3)(Random.insideUnitCircle * radius * 0.55f);
            WorldFx.Mote(p, Fx.Hues.Ember, Random.Range(0.1f, 0.16f), Vector3.up * 0.05f, Random.Range(1.2f, 1.8f));
        }

        StartCoroutine(StampKills(EnemiesNear(at, radius * 1.1f, Cast.FieldTeam), at + Vector3.up * 0.75f));
        Destroy(gameObject, 1.5f);
    }
}
