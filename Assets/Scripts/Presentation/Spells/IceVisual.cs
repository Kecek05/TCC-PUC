using DG.Tweening;
using UnityEngine;

/// <summary>
/// Ice (Spell Juice board "Ice"): a faceted crystal drops onto their field in 0.2 s and bursts into a frost nova (a
/// cold flash, a ring of spikes out of the ground, mist and snow sparkle); the ground stays frosted for as long as the
/// freeze. The towers it catches are sealed by <see cref="TowerStatusView"/>, which reads this cast's zone for the
/// frost clock and the moment to crack and shatter.
/// </summary>
public class IceVisual : SpellVisual
{
    private const float CrystalLength = 0.85f;

    private Transform _crystal;

    protected override void OnPlay()
    {
        float travel = Mathf.Max(0.05f, Cast.TravelTime);
        Vector3 at = Cast.Position;
        Vector3 from = at + new Vector3(0.9f, 2.4f, 0f);

        _crystal = new GameObject("Crystal").transform;
        _crystal.SetParent(transform, false);
        _crystal.position = from;
        Vector3 dir = (at - from).normalized;
        _crystal.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-dir.y, -dir.x) * Mathf.Rad2Deg - 90f);
        Sprite("Shard", SpellFxSettingsSO.Or(Fx.Sprites.IceShard, JuiceSprites.Star), Hue, CrystalLength, false, 35, _crystal);
        Sprite("Glint", JuiceSprites.Glow, WithAlpha(Color.white, 0.8f), 0.45f, false, 36, _crystal);

        _crystal.localScale = Vector3.one * 0.7f;
        _crystal.DOScale(1f, travel).SetEase(T.Eases.Fall).SetLink(gameObject);
        _crystal.DOMove(at, travel).SetEase(T.Eases.Fall).SetLink(gameObject).OnComplete(() =>
        {
            WorldFx.Streak(from, at, Fx.Hues.IceSoft, 0.16f, 0.3f);
            Impact();
        });

        Sfx(SfxId.Whoosh, 0, 0.6f);
    }

    private void Impact()
    {
        if (_crystal != null) Destroy(_crystal.gameObject);

        Vector3 at = Cast.Position;
        float radius = Mathf.Max(0.3f, Cast.Range);
        float freeze = Mathf.Max(0.5f, Cast.Duration);

        Wash(Fx.Hues.IceSoft, 0.3f);
        Shake(T.Shake.Small, T.Durations.ShakeLong);
        Buzz(HapticRole.Medium);
        Sfx(SfxId.SpellFreeze);

        WorldFx.Flash(at, Color.white, radius * 0.8f);
        WorldFx.Ring(at, Hue, radius * 0.2f, radius * 1.15f, 0.45f);
        DOVirtual.DelayedCall(0.07f, () => WorldFx.Ring(at, Color.white, radius * 0.3f, radius * 1.3f, 0.55f), false).SetLink(gameObject);

        // The nova: a ring of spikes bursting out of the ground, overshooting a little, then melting away.
        SpriteRenderer nova = Sprite("Nova", SpellFxSettingsSO.Or(Fx.Sprites.FrostNova, JuiceSprites.Ring), Hue, radius * 2.5f, false, 30);
        Transform novaT = nova.transform;
        Vector3 rest = novaT.localScale;
        novaT.localScale = rest * 0.3f;
        DOTween.Sequence().SetLink(gameObject)
            .Join(novaT.DOScale(rest * 1.06f, 0.18f).SetEase(T.Eases.Pop))
            .Append(novaT.DOScale(rest, 0.12f))
            .Join(novaT.DORotate(new Vector3(0f, 0f, 10f), 0.3f))
            .AppendInterval(0.45f)
            .Append(nova.DOFade(0f, 0.4f).SetEase(T.Eases.In))
            .Join(novaT.DORotate(new Vector3(0f, 0f, 16f), 0.4f));

        for (int i = 0; i < (Juice.ReduceMotion ? 1 : 3); i++)
        {
            Vector3 p = at + (Vector3)(Random.insideUnitCircle * radius * 0.4f);
            WorldFx.Puff(Fx.Sprites.Smoke, p, WithAlpha(Fx.Hues.IceSoft, 0.5f), 0.9f, Vector3.up * 0.25f, 1.1f);
        }

        WorldFx.Burst(JuiceSprites.Star, at, Color.white, 8, radius * 1.1f, 0.18f);
        WorldFx.Debris(new[] { Fx.Sprites.IceShard }, at, radius * 0.8f, Fx.Hues.IceSoft, 6, 0.22f);

        // The ground stays frosted for the freeze, then lets go a beat before the towers thaw.
        // The board's frosted ground: ice-soft at 34% in the middle melting into the hue and gone by seven tenths of
        // a 132px disc (the reach is 112), under nine hairlines of rime.
        float hold = Mathf.Max(0f, freeze - 0.6f);
        WorldFx.Decal(JuiceSprites.Glow, at, WithAlpha(Fx.Hues.IceSoft, 0.34f), radius * 2.1f, hold, 0.6f);
        WorldFx.Decal(JuiceSprites.Glow, at, WithAlpha(Hue, 0.18f), radius * 2.36f, hold, 0.6f);
        for (int i = 0; i < 9; i++)
        {
            SpriteRenderer line = Sprite("Rime", JuiceSprites.Ray, new Color(1f, 1f, 1f, 0.2f), 1f, true, -11);
            line.transform.localRotation = Quaternion.Euler(0f, 0f, 10f + i * 40f);
            line.transform.localScale = new Vector3(0.06f / 1.28f, radius * 0.95f / 1.28f, 1f);
            line.transform.localPosition = line.transform.localRotation * Vector3.up * radius * 0.12f;
            line.DOFade(0f, 0.6f).SetDelay(hold).SetEase(T.Eases.In).SetLink(gameObject);
        }

        RegisterZone();
        Destroy(gameObject, freeze + 0.5f);
    }
}
