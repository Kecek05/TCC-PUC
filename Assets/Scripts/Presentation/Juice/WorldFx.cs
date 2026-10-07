using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Board-space flourishes built from <see cref="JuiceSprites"/>: an expanding ring, a soft flash, sparks thrown
/// outward, and a <see cref="Blast"/> that combines them. For the moments that happen a few times a match (a
/// spell landing, a tower levelling, a base hit), not per hit: each one is a handful of pooled sprites and a
/// DOTween sequence.
/// </summary>
/// <remarks>
/// Pooled per scene under a hidden root that dies with the scene, so nothing leaks into the next match. Drawn
/// on the <c>UI</c> sorting layer below the floating numbers, scaled time: it slows with the end cutscene like
/// the moment it decorates.
/// </remarks>
public static class WorldFx
{
    private const string SortingLayer = "UI";
    private const int SortingOrder = 40;

    /// <summary>World size of a <see cref="JuiceSprites"/> sprite at scale 1 (128 px at 100 px per unit).</summary>
    private const float SpriteSize = 1.28f;

    private static Transform _root;
    private static readonly Stack<SpriteRenderer> Free = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _root = null;
        Free.Clear();
    }

    /// <summary>A ring thrown outward from <paramref name="fromRadius"/> to <paramref name="toRadius"/>, fading.</summary>
    public static void Ring(Vector3 position, Color color, float fromRadius, float toRadius, float duration = -1f)
    {
        JuiceTokensSO t = Juice.Tokens;
        float d = duration > 0f ? duration : t.Durations.Settle;
        SpriteRenderer ring = Take(JuiceSprites.Ring, position, color, fromRadius);
        if (ring == null) return;

        Transform tr = ring.transform;
        DOTween.Sequence().SetLink(ring.gameObject)
            .Join(tr.DOScale(ScaleFor(toRadius), d).SetEase(t.Eases.Out))
            .Join(ring.DOFade(0f, d).SetEase(t.Eases.In))
            .OnComplete(() => Give(ring));
    }

    /// <summary>A soft flash that swells a little and is gone almost at once.</summary>
    public static void Flash(Vector3 position, Color color, float radius, float duration = -1f)
    {
        JuiceTokensSO t = Juice.Tokens;
        float d = duration > 0f ? duration : t.Durations.Pop;
        if (!JuiceCamera.TakeFlash()) return;

        SpriteRenderer flash = Take(JuiceSprites.Glow, position, color, radius * 0.7f);
        if (flash == null) return;

        Transform tr = flash.transform;
        DOTween.Sequence().SetLink(flash.gameObject)
            .Join(tr.DOScale(ScaleFor(radius * 1.25f), d).SetEase(t.Eases.Out))
            .Join(flash.DOFade(0f, d).SetEase(t.Eases.In))
            .OnComplete(() => Give(flash));
    }

    /// <summary>Sparks thrown out from a point, each spinning and shrinking away.</summary>
    public static void Sparks(Vector3 position, Color color, int count, float radius, float size = 0.22f)
    {
        JuiceTokensSO t = Juice.Tokens;
        if (Juice.ReduceMotion) count = Mathf.Max(1, count / 2);

        for (int i = 0; i < count; i++)
        {
            SpriteRenderer spark = Take(JuiceSprites.Star, position, color, size * Random.Range(0.7f, 1.2f));
            if (spark == null) return;

            Transform tr = spark.transform;
            float angle = (i / (float)count + Random.Range(-0.05f, 0.05f)) * Mathf.PI * 2f;
            Vector3 to = position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius * Random.Range(0.6f, 1.1f);
            float d = t.Durations.Settle + Random.Range(0f, 0.2f);
            Vector3 full = tr.localScale;
            tr.localScale = full * 0.3f;

            DOTween.Sequence().SetLink(spark.gameObject)
                .Join(tr.DOMove(to, d).SetEase(t.Eases.Out))
                .Join(tr.DOScale(full, d * 0.3f).SetEase(t.Eases.Pop))
                .Join(tr.DORotate(new Vector3(0f, 0f, Random.Range(-160f, 160f)), d))
                .Insert(d * 0.45f, tr.DOScale(0f, d * 0.55f).SetEase(t.Eases.In))
                .OnComplete(() => Give(spark));
        }
    }

    /// <summary>
    /// An explosion sized to <paramref name="radius"/>: a white-hot flash, a ring in the blast colour to the edge
    /// of the area it hit, and sparks. The SpellCast recipe's landing, without the debris.
    /// </summary>
    public static void Blast(Vector3 position, float radius, Color color)
    {
        Flash(position, Color.Lerp(color, Color.white, 0.6f), radius * 0.9f);
        Ring(position, color, radius * 0.25f, radius * 1.05f);
        Sparks(position, Color.Lerp(color, Juice.Tokens.Colors.Sparkle, 0.5f), 8, radius * 0.9f, Mathf.Clamp(radius * 0.18f, 0.12f, 0.35f));
    }

    /// <summary>
    /// Debris thrown up and out of a landing, arcing back down as it spins and fades: the heavy half of a blast
    /// (SpellCast's impact), in the blast colour, dust and sparkle.
    /// </summary>
    public static void Debris(Vector3 position, float radius, Color color, int count = 14)
    {
        JuiceTokensSO t = Juice.Tokens;
        if (Juice.ReduceMotion) count = Mathf.Max(4, count / 2);
        Color[] palette = { color, t.Colors.Dust, Color.Lerp(color, t.Colors.Sparkle, 0.5f) };
        float reach = Mathf.Clamp(radius, 0.5f, 1.8f);

        for (int i = 0; i < count; i++)
        {
            bool star = i % 3 == 2;
            float size = Random.Range(0.05f, 0.1f) * reach;
            SpriteRenderer bit = Take(star ? JuiceSprites.Star : JuiceSprites.Disc, position, palette[i % palette.Length], size);
            if (bit == null) return;

            Transform tr = bit.transform;
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 to = position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * 0.6f, 0f) * reach * Random.Range(0.5f, 1.15f);
            float d = Random.Range(0.45f, 0.75f);

            DOTween.Sequence().SetLink(bit.gameObject)
                .Join(tr.DOJump(to, reach * Random.Range(0.35f, 0.7f), 1, d).SetEase(Ease.Linear))
                .Join(tr.DORotate(new Vector3(0f, 0f, Random.Range(-240f, 240f)), d, RotateMode.FastBeyond360))
                .Insert(d * 0.6f, bit.DOFade(0f, d * 0.4f))
                .OnComplete(() => Give(bit));
        }
    }

    /// <summary>
    /// Sprites thrown out of a point and spun away, like <see cref="Sparks"/> with any sprite: a spell's fire
    /// puffs, feathers or shards. With <paramref name="pointOutward"/> each one turns its top toward where it flies
    /// (a flame tongue licking outward).
    /// </summary>
    public static void Burst(Sprite sprite, Vector3 position, Color color, int count, float radius, float size,
        bool pointOutward = false, float duration = -1f)
    {
        JuiceTokensSO t = Juice.Tokens;
        if (sprite == null) sprite = JuiceSprites.Star;
        if (Juice.ReduceMotion) count = Mathf.Max(1, count / 2);
        float baseDuration = duration > 0f ? duration : t.Durations.Settle;

        for (int i = 0; i < count; i++)
        {
            float angle = (i / (float)count + Random.Range(-0.05f, 0.05f)) * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            SpriteRenderer bit = TakeSized(sprite, position, color, size * Random.Range(0.8f, 1.2f), SortingLayer, SortingOrder);
            if (bit == null) return;

            Transform tr = bit.transform;
            float facing = pointOutward ? angle * Mathf.Rad2Deg - 90f : Random.Range(0f, 360f);
            tr.rotation = Quaternion.Euler(0f, 0f, facing);
            Vector3 full = tr.localScale;
            tr.localScale = full * 0.3f;
            float d = baseDuration + Random.Range(0f, 0.15f);
            float spin = pointOutward ? Random.Range(-25f, 25f) : Random.Range(-180f, 180f);

            DOTween.Sequence().SetLink(bit.gameObject)
                .Join(tr.DOMove(position + dir * radius * Random.Range(0.7f, 1.1f), d).SetEase(t.Eases.Out))
                .Join(tr.DOScale(full, d * 0.3f).SetEase(t.Eases.Pop))
                .Join(tr.DORotate(new Vector3(0f, 0f, facing + spin), d))
                .Insert(d * 0.45f, tr.DOScale(0f, d * 0.55f).SetEase(t.Eases.In))
                .Insert(d * 0.45f, bit.DOFade(0f, d * 0.55f))
                .OnComplete(() => Give(bit));
        }
    }

    /// <summary>One soft puff drifting and swelling as it fades: smoke after a blast, frost mist, an acid hiss.</summary>
    public static void Puff(Sprite sprite, Vector3 position, Color color, float size, Vector3 drift, float duration)
    {
        if (sprite == null) sprite = JuiceSprites.Glow;
        SpriteRenderer puff = TakeSized(sprite, position, color, size * 0.6f, SortingLayer, SortingOrder - 2);
        if (puff == null) return;

        Transform tr = puff.transform;
        tr.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        Color clear = color;
        clear.a = 0f;
        puff.color = clear;
        Vector3 to = tr.localScale / 0.6f * 1.25f;

        DOTween.Sequence().SetLink(puff.gameObject)
            .Join(puff.DOFade(color.a, duration * 0.15f))
            .Join(tr.DOMove(position + drift, duration).SetEase(Juice.Tokens.Eases.Out))
            .Join(tr.DOScale(to, duration).SetEase(Juice.Tokens.Eases.Out))
            .Join(tr.DORotate(new Vector3(0f, 0f, tr.eulerAngles.z + Random.Range(-40f, 40f)), duration))
            .Insert(duration * 0.35f, puff.DOFade(0f, duration * 0.65f).SetEase(Juice.Tokens.Eases.In))
            .OnComplete(() => Give(puff));
    }

    /// <summary>
    /// A mark left on the ground that holds, then fades: a scorch, a frosted patch. Drawn on the enemies' layer
    /// beneath their bodies, so what walks over it stays readable.
    /// </summary>
    public static void Decal(Sprite sprite, Vector3 position, Color color, float size, float hold, float fade, float rotation = 0f)
    {
        if (sprite == null) sprite = JuiceSprites.Glow;
        SpriteRenderer decal = TakeSized(sprite, position, color, size, GroundLayer, GroundOrder);
        if (decal == null) return;

        Transform tr = decal.transform;
        tr.rotation = Quaternion.Euler(0f, 0f, rotation);
        Vector3 full = tr.localScale;
        tr.localScale = full * 0.6f;

        DOTween.Sequence().SetLink(decal.gameObject)
            .Join(tr.DOScale(full * 1.05f, Juice.Tokens.Durations.Select).SetEase(Juice.Tokens.Eases.Out))
            .Append(tr.DOScale(full, Juice.Tokens.Durations.Select))
            .AppendInterval(Mathf.Max(0f, hold))
            .Append(decal.DOFade(0f, fade).SetEase(Juice.Tokens.Eases.In))
            .OnComplete(() => Give(decal));
    }

    /// <summary>Debris built from sprites (ice shards, rust flakes), arcing out and spinning down like <see cref="Debris(Vector3, float, Color, int)"/>.</summary>
    public static void Debris(Sprite[] sprites, Vector3 position, float radius, Color color, int count, float size)
    {
        if (sprites == null || sprites.Length == 0) { Debris(position, radius, color, count); return; }
        if (Juice.ReduceMotion) count = Mathf.Max(3, count / 2);
        float reach = Mathf.Clamp(radius, 0.3f, 1.8f);

        for (int i = 0; i < count; i++)
        {
            Sprite sprite = sprites[i % sprites.Length] != null ? sprites[i % sprites.Length] : JuiceSprites.Disc;
            SpriteRenderer bit = TakeSized(sprite, position, color, size * Random.Range(0.7f, 1.2f), SortingLayer, SortingOrder);
            if (bit == null) return;

            Transform tr = bit.transform;
            tr.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 to = position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * 0.6f, 0f) * reach * Random.Range(0.5f, 1.15f);
            float d = Random.Range(0.45f, 0.75f);

            DOTween.Sequence().SetLink(bit.gameObject)
                .Join(tr.DOJump(to, reach * Random.Range(0.35f, 0.7f), 1, d).SetEase(Ease.Linear))
                .Join(tr.DORotate(new Vector3(0f, 0f, Random.Range(-300f, 300f)), d, RotateMode.FastBeyond360))
                .Insert(d * 0.6f, bit.DOFade(0f, d * 0.4f))
                .OnComplete(() => Give(bit));
        }
    }

    /// <summary>
    /// A streak of light from <paramref name="from"/> to <paramref name="to"/>, brightest at <paramref name="to"/>,
    /// fading out: the trail a falling spell leaves, the line a spear slams down along.
    /// </summary>
    public static void Streak(Vector3 from, Vector3 to, Color color, float width, float duration)
    {
        SpriteRenderer ray = TakeSized(JuiceSprites.Ray, to, color, 1f, SortingLayer, SortingOrder - 1);
        if (ray == null) return;

        Vector3 along = from - to;
        float length = along.magnitude;
        if (length < 0.01f) { Give(ray); return; }

        Transform tr = ray.transform;
        tr.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg - 90f);
        tr.localScale = new Vector3(width / SpriteSize, length / SpriteSize, 1f);

        DOTween.Sequence().SetLink(ray.gameObject)
            .Join(ray.DOFade(0f, duration).SetEase(Juice.Tokens.Eases.In))
            .Join(tr.DOScaleX(width * 0.4f / SpriteSize, duration))
            .OnComplete(() => Give(ray));
    }

    /// <summary>A mote that rises and fades: an ember off a meteor, a light mote off Lance's scar, a feather.</summary>
    public static void Mote(Vector3 position, Color color, float size, Vector3 drift, float duration, Sprite sprite = null)
    {
        SpriteRenderer mote = TakeSized(sprite != null ? sprite : JuiceSprites.Glow, position, color, size, SortingLayer, SortingOrder - 1);
        if (mote == null) return;

        Transform tr = mote.transform;
        tr.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        DOTween.Sequence().SetLink(mote.gameObject)
            .Join(tr.DOMove(position + drift, duration).SetEase(Juice.Tokens.Eases.Out))
            .Join(tr.DORotate(new Vector3(0f, 0f, tr.eulerAngles.z + Random.Range(-120f, 120f)), duration))
            .Join(tr.DOScale(tr.localScale * 0.3f, duration).SetEase(Juice.Tokens.Eases.In))
            .Insert(duration * 0.3f, mote.DOFade(0f, duration * 0.7f))
            .OnComplete(() => Give(mote));
    }

    /// <summary>The enemies' layer, under their bodies (order 5) and over the board: where spells mark the ground.</summary>
    public const string GroundLayer = "Enemy";
    public const int GroundOrder = -12;

    private static float ScaleFor(float radius) => radius * 2f / SpriteSize;

    private static SpriteRenderer Take(Sprite sprite, Vector3 position, Color color, float radius)
    {
        SpriteRenderer renderer = TakeSized(sprite, position, color, 1f, SortingLayer, SortingOrder);
        if (renderer != null) renderer.transform.localScale = Vector3.one * ScaleFor(radius);
        return renderer;
    }

    /// <summary>
    /// A pooled renderer showing <paramref name="sprite"/> with its longest side <paramref name="size"/> world units,
    /// whatever its pixels per unit. The layer is set on every take: the pool is shared by air and ground effects.
    /// </summary>
    private static SpriteRenderer TakeSized(Sprite sprite, Vector3 position, Color color, float size, string layer, int order)
    {
        Transform root = Root;
        if (root == null || sprite == null) return null;

        SpriteRenderer renderer = null;
        while (Free.Count > 0 && renderer == null) renderer = Free.Pop();

        if (renderer == null)
        {
            GameObject go = new GameObject("Fx");
            go.transform.SetParent(root, false);
            renderer = go.AddComponent<SpriteRenderer>();
        }

        renderer.sortingLayerName = layer;
        renderer.sortingOrder = order;

        Transform tr = renderer.transform;
        tr.DOKill();
        renderer.DOKill();
        Vector3 extent = sprite.bounds.size;
        float longest = Mathf.Max(0.0001f, Mathf.Max(extent.x, extent.y));
        tr.SetPositionAndRotation(new Vector3(position.x, position.y, 0f), Quaternion.identity);
        tr.localScale = Vector3.one * (size / longest);
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.gameObject.SetActive(true);
        return renderer;
    }

    private static void Give(SpriteRenderer renderer)
    {
        if (renderer == null) return;
        renderer.gameObject.SetActive(false);
        Free.Push(renderer);
    }

    private static Transform Root
    {
        get
        {
            if (_root != null) return _root;
            if (!Application.isPlaying) return null;

            // A new scene: the old root went with the last one, and so did every pooled sprite under it.
            Free.Clear();
            GameObject go = new GameObject("[Juice] WorldFx");
            SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
            _root = go.transform;
            return _root;
        }
    }
}
