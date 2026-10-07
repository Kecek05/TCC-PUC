using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The payoff of an upgrade or a won card: a white flash, a ring and a fan of rays in the card's rarity
/// colour, and sparkles thrown outward. Scaled by rarity, so a Common stays modest and a Legendary fills
/// the panel. Built from <see cref="JuiceSprites"/> under the anchor and destroyed when it is done.
/// </summary>
/// <remarks>Unscaled time and no raycast targets: it can play over a frozen tutorial and never eats a tap.</remarks>
public static class RarityBurst
{
    /// <summary>How big a rarity's burst is.</summary>
    public static float IntensityOf(CardRarityType rarity) => rarity switch
    {
        CardRarityType.Legendary => 1.35f,
        CardRarityType.Epic => 1.1f,
        CardRarityType.Rare => 0.9f,
        _ => 0.7f,
    };

    /// <param name="anchor">What bursts: the burst is centred on it and sized from it.</param>
    /// <param name="behind">Draw under the anchor's own children (a glow behind a card) rather than over them.</param>
    public static void Play(RectTransform anchor, Color color, float intensity = 1f, bool behind = false)
    {
        if (anchor == null) return;

        JuiceTokensSO t = Juice.Tokens;
        float reduce = Juice.ReduceMotion ? 0.6f : 1f;
        float size = Mathf.Max(anchor.rect.width, anchor.rect.height) * intensity;

        GameObject root = new GameObject("RarityBurst", typeof(RectTransform), typeof(LayoutElement));
        root.GetComponent<LayoutElement>().ignoreLayout = true;
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.SetParent(anchor, false);
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = Vector2.zero;
        rootRect.anchoredPosition = Vector2.zero; // centre-anchored: zero is the anchor's middle
        if (behind) rootRect.SetAsFirstSibling();
        else rootRect.SetAsLastSibling();

        Sequence s = DOTween.Sequence().SetUpdate(true).SetLink(root);

        // Rays: a slow fan turning a little as it fades.
        RectTransform fan = Child(rootRect, "Rays");
        int rays = 10;
        for (int i = 0; i < rays; i++)
        {
            Image ray = Part(fan, "Ray", JuiceSprites.Ray, color, new Vector2(size * 0.16f, size * 1.15f));
            ray.rectTransform.pivot = new Vector2(0.5f, 0f);
            ray.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i * 360f / rays);
            ray.color = new Color(color.r, color.g, color.b, 0.55f);
        }

        fan.localScale = Vector3.one * 0.3f;
        s.Join(fan.DOScale(1f, t.Durations.Pop).SetEase(t.Eases.Out));
        s.Join(fan.DOLocalRotate(new Vector3(0f, 0f, 30f * reduce), t.Durations.Celebrate * 0.6f).SetEase(t.Eases.Out));
        s.Insert(t.Durations.Pop, Fade(fan, 0f, t.Durations.Settle));

        // Ring: thrown outward in the rarity colour.
        Image ring = Part(rootRect, "Ring", JuiceSprites.Ring, color, Vector2.one * size);
        ring.rectTransform.localScale = Vector3.one * 0.3f;
        s.Join(ring.rectTransform.DOScale(1.9f * reduce, t.Durations.Settle).SetEase(t.Eases.Out));
        s.Join(ring.DOFade(0f, t.Durations.Settle).SetEase(t.Eases.In));

        // Flash: white, gone almost as soon as it is seen.
        Image flash = Part(rootRect, "Flash", JuiceSprites.Glow, Color.white, Vector2.one * size * 1.3f);
        flash.rectTransform.localScale = Vector3.one * 0.5f;
        flash.color = new Color(1f, 1f, 1f, t.Opacity.FlashPeak + 0.3f);
        s.Join(flash.rectTransform.DOScale(1.4f, t.Durations.Pop).SetEase(t.Eases.Out));
        s.Join(flash.DOFade(0f, t.Durations.Pop).SetEase(t.Eases.In));

        // Sparkles: more and further for the rarer card.
        int sparkles = Mathf.RoundToInt(Mathf.Lerp(5f, 14f, Mathf.InverseLerp(0.7f, 1.35f, intensity)) * reduce);
        for (int i = 0; i < sparkles; i++)
        {
            Image star = Part(rootRect, "Sparkle", JuiceSprites.Star, i % 2 == 0 ? t.Colors.Sparkle : Color.Lerp(color, Color.white, 0.5f),
                Vector2.one * size * Random.Range(0.12f, 0.22f));
            RectTransform r = star.rectTransform;
            float angle = (i / (float)sparkles + Random.Range(-0.04f, 0.04f)) * Mathf.PI * 2f;
            Vector2 to = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * size * Random.Range(0.55f, 1.05f);
            float delay = Random.Range(0f, 0.08f);
            float d = t.Durations.Settle + Random.Range(0f, 0.25f);

            r.localScale = Vector3.zero;
            s.Insert(delay, r.DOAnchorPos(to, d).SetEase(t.Eases.Out));
            s.Insert(delay, r.DOScale(1f, d * 0.3f).SetEase(t.Eases.Pop));
            s.Insert(delay + d * 0.45f, r.DOScale(0f, d * 0.55f).SetEase(t.Eases.In));
            s.Insert(delay, r.DOLocalRotate(new Vector3(0f, 0f, Random.Range(-120f, 120f)), d));
        }

        s.OnComplete(() => Object.Destroy(root));
    }

    /// <summary>A soft glow that breathes behind something, for a "this is ready" state. Returns it to stop later.</summary>
    public static Image Glow(RectTransform anchor, Color color, float scale = 1.35f)
    {
        Image glow = Part(anchor, "ReadyGlow", JuiceSprites.Glow, color, anchor.rect.size * scale);
        glow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        glow.rectTransform.anchoredPosition = Vector2.zero;
        glow.rectTransform.SetAsFirstSibling();
        return glow;
    }

    private static RectTransform Child(RectTransform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    private static Image Part(RectTransform parent, string name, Sprite sprite, Color color, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>Fades every Image under a parent together.</summary>
    private static Tween Fade(RectTransform parent, float to, float duration)
    {
        CanvasGroup group = parent.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        return group.DOFade(to, duration).SetEase(Juice.Tokens.Eases.In);
    }
}
