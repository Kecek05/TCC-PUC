using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Haste (Spell Juice board "Haste"): a clock rune slams down and winds up (a fast spin that settles into a steady
/// turn), lightning jumps from it to each of your towers inside in turn, one semitone higher each, and near the end its
/// twelve ticks go out one by one before it winds down and stops. The towers wear their halo through
/// <see cref="TowerStatusView"/>, off the replicated haste state.
/// </summary>
/// <remarks>
/// Re-scans for towers placed inside after the cast (the zone does too: HasteExecutor buffs late arrivals), so a
/// tower dropped into a running rune gets its own zap.
/// </remarks>
public class HasteVisual : SpellVisual
{
    private const int Ticks = 12;
    private const float WindDown = 0.6f;
    private const float TurnSpeed = -50f;

    private Transform _spinner;
    private SpriteRenderer _rune, _fill;
    private readonly List<SpriteRenderer> _ticks = new();
    private readonly HashSet<TowerManager> _zapped = new();
    private float _landedAt = -1f;
    private float _spinSpeed;
    private int _zapStep;

    protected override void OnPlay()
    {
        DOVirtual.DelayedCall(Mathf.Max(0f, Cast.TravelTime), Land, false).SetLink(gameObject);
    }

    private void Land()
    {
        float radius = Mathf.Max(0.3f, Cast.Range);
        float duration = Mathf.Max(0.8f, Cast.Duration);
        Vector3 at = Cast.Position;
        _landedAt = Time.time;

        _fill = Sprite("Fill", JuiceSprites.Glow, WithAlpha(Hue, 0.32f), radius * 2.3f, true, -14);
        Bloom(_fill.transform);

        _spinner = new GameObject("Spinner").transform;
        _spinner.SetParent(transform, false);
        _rune = Sprite("Rune", SpellFxSettingsSO.Or(Fx.Sprites.HasteRune, JuiceSprites.DashedRing), WithAlpha(Hue, 0.95f), radius * 2f, true, -13, _spinner);
        for (int i = 0; i < Ticks; i++)
        {
            float a = i * Mathf.PI * 2f / Ticks;
            SpriteRenderer tick = Sprite($"Tick{i}", JuiceSprites.Glow, Fx.Hues.HasteSoft, 0.2f, true, -12, _spinner);
            tick.transform.localPosition = new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f) * radius * 0.83f;
            _ticks.Add(tick);
        }

        // Wind-up: a fast turn that settles into the steady one.
        Bloom(_spinner);
        _spinner.DORotate(new Vector3(0f, 0f, -540f), 0.35f, RotateMode.FastBeyond360).SetEase(T.Eases.Snap).SetLink(gameObject)
            .OnComplete(() => _spinSpeed = TurnSpeed);

        UnityEngine.UI.Image clock = BuildClock(radius, -11);
        clock.DOFillAmount(0f, duration).SetEase(Ease.Linear).SetLink(gameObject);

        WorldFx.Ring(at, Hue, radius * 0.3f, radius * 1.1f, 0.45f);
        Sfx(SfxId.SpellWindUp);
        Buzz(HapticRole.Light);

        float bonus = Cast.Data is SpellBuffDataSO buff ? buff.AttackSpeedBonus * Cast.Scale.EffectBonus : 0f;
        RegisterZone(0f, bonus);

        StartCoroutine(ZapLoop(duration));
        StartCoroutine(Crackle(duration));
        StartCoroutine(TickOut(duration));
    }

    private void Update()
    {
        if (_spinner == null || _landedAt < 0f) return;
        if (!Juice.ReduceMotion) _spinner.Rotate(0f, 0f, _spinSpeed * Time.deltaTime);
    }

    /// <summary>Zaps every tower of the caster's inside the rune, now and whenever one is placed inside later.</summary>
    private IEnumerator ZapLoop(float duration)
    {
        yield return new WaitForSeconds(0.12f);
        float end = _landedAt + duration;
        float radius = Cast.Range;

        while (Time.time < end)
        {
            ClientTowerRegistry.Cleanup();
            List<TowerManager> fresh = new();
            foreach (TowerManager tower in ClientTowerRegistry.ActiveTowers)
            {
                if (tower == null || _zapped.Contains(tower) || tower.Team == null) continue;
                if (tower.Team.GetTeamType() != Cast.CasterTeam) continue;
                if (((Vector2)(tower.transform.position - Cast.Position)).sqrMagnitude > radius * radius) continue;
                fresh.Add(tower);
            }

            fresh.Sort((a, b) => Vector2.SqrMagnitude(a.transform.position - Cast.Position)
                .CompareTo(Vector2.SqrMagnitude(b.transform.position - Cast.Position)));

            foreach (TowerManager tower in fresh)
            {
                _zapped.Add(tower);
                Zap(tower);
                yield return new WaitForSeconds(0.06f);
            }

            yield return new WaitForSeconds(0.25f);
        }
    }

    /// <summary>A bolt from the rune's heart to the tower: it flickers twice, the tower gets a ring and sparks.</summary>
    private void Zap(TowerManager tower)
    {
        Vector3 from = Cast.Position;
        Vector3 to = tower.transform.position;
        int step = _zapStep++;

        LineRenderer glow = Bolt(from, to, WithAlpha(Hue, 0.5f), 0.24f, 38);
        LineRenderer core = Bolt(from, to, Color.white, 0.07f, 39);

        DOTween.Sequence().SetLink(gameObject)
            .AppendInterval(0.06f)
            .AppendCallback(() => { Jag(glow, from, to); Jag(core, from, to); SetLineAlpha(core, 0.25f); SetLineAlpha(glow, 0.2f); })
            .AppendInterval(0.04f)
            .AppendCallback(() => { SetLineAlpha(core, 1f); SetLineAlpha(glow, 0.5f); })
            .Append(DOVirtual.Float(1f, 0f, 0.14f, a => { SetLineAlpha(core, a); SetLineAlpha(glow, a * 0.5f); }))
            .OnComplete(() =>
            {
                if (glow != null) Destroy(glow.gameObject);
                if (core != null) Destroy(core.gameObject);
            });

        WorldFx.Ring(to, Hue, 0.1f, 0.5f, 0.3f);
        WorldFx.Sparks(to, Fx.Hues.HasteSoft, 4, 0.35f, 0.14f);
        if (CameraFeedback.IsOnScreen(to, 0.05f)) Juice.Sfx(SfxId.SpellZap, step);
        if (Cast.CasterIsLocal) Juice.Haptic(HapticRole.Select);
    }

    private IEnumerator Crackle(float duration)
    {
        float end = _landedAt + duration - WindDown;
        while (Time.time < end)
        {
            yield return new WaitForSeconds(Random.Range(0.35f, 0.65f));
            if (Juice.ReduceMotion) continue;
            float a = Random.Range(0f, Mathf.PI * 2f);
            Vector3 p = Cast.Position + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Cast.Range * 0.95f;
            WorldFx.Sparks(p, Fx.Hues.HasteSoft, 2, 0.2f, 0.12f);
        }
    }

    /// <summary>The clock running out: the ticks go dark one by one, counter-clockwise, the turn slows to a stop.</summary>
    private IEnumerator TickOut(float duration)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, duration - WindDown));

        float startSpeed = _spinSpeed;
        DOVirtual.Float(1f, 0f, WindDown, k => _spinSpeed = startSpeed * k).SetLink(gameObject);

        for (int i = 0; i < _ticks.Count; i++)
        {
            SpriteRenderer tick = _ticks[(_ticks.Count - i) % _ticks.Count];
            tick.DOFade(0.12f, 0.08f).SetLink(gameObject);
            tick.transform.DOScale(tick.transform.localScale * 0.6f, 0.08f).SetLink(gameObject);
            if (i % 2 == 0) Sfx(SfxId.Tick, 0, 0.45f);
            yield return new WaitForSeconds(WindDown / _ticks.Count);
        }

        // Stopped: the rune lets go and fades as the halos pop off their towers.
        float radius = Cast.Range;
        WorldFx.Ring(Cast.Position, Fx.Hues.HasteSoft, radius * 0.9f, radius * 1.1f, 0.3f);
        Sfx(SfxId.SpellSeal, 0, 0.6f);
        if (_spinner != null) _spinner.DOScale(_spinner.localScale * 0.88f, 0.3f).SetEase(T.Eases.In).SetLink(gameObject);
        if (_rune != null) _rune.DOFade(0f, 0.3f).SetEase(T.Eases.In).SetLink(gameObject);
        if (_fill != null) _fill.DOFade(0f, 0.3f).SetEase(T.Eases.In).SetLink(gameObject);
        foreach (SpriteRenderer tick in _ticks) tick.DOFade(0f, 0.3f).SetLink(gameObject);
        Destroy(gameObject, 0.45f);
    }

    private void Bloom(Transform target)
    {
        Vector3 rest = target.localScale;
        target.localScale = rest * 0.3f;
        target.DOScale(rest, T.Durations.Pop).SetEase(T.Eases.Pop).SetLink(gameObject);
    }

    private LineRenderer Bolt(Vector3 from, Vector3 to, Color color, float width, int order)
    {
        GameObject go = new GameObject("Bolt");
        go.transform.SetParent(transform, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = LineMaterial;
        line.useWorldSpace = true;
        line.widthMultiplier = width;
        line.numCapVertices = 2;
        line.startColor = line.endColor = color;
        line.sortingLayerName = AirLayer;
        line.sortingOrder = order;
        Jag(line, from, to);
        return line;
    }

    /// <summary>A fresh zigzag between two points: the bolt never holds the same shape for two frames of its flicker.</summary>
    private static void Jag(LineRenderer line, Vector3 from, Vector3 to)
    {
        if (line == null) return;
        const int points = 7;
        Vector3 across = Vector3.Cross(to - from, Vector3.forward).normalized;
        line.positionCount = points;
        for (int i = 0; i < points; i++)
        {
            float t = i / (points - 1f);
            float jitter = i == 0 || i == points - 1 ? 0f : Random.Range(-0.13f, 0.13f);
            line.SetPosition(i, Vector3.Lerp(from, to, t) + across * jitter);
        }
    }

    private static void SetLineAlpha(LineRenderer line, float alpha)
    {
        if (line == null) return;
        line.startColor = line.endColor = WithAlpha(line.startColor, alpha);
    }
}
