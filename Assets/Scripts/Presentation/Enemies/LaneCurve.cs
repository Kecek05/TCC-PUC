using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The lane as troops are DRAWN walking it: the gameplay <see cref="WaypointPath"/> with every corner rounded,
/// baked once into a lookup table indexed by the same normalised progress gameplay uses.
/// </summary>
/// <remarks>
/// The gameplay path stays a polyline: range checks, "first" targeting and Lance's band all read it untouched.
/// This curve only decides where a body is drawn, so a troop no longer snaps 90 degrees at each waypoint.
/// <para/>
/// A corner is cut short (a quadratic Bezier from <c>trim</c> before the waypoint to <c>trim</c> after it, through
/// the waypoint), and the table is indexed by the share of the DRAWN length covered, not by centreline distance, so
/// gameplay's constant progress becomes a constant speed on screen through bends as well as along straights.
/// <para/>
/// Server space, like the path: callers translate with <see cref="BaseMapTranslator.ServerToLocal"/>. One curve per
/// path instance, built lazily, cached for the scene.
/// </remarks>
public sealed class LaneCurve
{
    private static readonly Dictionary<WaypointPath, LaneCurve> Cache = new();
    private static readonly List<WaypointPath> Dead = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Cache.Clear();

    private readonly Vector2[] _positions;
    private readonly Vector2[] _tangents;

    /// <summary>Length of the gameplay centreline, the unit progress is measured in.</summary>
    public float Length { get; }

    public static LaneCurve For(WaypointPath path)
    {
        if (path == null) return null;
        if (Cache.TryGetValue(path, out LaneCurve curve)) return curve;

        // A scene's paths die with it; drop their curves before caching the next scene's.
        Dead.Clear();
        foreach (WaypointPath key in Cache.Keys)
            if (key == null) Dead.Add(key);
        foreach (WaypointPath key in Dead) Cache.Remove(key);

        TroopMotionSettingsSO settings = TroopMotionSettingsSO.Instance;
        curve = new LaneCurve(path, settings.CornerTrim, settings.CurveSampleSpacing);
        Cache[path] = curve;
        return curve;
    }

    private LaneCurve(WaypointPath path, float trim, float spacing)
    {
        int count = path.WaypointCount;
        Length = path.TotalLength;
        if (count < 2 || Length <= 0f)
        {
            _positions = new[] { (Vector2)path.transform.position };
            _tangents = new[] { Vector2.up };
            return;
        }

        var points = new Vector2[count];
        for (int i = 0; i < count; i++) points[i] = path.GetWaypointPosition(i);

        var lengths = new float[count - 1];
        var directions = new Vector2[count - 1];
        var starts = new float[count];
        for (int i = 0; i < count - 1; i++)
        {
            Vector2 d = points[i + 1] - points[i];
            lengths[i] = d.magnitude;
            directions[i] = lengths[i] > 1e-5f ? d / lengths[i] : (i > 0 ? directions[i - 1] : Vector2.up);
            starts[i + 1] = starts[i] + lengths[i];
        }

        // How far each interior waypoint's curve reaches along both of its segments: never past the middle of
        // either, so two corners sharing a short segment meet instead of overlapping.
        var trims = new float[count];
        for (int i = 1; i < count - 1; i++)
        {
            bool straight = Vector2.Dot(directions[i - 1], directions[i]) > 0.9998f;
            trims[i] = straight ? 0f : Mathf.Min(trim, lengths[i - 1] * 0.5f, lengths[i] * 0.5f);
        }

        int samples = Mathf.Clamp(Mathf.CeilToInt(Length / Mathf.Max(0.005f, spacing)) + 1, 2, 8192);
        var positions = new Vector2[samples];
        var tangents = new Vector2[samples];

        int segment = 0;
        for (int k = 0; k < samples; k++)
        {
            float d = Length * k / (samples - 1);
            while (segment < count - 2 && d > starts[segment + 1]) segment++;

            // Inside the curve of the waypoint this segment starts at, or of the one it ends at?
            int corner = -1;
            if (segment >= 1 && d - starts[segment] < trims[segment]) corner = segment;
            else if (segment + 1 <= count - 2 && starts[segment + 1] - d < trims[segment + 1]) corner = segment + 1;

            if (corner >= 0)
            {
                float t = trims[corner];
                Vector2 w = points[corner];
                Vector2 a = w - directions[corner - 1] * t;
                Vector2 b = w + directions[corner] * t;
                float u = Mathf.Clamp01((d - (starts[corner] - t)) / (2f * t));
                float v = 1f - u;
                positions[k] = v * v * a + 2f * u * v * w + u * u * b;
                Vector2 tangent = 2f * v * (w - a) + 2f * u * (b - w);
                tangents[k] = tangent.sqrMagnitude > 1e-10f ? tangent.normalized : directions[corner];
            }
            else
            {
                positions[k] = points[segment] + directions[segment] * (d - starts[segment]);
                tangents[k] = directions[segment];
            }
        }

        // Speed through a bend. Mapped straight onto centreline distance, a Bezier corner is crossed slower than a
        // straight, dipping to ~71% at its apex inside 0.7 units, and a lane with a bend every metre and a half read
        // as a walk that kept hitching. Mapping by the drawn curve's own length would make the speed constant, but
        // the corners' lost length then piles up into a drift of ~0.4 units between where a body is drawn and where
        // gameplay holds it. The middle way: the arc length reached at each centreline distance, low-pass filtered
        // over about a metre each side. The slowdown is spread into a gentle ease, and the drift stays under ~0.07.
        var walked = new float[samples];
        for (int k = 1; k < samples; k++) walked[k] = walked[k - 1] + Vector2.Distance(positions[k - 1], positions[k]);

        int reach = Mathf.Max(1, Mathf.RoundToInt(SpeedSmoothing / (Length / (samples - 1))));
        float[] eased = BoxFilter(BoxFilter(walked, reach), reach);

        _positions = new Vector2[samples];
        _tangents = new Vector2[samples];
        int source = 0;
        for (int k = 0; k < samples; k++)
        {
            float target = eased[k];
            while (source < samples - 2 && walked[source + 1] < target) source++;
            float span = walked[source + 1] - walked[source];
            float frac = span > 1e-7f ? Mathf.Clamp01((target - walked[source]) / span) : 0f;
            _positions[k] = Vector2.Lerp(positions[source], positions[source + 1], frac);
            Vector2 tangent = Vector2.Lerp(tangents[source], tangents[source + 1], frac);
            _tangents[k] = tangent.sqrMagnitude > 1e-10f ? tangent.normalized : tangents[source];
        }
    }

    /// <summary>World units each side over which a corner's slowdown is spread.</summary>
    private const float SpeedSmoothing = 0.55f;

    /// <summary>
    /// Moving average with a window that shrinks symmetrically toward the ends, so both ends stay exactly where
    /// they were (the lane's mouth and the base) and a monotonic input stays monotonic.
    /// </summary>
    private static float[] BoxFilter(float[] values, int reach)
    {
        int count = values.Length;
        var prefix = new double[count + 1];
        for (int i = 0; i < count; i++) prefix[i + 1] = prefix[i] + values[i];

        var result = new float[count];
        for (int i = 0; i < count; i++)
        {
            int window = Mathf.Min(reach, Mathf.Min(i, count - 1 - i));
            result[i] = (float)((prefix[i + window + 1] - prefix[i - window]) / (2 * window + 1));
        }
        return result;
    }

    /// <summary>
    /// Editor diagnostics: the largest distance between the drawn centreline and the gameplay polyline at the
    /// same progress, and the slowest / fastest drawn speed relative to gameplay's.
    /// </summary>
    public static (float maxOffset, float minSpeed, float maxSpeed) Measure(WaypointPath path)
    {
        LaneCurve curve = For(path);
        if (curve == null || curve._positions.Length < 3) return (0f, 1f, 1f);
        int last = curve._positions.Length - 1;
        float step = curve.Length / last;
        float maxOffset = 0f, minSpeed = float.MaxValue, maxSpeed = 0f;
        for (int k = 0; k <= last; k++)
        {
            float t = k / (float)last;
            maxOffset = Mathf.Max(maxOffset, Vector2.Distance(curve._positions[k], path.SamplePosition(t)));
            if (k == 0) continue;
            float speed = Vector2.Distance(curve._positions[k - 1], curve._positions[k]) / step;
            minSpeed = Mathf.Min(minSpeed, speed);
            maxSpeed = Mathf.Max(maxSpeed, speed);
        }
        return (maxOffset, minSpeed, maxSpeed);
    }

    /// <summary>
    /// The drawn lane at physical progress <paramref name="t"/> (0 = the path's first waypoint). Past either end it
    /// carries on straight along the end tangent, so a body nudged ahead of the last point does not pile up on it.
    /// </summary>
    public void Sample(float t, out Vector2 position, out Vector2 tangent)
    {
        int last = _positions.Length - 1;
        if (last == 0)
        {
            position = _positions[0];
            tangent = _tangents[0];
            return;
        }

        if (t <= 0f)
        {
            tangent = _tangents[0];
            position = _positions[0] + tangent * (t * Length);
            return;
        }

        if (t >= 1f)
        {
            tangent = _tangents[last];
            position = _positions[last] + tangent * ((t - 1f) * Length);
            return;
        }

        float f = t * last;
        int i = Mathf.Min((int)f, last - 1);
        float frac = f - i;
        position = Vector2.LerpUnclamped(_positions[i], _positions[i + 1], frac);
        Vector2 blended = Vector2.LerpUnclamped(_tangents[i], _tangents[i + 1], frac);
        tangent = blended.sqrMagnitude > 1e-10f ? blended.normalized : _tangents[i];
    }
}
