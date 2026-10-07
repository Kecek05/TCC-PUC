using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Lance (Spell Juice board "Lance"): a spear of light appears high above the enemy it will anchor on, pulls back a
/// hair and slams down; the light then races along the lane both ways for the strip's length, around bends, piercing
/// each enemy it reaches in order of distance, one semitone higher each. A glowing scar keeps the strip's shape for a
/// second. With no enemy under it the spear hits dirt and says so.
/// </summary>
/// <remarks>
/// The anchor and the strip are found exactly the way <see cref="LanceExecutor"/> finds them (closest enemy within
/// 1.5x the half-width, a band of progress along that enemy's own path), at the landing, so what lights up is what
/// the server hits.
/// </remarks>
public class LanceVisual : SpellVisual
{
    private const float SpearLength = 2.3f;
    private const float SpearHeight = 2.7f;
    private const float RaceTime = 0.16f;
    private const float LengthMultiplier = 3f;

    private readonly List<Vector3> _band = new();
    private Transform _spear;
    private LineRenderer _core, _glow, _crack;

    private float HalfWidth => Cast.Data != null ? Cast.Data.Range * Cast.Scale.Range : 0.45f;
    private float SearchRadius => HalfWidth * LengthMultiplier * 0.5f;

    protected override void OnPlay()
    {
        float travel = Mathf.Max(0.12f, Cast.TravelTime);
        EnemyManager predicted = SpellLane.FindAnchor(Cast.Position, Cast.FieldTeam, SearchRadius,
            FindObjectsByType<EnemyManager>(FindObjectsSortMode.None));
        Vector3 Target() => predicted != null && predicted.isActiveAndEnabled ? predicted.transform.position : Cast.Position;

        // The spear: its tip is the container's origin, so moving the container moves the point that lands.
        _spear = new GameObject("Spear").transform;
        _spear.SetParent(transform, false);
        SpriteRenderer blade = Sprite("Blade", SpellFxSettingsSO.Or(Fx.Sprites.Spear, JuiceSprites.Ray), Hue, SpearLength, false, 36, _spear);
        blade.transform.localPosition = Vector3.up * SpearLength * 0.5f;

        Vector3 start = Target() + Vector3.up * SpearHeight;
        _spear.position = start;
        WorldFx.Flash(start, Color.white, 0.22f, 0.25f);
        WorldFx.Sparks(start, Fx.Hues.LightEdge, 4, 0.3f, 0.14f);

        // Anticipation (a hair up on ease-out), then the fall onto wherever the anchor has walked to by then.
        float lift = 0.08f;
        Vector3 lifted = start + Vector3.up * 0.16f;
        _spear.DOMove(lifted, lift).SetEase(T.Eases.Out).SetLink(gameObject).OnComplete(() =>
        {
            DOVirtual.Float(0f, 1f, travel - lift, k =>
                {
                    if (_spear == null) return;
                    _spear.position = Vector3.LerpUnclamped(lifted, Target(), k * k);
                })
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    Vector3 tip = _spear != null ? _spear.position : Cast.Position;
                    WorldFx.Streak(lifted, tip, Color.white, 0.2f, 0.25f);
                    Impact();
                });
        });

        Sfx(SfxId.SpellWindUp, 0, 0.7f);
    }

    private void Impact()
    {
        if (_spear != null) Destroy(_spear.gameObject);

        List<EnemyManager> enemies = new(FindObjectsByType<EnemyManager>(FindObjectsSortMode.None));
        EnemyManager anchor = SpellLane.FindAnchor(Cast.Position, Cast.FieldTeam, SearchRadius, enemies);

        if (anchor == null || !SpellLane.TryGetPath(anchor, out WaypointPath path))
        {
            Fizzle(anchor != null ? anchor.transform.position : Cast.Position);
            return;
        }

        Vector3 anchorAt = anchor.transform.position;
        float halfBand = HalfWidth * LengthMultiplier / path.TotalLength;
        float center = anchor.ServerMovement.PathProgress.Value;
        float physical = SpellLane.PhysicalProgress(anchor);
        TeamType team = anchor.Team.GetTeamType();
        SpellLane.Sample(path, physical - halfBand, physical + halfBand, team, _band);

        Wash(Color.white, 0.22f);
        Shake(T.Shake.Small, T.Durations.Shake);
        Buzz(HapticRole.Medium);
        Sfx(SfxId.Impact);
        WorldFx.Flash(anchorAt, Color.white, 0.55f);
        WorldFx.Ring(anchorAt, Color.white, 0.1f, 0.55f, 0.35f);

        // The race: the strip lights from the anchor outward both ways.
        int anchorIndex = NearestIndex(_band, anchorAt);
        _glow = Line("Glow", WithAlpha(Fx.Hues.LightEdge, 0.45f), 0.5f, 37);
        _core = Line("Core", Color.white, 0.16f, 38);
        DOVirtual.Float(0f, 1f, RaceTime, k => Reveal(anchorIndex, k)).SetEase(Ease.Linear).SetLink(gameObject)
            .OnComplete(Scar);

        // Every enemy in the band, pierced in order of distance (the server's own test: same path, progress band).
        List<(EnemyManager enemy, float distance)> hits = new();
        foreach (EnemyManager enemy in enemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled || enemy.Team == null) continue;
            if (enemy.Team.GetTeamType() != Cast.FieldTeam || enemy.PathAssignment == null) continue;
            if (enemy.PathAssignment.TargetMap != anchor.PathAssignment.TargetMap) continue;

            float distance = Mathf.Abs(enemy.ServerMovement.PathProgress.Value - center);
            if (distance <= halfBand) hits.Add((enemy, distance));
        }

        hits.Sort((a, b) => a.distance.CompareTo(b.distance));
        List<EnemyManager> targets = new();
        for (int i = 0; i < hits.Count; i++)
        {
            EnemyManager enemy = hits[i].enemy;
            int step = i;
            targets.Add(enemy);
            DOVirtual.DelayedCall(hits[i].distance / Mathf.Max(0.0001f, halfBand) * RaceTime, () => Pierce(enemy, step), false)
                .SetLink(gameObject);
        }

        StartCoroutine(StampKills(targets, anchorAt + Vector3.up * 0.75f));
        Destroy(gameObject, 2.4f);
    }

    private void Pierce(EnemyManager enemy, int step)
    {
        if (enemy == null || !enemy.isActiveAndEnabled) return;
        Vector3 at = enemy.transform.position;
        Vector3 along = LaneTangent(at);

        WorldFx.Streak(at - along * 0.32f, at + along * 0.32f, Color.white, 0.12f, 0.2f);
        WorldFx.Sparks(at, Fx.Hues.LightEdge, 4, 0.35f, 0.14f);
        if (CameraFeedback.IsOnScreen(at, 0.05f)) Juice.Sfx(SfxId.SpellPierce, step);
    }

    /// <summary>The strip after the race: a scar that keeps its shape, motes rising off it, a crack under it.</summary>
    private void Scar()
    {
        if (_core == null) return;

        _crack = Line("Crack", WithAlpha(new Color(0.106f, 0.078f, 0.188f), 0.55f), 0.07f, -9, true);
        _crack.positionCount = _band.Count;
        _crack.SetPositions(_band.ToArray());

        DOVirtual.Float(1f, 0f, 1f, a =>
        {
            if (_core == null) return;
            Color c = Color.Lerp(Fx.Hues.LightEdge, Color.white, a);
            c.a = a;
            _core.startColor = _core.endColor = c;
            _core.widthMultiplier = Mathf.Lerp(0.08f, 0.16f, a);
        }).SetEase(T.Eases.In).SetLink(gameObject);

        DOVirtual.Float(0.45f, 0f, 0.3f, a => { if (_glow != null) _glow.startColor = _glow.endColor = WithAlpha(Fx.Hues.LightEdge, a); })
            .SetLink(gameObject);
        DOVirtual.Float(0.55f, 0f, 1.6f, a => { if (_crack != null) _crack.startColor = _crack.endColor = WithAlpha(_crack.startColor, a); })
            .SetEase(T.Eases.In).SetLink(gameObject);

        for (int i = 0; i < (Juice.ReduceMotion ? 3 : 7); i++)
        {
            Vector3 p = _band[Random.Range(0, _band.Count)];
            WorldFx.Mote(p, i % 2 == 0 ? Color.white : Fx.Hues.LightEdge, 0.12f, Vector3.up * Random.Range(0.25f, 0.45f), 0.8f);
        }
    }

    /// <summary>No enemy under the spear: it hits dirt, and the board says so rather than pretending.</summary>
    private void Fizzle(Vector3 at)
    {
        WorldFx.Puff(Fx.Sprites.Smoke, at, WithAlpha(Juice.Tokens.Colors.Dust, 0.4f), 0.6f, Vector3.up * 0.25f, 0.8f);
        WorldFx.Ring(at, Juice.Tokens.Colors.InkMuted, 0.1f, 0.4f, 0.3f);
        if (Seen)
        {
            FloatingNumbers.Say(at + Vector3.up * 0.4f, "NO TARGET", 0.9f, Juice.Tokens.Colors.InkMuted);
            Juice.Sfx(SfxId.Thud, 0, 0.6f);
        }
        Destroy(gameObject, 1f);
    }

    private void Reveal(int anchorIndex, float k)
    {
        if (_core == null || _band.Count < 2) return;

        int reach = Mathf.CeilToInt(k * Mathf.Max(anchorIndex, _band.Count - 1 - anchorIndex));
        int from = Mathf.Max(0, anchorIndex - reach);
        int to = Mathf.Min(_band.Count - 1, anchorIndex + reach);
        int count = Mathf.Max(2, to - from + 1);

        Vector3[] points = new Vector3[count];
        for (int i = 0; i < count; i++) points[i] = _band[Mathf.Min(from + i, _band.Count - 1)];

        _core.positionCount = _glow.positionCount = count;
        _core.SetPositions(points);
        _glow.SetPositions(points);
    }

    private Vector3 LaneTangent(Vector3 at)
    {
        int i = NearestIndex(_band, at);
        if (_band.Count < 2) return Vector3.up;
        Vector3 a = _band[Mathf.Max(0, i - 1)], b = _band[Mathf.Min(_band.Count - 1, i + 1)];
        Vector3 d = b - a;
        return d.sqrMagnitude > 0.000001f ? d.normalized : Vector3.up;
    }

    private static int NearestIndex(List<Vector3> points, Vector3 at)
    {
        int best = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < points.Count; i++)
        {
            float d = ((Vector2)(points[i] - at)).sqrMagnitude;
            if (d >= bestDistance) continue;
            bestDistance = d;
            best = i;
        }
        return best;
    }

    private LineRenderer Line(string name, Color color, float width, int order, bool ground = false)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = LineMaterial;
        line.useWorldSpace = true;
        line.widthMultiplier = width;
        line.numCapVertices = 4;
        line.numCornerVertices = 3;
        line.startColor = line.endColor = color;
        line.sortingLayerName = ground ? GroundLayer : AirLayer;
        line.sortingOrder = order;
        line.positionCount = 0;
        return line;
    }
}
