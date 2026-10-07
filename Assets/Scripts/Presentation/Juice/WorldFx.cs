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

    private static float ScaleFor(float radius) => radius * 2f / SpriteSize;

    private static SpriteRenderer Take(Sprite sprite, Vector3 position, Color color, float radius)
    {
        Transform root = Root;
        if (root == null) return null;

        SpriteRenderer renderer = null;
        while (Free.Count > 0 && renderer == null) renderer = Free.Pop();

        if (renderer == null)
        {
            GameObject go = new GameObject("Fx");
            go.transform.SetParent(root, false);
            renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = SortingLayer;
            renderer.sortingOrder = SortingOrder;
        }

        Transform tr = renderer.transform;
        tr.DOKill();
        tr.SetPositionAndRotation(new Vector3(position.x, position.y, 0f), Quaternion.identity);
        tr.localScale = Vector3.one * ScaleFor(radius);
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
