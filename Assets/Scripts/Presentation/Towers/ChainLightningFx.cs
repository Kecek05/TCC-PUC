using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The Chain tower's bolt jumping through a wave: once the shot lands on its first target, an arc leaps from enemy
/// to enemy in the order the server hit them, each leap a beat after the last, thinner and dimmer the way its damage
/// falls off, a ring and sparks where it lands and the zap climbing a semitone per jump. Drawn in the tower's own
/// colour round a white core, because that colour is the damage it deals.
/// </summary>
/// <remarks>
/// Pure presentation, fed one list of points per shot (<see cref="ServerChainTowerCombat"/> sends it in a single
/// Rpc). The arcs are jagged line renderers, the same bolt Haste's zap is drawn with, pooled per scene and animated
/// by hand in <see cref="Update"/>: a Chain fires about once a second per tower, and a tween per leap would be
/// garbage on every one of them. Scaled time, like the hit it shows. Unlit, like every effect (see
/// <see cref="JuiceSprites.Unlit"/>).
/// </remarks>
public class ChainLightningFx : MonoBehaviour
{
    /// <summary>The beat between one leap and the next: fast enough to read as one bolt, slow enough to read its path.</summary>
    private const float HopSeconds = 0.045f;
    private const float HoldSeconds = 0.07f;
    private const float FadeSeconds = 0.16f;
    private const float RejagAt = 0.04f;
    private const int Points = 7;
    private const float MinStrength = 0.45f;

    private sealed class Arc
    {
        public LineRenderer Glow, Core;
        public Vector3 From, To;
        public float Start;
        public float Strength;
        public Color Tint;
        public int Step;
        public bool Landed, Rejagged;
    }

    private static ChainLightningFx _instance;
    private static Material _fallbackMaterial;

    private readonly List<Arc> _active = new();
    private readonly Stack<Arc> _free = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        _fallbackMaterial = null;
    }

    /// <summary>
    /// Plays one chain: <paramref name="nodes"/> are the enemies it hit, first to last, in this client's view.
    /// </summary>
    /// <param name="tint">The tower's colour (its attack colour).</param>
    /// <param name="falloff">Damage lost per jump (0.25 = each leap is three quarters of the last): the arcs thin with it.</param>
    public static void Play(IReadOnlyList<Vector3> nodes, Color tint, float falloff)
    {
        if (nodes == null || nodes.Count < 2 || !Application.isPlaying) return;

        // Each player watches one field at a time: a chain nobody can see is not drawn.
        bool seen = false;
        for (int i = 0; i < nodes.Count && !seen; i++) seen = CameraFeedback.IsOnScreen(nodes[i], 0.1f);
        if (!seen) return;

        if (_instance == null)
        {
            GameObject go = new GameObject("[Juice] ChainLightning");
            SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
            _instance = go.AddComponent<ChainLightningFx>();
        }

        // A colourless tower still throws a visible bolt.
        tint.a = 1f;
        if (tint.r > 0.9f && tint.g > 0.9f && tint.b > 0.9f) tint = Juice.Tokens.Colors.Sparkle;

        // Where the bolt enters the wave: the brightest point of the chain.
        WorldFx.Flash(nodes[0], Color.Lerp(tint, Color.white, 0.6f), 0.3f);
        WorldFx.Ring(JuiceSprites.Outline, nodes[0], tint, 0.07f, 0.34f, 0.25f);

        float now = Time.time;
        float keep = 1f - Mathf.Clamp01(falloff);
        float strength = 1f;
        for (int i = 0; i < nodes.Count - 1; i++)
        {
            Arc arc = _instance.Take();
            arc.From = nodes[i];
            arc.To = nodes[i + 1];
            arc.Start = now + i * HopSeconds;
            arc.Strength = Mathf.Max(MinStrength, strength);
            arc.Tint = tint;
            arc.Step = i;
            arc.Landed = arc.Rejagged = false;
            _instance._active.Add(arc);
            strength *= keep;
        }
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        float now = Time.time;
        bool calm = Juice.ReduceMotion;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            Arc arc = _active[i];
            float t = now - arc.Start;
            if (t < 0f) continue;

            if (!arc.Landed)
            {
                arc.Landed = true;
                arc.Glow.widthMultiplier = 0.24f * arc.Strength;
                arc.Core.widthMultiplier = 0.07f * arc.Strength;
                Jag(arc);
                arc.Glow.enabled = arc.Core.enabled = true;
                Land(arc, calm);
            }

            // A bolt never holds one shape: it is redrawn once while it is still at full brightness.
            if (!arc.Rejagged && t >= RejagAt && !calm)
            {
                arc.Rejagged = true;
                Jag(arc);
            }

            float alpha = t < HoldSeconds ? 1f : 1f - (t - HoldSeconds) / FadeSeconds;
            if (alpha <= 0f)
            {
                Release(arc);
                _active.RemoveAt(i);
                continue;
            }

            Color glow = arc.Tint;
            glow.a = 0.55f * alpha;
            arc.Glow.startColor = arc.Glow.endColor = glow;
            Color core = Color.Lerp(Color.white, arc.Tint, 1f - alpha);
            core.a = alpha;
            arc.Core.startColor = arc.Core.endColor = core;
        }
    }

    /// <summary>The leap arrives: a ring and sparks on the enemy it reached, and the zap one step higher than the last.</summary>
    private static void Land(Arc arc, bool calm)
    {
        float size = arc.Strength;
        WorldFx.Ring(JuiceSprites.Outline, arc.To, arc.Tint, 0.06f, 0.14f + 0.2f * size, 0.25f);
        WorldFx.Sparks(arc.To, Color.Lerp(arc.Tint, Color.white, 0.5f), calm ? 2 : 3, 0.3f * size, 0.13f);
        if (CameraFeedback.IsOnScreen(arc.To, 0.05f)) Juice.Sfx(SfxId.SpellZap, arc.Step, 0.4f);
    }

    /// <summary>A fresh zigzag between the two enemies, its swing in proportion to the leap and to its strength.</summary>
    private static void Jag(Arc arc)
    {
        Vector3 along = arc.To - arc.From;
        Vector3 across = Vector3.Cross(along, Vector3.forward).normalized;
        float swing = Mathf.Min(0.14f, along.magnitude * 0.18f) * arc.Strength;

        for (int i = 0; i < Points; i++)
        {
            float t = i / (Points - 1f);
            float jitter = i == 0 || i == Points - 1 ? 0f : Random.Range(-swing, swing);
            Vector3 point = Vector3.Lerp(arc.From, arc.To, t) + across * jitter;
            arc.Glow.SetPosition(i, point);
            arc.Core.SetPosition(i, point);
        }
    }

    private Arc Take()
    {
        while (_free.Count > 0)
        {
            Arc pooled = _free.Pop();
            if (pooled.Glow != null && pooled.Core != null) return pooled;
        }

        return new Arc { Glow = Line("Glow", 41), Core = Line("Core", 42) };
    }

    private void Release(Arc arc)
    {
        arc.Glow.enabled = arc.Core.enabled = false;
        _free.Push(arc);
    }

    private LineRenderer Line(string name, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = LineMaterial;
        line.useWorldSpace = true;
        line.positionCount = Points;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.sortingLayerName = "UI";
        line.sortingOrder = order;
        line.enabled = false;
        return line;
    }

    /// <summary>The unlit effect material, or without it the pipeline's default sprite material read off a throwaway renderer.</summary>
    private static Material LineMaterial
    {
        get
        {
            if (JuiceSprites.Unlit != null) return JuiceSprites.Unlit;
            if (_fallbackMaterial != null) return _fallbackMaterial;

            GameObject probe = new GameObject("LineMaterialProbe");
            _fallbackMaterial = probe.AddComponent<SpriteRenderer>().sharedMaterial;
            Destroy(probe);
            return _fallbackMaterial;
        }
    }
}
