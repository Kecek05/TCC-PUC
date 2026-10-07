using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Canvas-space flourishes built from <see cref="JuiceSprites"/>: a ring thrown from an element (a slot answering
/// the card that landed on it), a puff of dust (a tile landing), and a full-screen colour flash (a spell landing,
/// a card won). Each one is a few Images and one DOTween sequence, destroyed when it ends.
/// </summary>
/// <remarks>Unscaled time and no raycast targets, like every UI answer in the Juice system.</remarks>
public static class UiFx
{
    /// <summary>A ring that grows out of <paramref name="anchor"/> and fades: "this is where it went".</summary>
    public static void Ring(RectTransform anchor, Color color, float fromSize, float toSize, float duration = -1f)
    {
        if (anchor == null) return;

        JuiceTokensSO t = Juice.Tokens;
        float d = duration > 0f ? duration : t.Durations.Settle;
        Image ring = Part(anchor, "Ring", JuiceSprites.Ring, color, Vector2.one * toSize);
        RectTransform rect = ring.rectTransform;
        rect.localScale = Vector3.one * (fromSize / Mathf.Max(1f, toSize));

        DOTween.Sequence().SetUpdate(true).SetLink(ring.gameObject)
            .Join(rect.DOScale(1f, d).SetEase(t.Eases.Out))
            .Join(ring.DOFade(0f, d).SetEase(t.Eases.In))
            .OnComplete(() => Object.Destroy(ring.gameObject));
    }

    /// <summary>
    /// A puff of dust from the bottom edge of <paramref name="anchor"/>, thrown up and out and falling away: the
    /// landing in TowerPlacement and RewardReveal, on a canvas.
    /// </summary>
    public static void Dust(RectTransform anchor, int count = 10, float spread = 1f)
    {
        if (anchor == null) return;

        JuiceTokensSO t = Juice.Tokens;
        if (Juice.ReduceMotion) count = Mathf.Max(3, count / 2);
        Rect r = anchor.rect;

        for (int i = 0; i < count; i++)
        {
            Image puff = Part(anchor, "Dust", JuiceSprites.Disc, t.Colors.Dust, Vector2.one * Random.Range(10f, 20f));
            RectTransform rect = puff.rectTransform;

            // Children sit on the anchor's centre, so the bottom edge is half a height down from zero.
            Vector2 start = new(Random.Range(-r.width * 0.4f, r.width * 0.4f), -r.height * 0.5f + 6f);
            rect.anchoredPosition = start;

            float side = Mathf.Sign(start.x == 0f ? Random.value - 0.5f : start.x);
            Vector2 to = start + new Vector2(side * Random.Range(30f, 90f) * spread, Random.Range(20f, 70f));
            float d = t.Durations.Settle + Random.Range(0f, 0.2f);

            DOTween.Sequence().SetUpdate(true).SetLink(puff.gameObject)
                .Join(rect.DOAnchorPos(to, d).SetEase(t.Eases.Out))
                .Join(rect.DOScale(0.4f, d).SetEase(t.Eases.In))
                .Join(puff.DOFade(0f, d).SetEase(t.Eases.In))
                .OnComplete(() => Object.Destroy(puff.gameObject));
        }
    }

    private static Image Part(RectTransform parent, string name, Sprite sprite, Color color, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
}

/// <summary>
/// The whole screen washing over in one colour for an instant: a spell landing in its blast colour, a won card in
/// its rarity. One overlay per scene, built on first use above the HUD and below the toast. Every flash asks
/// <see cref="JuiceCamera.TakeFlash"/> first, so the flash budget (and Reduce motion's one-a-second) holds.
/// </summary>
public static class ScreenFlash
{
    private const int SortingOrder = 24;
    private static Image _image;
    private static Tween _tween;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _image = null;
        _tween = null;
    }

    /// <param name="peakAlpha">How strong the wash is at its brightest (the design uses 0.3-0.35).</param>
    public static void Flash(Color color, float peakAlpha = 0.35f, float duration = -1f)
    {
        if (!Application.isPlaying || !JuiceCamera.TakeFlash()) return;

        Image image = Overlay;
        if (image == null) return;

        JuiceTokensSO t = Juice.Tokens;
        float d = duration > 0f ? duration : t.Durations.Pop;
        if (Juice.ReduceMotion) peakAlpha *= 0.6f;

        _tween?.Kill();
        image.gameObject.SetActive(true);
        color.a = peakAlpha;
        image.color = color;
        _tween = image.DOFade(0f, d).SetEase(t.Eases.Out).SetUpdate(true).SetLink(image.gameObject)
            .OnComplete(() => image.gameObject.SetActive(false));
    }

    private static Image Overlay
    {
        get
        {
            if (_image != null) return _image;

            GameObject root = new GameObject("[Juice] ScreenFlash", typeof(Canvas));
            SceneManager.MoveGameObjectToScene(root, SceneManager.GetActiveScene());
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            GameObject go = new GameObject("Flash", typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(root.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            _image = go.GetComponent<Image>();
            _image.raycastTarget = false;
            go.SetActive(false);
            return _image;
        }
    }
}
