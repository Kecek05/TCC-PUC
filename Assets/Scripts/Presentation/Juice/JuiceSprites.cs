using UnityEngine;

/// <summary>
/// The few soft shapes every burst, flash and glow is made of, drawn once in code: a radial glow, a ring, a
/// ray and a four-point sparkle, all white so a tint colours them. Generated rather than imported, so the
/// effects that use them need no art and cannot lose a reference.
/// </summary>
/// <remarks>Built on first use and kept for the session. 128px is plenty: every use is soft-edged and scaled.</remarks>
public static class JuiceSprites
{
    private const int Size = 128;

    private static Sprite _glow, _ring, _ray, _star, _disc, _vignette;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _glow = _ring = _ray = _star = _disc = _vignette = null;

    /// <summary>A screen-edge vignette: clear in the middle, soft toward a rounded-rectangle edge.</summary>
    public static Sprite Vignette => _vignette != null ? _vignette : _vignette = Build("JuiceVignette", (x, y) =>
    {
        float d = Mathf.Pow(Mathf.Pow(Mathf.Abs(x), 4f) + Mathf.Pow(Mathf.Abs(y), 4f), 0.25f);
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.78f, 1.02f, d));
    });

    /// <summary>A radial glow, opaque at the centre and fading to nothing at the edge.</summary>
    public static Sprite Glow => _glow != null ? _glow : _glow = Build("JuiceGlow", (x, y) =>
    {
        float d = Mathf.Clamp01(Mathf.Sqrt(x * x + y * y));
        float a = 1f - d;
        return a * a;
    });

    /// <summary>A soft ring, its line about a tenth of its radius.</summary>
    public static Sprite Ring => _ring != null ? _ring : _ring = Build("JuiceRing", (x, y) =>
    {
        float d = Mathf.Sqrt(x * x + y * y);
        return Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.1f);
    });

    /// <summary>A light beam: narrow, brightest along its middle, fading toward its far end (the top).</summary>
    public static Sprite Ray => _ray != null ? _ray : _ray = Build("JuiceRay", (x, y) =>
    {
        float along = (y + 1f) * 0.5f;                 // 0 at the bottom (the origin), 1 at the tip
        float across = 1f - Mathf.Clamp01(Mathf.Abs(x) / Mathf.Lerp(0.9f, 0.15f, along));
        return across * across * (1f - along);
    }, new Vector2(0.5f, 0f));

    /// <summary>A four-point sparkle.</summary>
    public static Sprite Star => _star != null ? _star : _star = Build("JuiceStar", (x, y) =>
    {
        float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
        float arms = Mathf.Max(1f - (ax * 6f + ay), 1f - (ay * 6f + ax));
        float core = 1f - Mathf.Sqrt(x * x + y * y) * 2.4f;
        return Mathf.Clamp01(Mathf.Max(arms, core));
    });

    /// <summary>A solid disc with a soft rim.</summary>
    public static Sprite Disc => _disc != null ? _disc : _disc = Build("JuiceDisc", (x, y) =>
        Mathf.Clamp01((1f - Mathf.Sqrt(x * x + y * y)) * 20f));

    /// <summary>
    /// The spell aim's outline (SpellCast): a thin ring cut into 28 dashes. 256px, because it is drawn as large as
    /// a spell's whole radius.
    /// </summary>
    public static Sprite DashedRing => _dashedRing != null ? _dashedRing : _dashedRing = Build("JuiceDashedRing", (x, y) =>
    {
        float d = Mathf.Sqrt(x * x + y * y);
        float line = Mathf.Clamp01(1f - Mathf.Abs(d - 0.95f) / 0.035f);
        float turn = (Mathf.Atan2(y, x) / (Mathf.PI * 2f) + 1f) * 28f;
        float f = turn - Mathf.Floor(turn);
        float dash = Mathf.Clamp01((0.6f - f) * 20f) * Mathf.Clamp01(f * 20f);
        return line * dash;
    }, null, 256);

    /// <summary>
    /// A thin, crisp, unbroken ring at the edge of its square (256px): a zone's countdown, drawn as a radial fill.
    /// </summary>
    public static Sprite Outline => _outline != null ? _outline : _outline = Build("JuiceOutline", (x, y) =>
    {
        float d = Mathf.Sqrt(x * x + y * y);
        return Mathf.Clamp01(1f - Mathf.Abs(d - 0.95f) / 0.035f);
    }, null, 256);

    /// <summary>A rounded triangle pointing down: a pointer under a popup, or an alert pointing home.</summary>
    public static Sprite ArrowDown => _arrowDown != null ? _arrowDown : _arrowDown = Build("JuiceArrowDown", (x, y) =>
    {
        // Inside the triangle (-0.8, 0.5) (0.8, 0.5) (0, -0.6): under its top edge and within its two sides
        // (each side's distance divided by its slope's length), softened over a couple of pixels.
        float top = 0.5f - y;
        float side = (0.727f * (y + 0.6f) - Mathf.Abs(x)) / 1.236f;
        return Mathf.Clamp01(Mathf.Min(top, side) * 30f);
    });

    /// <summary>A check mark, for a badge saying "this works" (TowerPlacement's place-ok).</summary>
    public static Sprite Check => _check != null ? _check : _check = Build("JuiceCheck", (x, y) =>
    {
        float d = Mathf.Min(Segment(x, y, -0.5f, 0f, -0.12f, -0.38f), Segment(x, y, -0.12f, -0.38f, 0.52f, 0.36f));
        return Mathf.Clamp01((0.14f - d) * 20f);
    });

    /// <summary>A cross, for a badge saying "not here" (TowerPlacement's blocked spot).</summary>
    public static Sprite Cross => _cross != null ? _cross : _cross = Build("JuiceCross", (x, y) =>
    {
        float d = Mathf.Min(Segment(x, y, -0.42f, -0.42f, 0.42f, 0.42f), Segment(x, y, -0.42f, 0.42f, 0.42f, -0.42f));
        return Mathf.Clamp01((0.13f - d) * 20f);
    });

    /// <summary>
    /// A disc filled with diagonal hatching (256px): a blocked spot reads by its pattern, never by its hue alone.
    /// </summary>
    public static Sprite HatchedDisc => _hatchedDisc != null ? _hatchedDisc : _hatchedDisc = Build("JuiceHatchedDisc", (x, y) =>
    {
        float inside = Mathf.Clamp01((0.95f - Mathf.Sqrt(x * x + y * y)) * 60f);
        float f = Mathf.Repeat((x + y) * 5f, 1f);
        float stripe = Mathf.Clamp01((0.5f - Mathf.Abs(f - 0.25f) * 2f) * 8f);
        return inside * Mathf.Lerp(0.18f, 0.6f, stripe);
    }, null, 256);

    private static float Segment(float x, float y, float ax, float ay, float bx, float by)
    {
        float px = x - ax, py = y - ay, dx = bx - ax, dy = by - ay;
        float h = Mathf.Clamp01((px * dx + py * dy) / (dx * dx + dy * dy));
        float ex = px - dx * h, ey = py - dy * h;
        return Mathf.Sqrt(ex * ex + ey * ey);
    }

    /// <summary>Four corner marks: the SpellCast bracket around an enemy the aimed spell will hit.</summary>
    public static Sprite Bracket => _bracket != null ? _bracket : _bracket = Build("JuiceBracket", (x, y) =>
    {
        float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
        float edge = Mathf.Max(ax, ay);
        float band = Mathf.Clamp01((edge - 0.8f) * 12f) * Mathf.Clamp01((0.98f - edge) * 12f);
        float corner = Mathf.Clamp01((Mathf.Min(ax, ay) - 0.42f) * 12f);
        return band * corner;
    });

    /// <summary>
    /// The back of a card still to be revealed (RewardReveal): diagonal stripes in the nav and pill colours inside
    /// a rounded frame. Drawn in its own colours, not tinted.
    /// </summary>
    public static Sprite CardBack => _cardBack != null ? _cardBack : _cardBack = BuildCardBack();

    /// <summary>
    /// A dashed rounded-rectangle outline at an exact size in pixels, for an empty deck slot (DeckEquip). Dashes
    /// cannot be 9-sliced without stretching, so each size is drawn once and kept.
    /// </summary>
    public static Sprite DashedRoundRect(int width, int height, int radius, int dash = 12, int gap = 9, float thickness = 3f)
    {
        string key = $"{width}x{height}r{radius}d{dash}g{gap}t{thickness}";
        if (DashedRects.TryGetValue(key, out Sprite cached) && cached != null) return cached;

        Texture2D texture = NewTexture("JuiceDashedRect" + key, width, height);
        Color32[] pixels = new Color32[width * height];
        float perimeter = 2f * (width + height);

        for (int py = 0; py < height; py++)
        for (int px = 0; px < width; px++)
        {
            float x = px + 0.5f, y = py + 0.5f;
            float edge = InsideRoundRect(x, y, width, height, radius);
            float line = Mathf.Clamp01(1f - Mathf.Abs(edge - thickness) / (thickness * 0.75f));

            // Distance travelled along the outline (bottom, right, top, left), so dashes stay evenly spaced.
            float dl = x, dr = width - x, db = y, dt = height - y;
            float nearest = Mathf.Min(Mathf.Min(dl, dr), Mathf.Min(db, dt));
            float along = nearest == db ? x
                : nearest == dr ? width + y
                : nearest == dt ? width + height + (width - x)
                : 2f * width + height + (height - y);
            float phase = Mathf.Repeat(along, dash + gap);
            float on = phase < dash ? 1f : 0f;

            pixels[py * width + px] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(line * on * 255f));
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.DontSave;
        DashedRects[key] = sprite;
        return sprite;
    }

    /// <summary>
    /// A capsule, cut for slicing: a sliced Image draws a pill of any length with fully round ends (tags, chips).
    /// Two pixels wider than its ends: a sliced SpriteRenderer has nothing to stretch across a zero-width middle.
    /// </summary>
    public static Sprite Pill => _pill != null ? _pill : _pill = BuildRounded("JuicePill", 66, 32);

    /// <summary>A rounded rectangle with 24px corners, cut for slicing: tiles and sheets of any size keep them.</summary>
    public static Sprite Panel => _panel != null ? _panel : _panel = BuildRounded("JuicePanel", 64, 24);

    private static Sprite _dashedRing, _bracket, _cardBack, _pill, _panel, _outline, _check, _cross, _hatchedDisc, _arrowDown;
    private static readonly System.Collections.Generic.Dictionary<string, Sprite> DashedRects = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetMoreStatics()
    {
        _dashedRing = _bracket = _cardBack = _pill = _panel = _outline = _check = _cross = _hatchedDisc = _arrowDown = null;
        DashedRects.Clear();
    }

    private static Sprite BuildRounded(string name, int size, int radius)
    {
        Texture2D texture = NewTexture(name, size, size);
        Color32[] pixels = new Color32[size * size];
        for (int py = 0; py < size; py++)
        for (int px = 0; px < size; px++)
        {
            float edge = InsideRoundRect(px + 0.5f, py + 0.5f, size, size, radius);
            pixels[py * size + px] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(edge + 0.5f) * 255f));
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, Vector4.one * radius);
        sprite.name = name;
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    private static Sprite BuildCardBack()
    {
        const int w = 180, h = 250, radius = 18;
        JuiceTokensSO t = JuiceTokensSO.Instance;
        Color32 nav = t.Colors.Nav;
        Color32 pill = Color.Lerp(t.Colors.Nav, Color.black, 0.35f);
        Color32 frame = t.Colors.InkMuted;

        Texture2D texture = NewTexture("JuiceCardBack", w, h);
        Color32[] pixels = new Color32[w * h];
        for (int py = 0; py < h; py++)
        for (int px = 0; px < w; px++)
        {
            float edge = InsideRoundRect(px + 0.5f, py + 0.5f, w, h, radius);
            float alpha = Mathf.Clamp01(edge);

            Color32 c = ((px + py) / 14) % 2 == 0 ? nav : pill;
            if (edge < 7f) c = frame;
            c.a = (byte)Mathf.RoundToInt(alpha * 255f);
            pixels[py * w + px] = c;
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    /// <summary>How far a point lies inside a rounded rectangle's edge, in pixels (negative outside).</summary>
    private static float InsideRoundRect(float x, float y, float width, float height, float radius)
    {
        float cx = Mathf.Clamp(x, radius, width - radius);
        float cy = Mathf.Clamp(y, radius, height - radius);
        bool corner = Mathf.Abs(x - cx) > 0.001f && Mathf.Abs(y - cy) > 0.001f;
        if (corner) return radius - Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
        return Mathf.Min(Mathf.Min(x, width - x), Mathf.Min(y, height - y));
    }

    private static Texture2D NewTexture(string name, int width, int height) => new(width, height, TextureFormat.RGBA32, false)
    {
        name = name,
        wrapMode = TextureWrapMode.Clamp,
        filterMode = FilterMode.Bilinear,
        hideFlags = HideFlags.DontSave,
    };

    /// <summary>Draws an alpha mask from -1..1 coordinates into a white sprite.</summary>
    private static Sprite Build(string name, System.Func<float, float, float> alpha, Vector2? pivot = null, int size = Size)
    {
        Texture2D texture = NewTexture(name, size, size);

        Color32[] pixels = new Color32[size * size];
        for (int py = 0; py < size; py++)
        for (int px = 0; px < size; px++)
        {
            float x = (px + 0.5f) / size * 2f - 1f;
            float y = (py + 0.5f) / size * 2f - 1f;
            byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(x, y)) * 255f);
            pixels[py * size + px] = new Color32(255, 255, 255, a);
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), pivot ?? new Vector2(0.5f, 0.5f), 100f);
        sprite.name = name;
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }
}
