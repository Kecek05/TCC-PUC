using System.Collections;
using System.Collections.Generic;
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
    private readonly SpriteRenderer[] _reticleTicks = new SpriteRenderer[2];
    private SpriteRenderer _shadow;

    protected override void OnPlay()
    {
        float travel = Mathf.Max(0.05f, Cast.TravelTime);
        float radius = Mathf.Max(0.3f, Cast.Range);
        Vector3 at = Cast.Position;
        Vector3 from = at + new Vector3(SpawnHeight * EntrySide, SpawnHeight, 0f);

        // The shadow on the ground grows and darkens as the meteor comes down (ease-fall over the flight).
        // The board's 110 by 40 px under a 134px reach, a hair below the centre.
        _shadow = Sprite("Shadow", JuiceSprites.Glow, new Color(0f, 0f, 0f, 0f), radius * 1.64f * 1.3f, true, -10);
        Transform shadow = _shadow.transform;
        shadow.localPosition = Vector3.down * radius * 0.06f;
        Vector3 shadowFull = new Vector3(shadow.localScale.x, shadow.localScale.y * 0.36f, 1f);
        shadow.localScale = shadowFull * 0.3f;
        shadow.DOScale(shadowFull, travel).SetEase(T.Eases.Fall).SetLink(gameObject);
        _shadow.DOFade(0.6f, travel).SetEase(T.Eases.Fall).SetLink(gameObject);

        // The ring that closes: 1.35x the radius down to the radius, landing exactly when the meteor does.
        _reticle = Sprite("Reticle", JuiceSprites.Outline, WithAlpha(Hue, 0f), radius * 2f * 1.35f / 0.95f, false, 20);
        Transform reticle = _reticle.transform;
        Vector3 reticleRest = reticle.localScale / 1.35f;

        // Two tick marks across the ring (the board's 2.5 by 14 px), turning with it: a sight closing on the spot.
        for (int i = 0; i < 2; i++)
        {
            SpriteRenderer tick = Sprite("Tick", JuiceSprites.Disc, Hue, 1f, false, 20, reticle);
            float ring = 1.28f * 0.95f * (i == 0 ? 1f : -1f);
            tick.transform.localPosition = new Vector3(0f, ring, 0f);
            tick.transform.localScale = new Vector3(0.045f / 1.28f / reticleRest.x, 0.25f / 1.28f / reticleRest.x, 1f);
            _reticleTicks[i] = tick;
            tick.color = WithAlpha(Hue, 0f);
            tick.DOFade(0.95f, T.Durations.Select).SetLink(gameObject);
        }
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

        // The slammed door, at the board's sizes for a 134px reach: a 160px white-hot core, a thick shockwave in the
        // hue out to 172px and a hairline one in sparkle to 206px a beat later, eight fire blobs thrown 51px, eight
        // sparks to 72px and ten bits of debris.
        WorldFx.Flash(at, Color.Lerp(Fx.Hues.FireCore, Color.white, 0.4f), radius * 0.95f);
        WorldFx.Ring(at, Hue, radius * 0.26f, radius * 1.28f, 0.45f);
        DOVirtual.DelayedCall(0.08f, () => WorldFx.Ring(JuiceSprites.Hairline, at, Juice.Tokens.Colors.Sparkle, radius * 0.46f, radius * 1.53f, 0.45f), false)
            .SetLink(gameObject);
        WorldFx.Burst(JuiceSprites.Glow, at, Color.Lerp(Hue, Fx.Hues.FireCore, 0.25f), 8, radius * 0.76f, radius * 0.5f, false, 0.48f);
        WorldFx.Sparks(at, Juice.Tokens.Colors.Sparkle, 8, radius * 1.07f, 0.25f);
        WorldFx.Debris(at, radius * 1.15f, Juice.Tokens.Colors.Blast, 10);

        for (int i = 0; i < (Juice.ReduceMotion ? 2 : 5); i++)
        {
            Vector3 p = at + (Vector3)(Random.insideUnitCircle * radius * 0.4f);
            WorldFx.Puff(Fx.Sprites.Smoke, p, WithAlpha(Fx.Hues.Smoke, 0.6f), Random.Range(0.75f, 1f),
                new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.5f, 0.8f), 0f), 1.3f);
        }

        // The scorch: burnt ground with dying embers, held a beat and faded over a couple of seconds.
        // The board's 176 by 72 px burnt mark (wider than the reach, lying flat), near-black, held and gone by 2.7 s.
        WorldFx.Decal(Fx.Sprites.Scorch, at + Vector3.down * radius * 0.06f, new Color(0.086f, 0.055f, 0.118f, 0.92f),
            radius * 2.62f, 1.3f, 1.2f, 0f, 0.41f);
        for (int i = 0; i < 6; i++)
        {
            Vector2 spread = Random.insideUnitCircle;
            Vector3 p = at + new Vector3(spread.x * radius * 0.75f, spread.y * radius * 0.28f, 0f);
            WorldFx.Mote(p, Fx.Hues.Ember, Random.Range(0.1f, 0.14f), Vector3.up * 0.05f, Random.Range(1.2f, 1.8f));
        }

        // Whoever is left standing is thrown back from the blast (their kills pop through StampKills).
        List<EnemyManager> caught = EnemiesNear(at, radius * 1.1f, Cast.FieldTeam);
        foreach (EnemyManager enemy in caught)
        {
            Vector3 away = enemy.transform.position - at;
            Shove(enemy, (away.sqrMagnitude > 0.0004f ? away.normalized : Vector3.up) * 0.2f);
        }

        StartCoroutine(StampKills(caught, at + new Vector3(radius * 1.07f, radius * 0.72f, 0f)));
        Destroy(gameObject, 1.5f);
    }
}
