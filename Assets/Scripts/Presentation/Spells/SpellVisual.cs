using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One spell's cast, drawn on one client: the flight, the landing, the zone and its end (Spell Juice's grammar:
/// release, flight, impact, linger, end). Each spell has its own subclass, chosen by <see cref="SpellVisualFactory"/>
/// the way <see cref="SpellExecutorFactory"/> chooses its executor, and builds itself in code from
/// <see cref="SpellFxSettingsSO"/>: no prefab per spell to keep in sync.
/// </summary>
/// <remarks>
/// Pure presentation, on scaled time like the gameplay it shows. The channels follow the Juice rules: sounds,
/// shakes and flashes only where the player is looking, and haptics only for the caster (a spell landing on your
/// towers plays its full look and sound, but only your own actions buzz).
/// <para/>
/// Sorting: what lies on the ground (zones, decals, the shadow) goes on the enemies' layer under their bodies;
/// what is in the air (a meteor, a spear, flashes) on the UI layer with <see cref="WorldFx"/>.
/// </remarks>
public abstract class SpellVisual : MonoBehaviour
{
    protected const string GroundLayer = "Enemy";
    protected const string AirLayer = "UI";

    private SpellZone _zone;

    protected SpellCastInfo Cast { get; private set; }

    protected static SpellFxSettingsSO Fx => SpellFxSettingsSO.Instance;
    protected static JuiceTokensSO T => Juice.Tokens;

    protected Color Hue => Fx.HueFor(Cast.Spell);
    protected Color Soft => Fx.SoftHueFor(Cast.Spell);

    public void Play(SpellCastInfo cast)
    {
        Cast = cast;
        transform.position = cast.Position;
        OnPlay();
    }

    protected abstract void OnPlay();

    protected virtual void OnDestroy()
    {
        if (_zone != null) SpellZoneRegistry.Remove(_zone);
    }

    // ---------- channels ----------

    protected bool Seen => CameraFeedback.IsOnScreen(Cast.Position, 0.15f);

    protected void Sfx(SfxId id, int step = 0, float volume = 1f)
    {
        if (Seen) Juice.Sfx(id, step, volume);
    }

    protected void Buzz(HapticRole role)
    {
        if (Cast.CasterIsLocal) Juice.Haptic(role);
    }

    protected void Shake(float share, float duration)
    {
        if (Seen) JuiceCamera.Shake(share, duration);
    }

    protected void Wash(Color color, float peak)
    {
        if (Seen) ScreenFlash.Flash(color, peak);
    }

    // ---------- targets ----------

    /// <summary>Enemies walking at <paramref name="team"/>'s field within <paramref name="radius"/> of a point, right now.</summary>
    protected static List<EnemyManager> EnemiesNear(Vector3 at, float radius, TeamType team)
    {
        List<EnemyManager> found = new();
        foreach (EnemyManager enemy in Object.FindObjectsByType<EnemyManager>(FindObjectsSortMode.None))
        {
            if (enemy == null || !enemy.isActiveAndEnabled || enemy.Team == null) continue;
            if (enemy.Team.GetTeamType() != team) continue;
            if (((Vector2)(enemy.transform.position - at)).sqrMagnitude > radius * radius) continue;
            found.Add(enemy);
        }
        return found;
    }

    /// <summary>
    /// Counts the kills among <paramref name="targets"/> over the next moment (each enemy's exit relay says how it
    /// left) and stamps "xN" for two or more: a multi-kill is the payoff of a well-read cast.
    /// </summary>
    protected IEnumerator StampKills(List<EnemyManager> targets, Vector3 stampAt, float window = 0.6f)
    {
        int kills = 0;
        List<EnemyFeedbackRelay> watched = new();
        System.Action<EnemyExitKind> onExit = kind => { if (kind == EnemyExitKind.Killed) kills++; };

        foreach (EnemyManager enemy in targets)
        {
            if (enemy == null || !enemy.TryGetComponent(out EnemyFeedbackRelay relay)) continue;
            relay.OnExit += onExit;
            watched.Add(relay);
        }

        yield return new WaitForSeconds(window);

        foreach (EnemyFeedbackRelay relay in watched)
            if (relay != null) relay.OnExit -= onExit;

        if (kills < 2 || !CameraFeedback.IsOnScreen(stampAt, 0.05f)) yield break;
        FloatingNumbers.Say(stampAt, "x" + kills, 1.6f, Juice.Tokens.Colors.Sparkle);
        Juice.Sfx(SfxId.Stamp, 0, 0.8f);
    }

    // ---------- zones ----------

    /// <summary>Publishes this cast's area from now for its duration, so unit statuses and frost clocks can read it.</summary>
    protected SpellZone RegisterZone(float linger = 0f, float magnitude = 0f)
    {
        _zone = new SpellZone(Cast.Spell, Cast.Position, Cast.Range, Cast.CasterTeam, Time.time, Cast.Duration, linger, magnitude);
        SpellZoneRegistry.Add(_zone);
        return _zone;
    }

    // ---------- building ----------

    /// <summary>A renderer owned by this visual (gone with it), its longest side <paramref name="size"/> world units.</summary>
    protected SpriteRenderer Sprite(string name, Sprite sprite, Color color, float size, bool ground, int order, Transform parent = null)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent != null ? parent : transform, false);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingLayerName = ground ? GroundLayer : AirLayer;
        renderer.sortingOrder = order;
        go.transform.localScale = Vector3.one * (size / SpriteSize(sprite));
        return renderer;
    }

    private static Material _lineMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _lineMaterial = null;

    /// <summary>
    /// The pipeline's default sprite material, borrowed for line renderers (a strip, a bolt) so they sit in the same
    /// light as every sprite beside them. Read off a throwaway renderer rather than found by shader name, which a
    /// build can strip.
    /// </summary>
    protected static Material LineMaterial
    {
        get
        {
            if (_lineMaterial != null) return _lineMaterial;
            GameObject probe = new GameObject("LineMaterialProbe");
            _lineMaterial = probe.AddComponent<SpriteRenderer>().sharedMaterial;
            Object.Destroy(probe);
            return _lineMaterial;
        }
    }

    protected static float SpriteSize(Sprite sprite)
    {
        if (sprite == null) return 1f;
        Vector3 extent = sprite.bounds.size;
        return Mathf.Max(0.0001f, Mathf.Max(extent.x, extent.y));
    }

    /// <summary>The scale that gives <paramref name="renderer"/>'s sprite a longest side of <paramref name="size"/>.</summary>
    protected static Vector3 ScaleFor(SpriteRenderer renderer, float size) => Vector3.one * (size / SpriteSize(renderer.sprite));

    protected static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    /// <summary>
    /// A thin ring just outside the zone, radially filled from the top and emptying over the real (scaled) duration:
    /// a zone shows how long it has left. A world-space canvas of its own is the one way to draw a partial ring on
    /// the board without a custom material (the same as the first pass's zone clock).
    /// </summary>
    protected Image BuildClock(float radius, int order, float alpha = 0.85f)
    {
        GameObject go = new GameObject("ZoneClock", typeof(RectTransform), typeof(Canvas), typeof(Image));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);

        // World space first: a new canvas starts as an overlay, which drives its rect to the screen's size.
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingLayerName = GroundLayer;
        canvas.sortingOrder = order;

        float diameter = radius * 2f * 1.08f;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(diameter, diameter);
        rect.localPosition = Vector3.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        Image image = go.GetComponent<Image>();
        image.sprite = JuiceSprites.Outline;
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Radial360;
        image.fillOrigin = (int)Image.Origin360.Top;
        image.fillClockwise = true;
        image.fillAmount = 1f;
        image.color = WithAlpha(Juice.Tokens.Colors.Ink, alpha);
        image.raycastTarget = false;
        return image;
    }
}

/// <summary>Which visual draws which spell: the presentation twin of <see cref="SpellExecutorFactory"/>.</summary>
public static class SpellVisualFactory
{
    /// <summary>A fresh visual for <paramref name="spell"/>, or null for a spell that has none (it keeps its prefab).</summary>
    public static SpellVisual Create(SpellType spell)
    {
        GameObject go = new GameObject($"{spell}Fx");
        SpellVisual visual = spell switch
        {
            SpellType.Fireball => go.AddComponent<FireballVisual>(),
            SpellType.Lance => go.AddComponent<LanceVisual>(),
            SpellType.Ice => go.AddComponent<IceVisual>(),
            SpellType.Haste => go.AddComponent<HasteVisual>(),
            SpellType.Rage => go.AddComponent<RageVisual>(),
            SpellType.Rift => go.AddComponent<RiftVisual>(),
            SpellType.Ferrugem => go.AddComponent<FerrugemVisual>(),
            _ => null
        };

        if (visual == null) Object.Destroy(go);
        return visual;
    }
}
