using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cosmetic, client-side buff-zone visual. Sizes a tiled SpriteRenderer the same way the drag-time
/// GhostSpellCard does (SpriteRenderer.size in world units), so the persistent zone matches the ghost
/// preview and the actual buff radius exactly. Removes itself after the duration. Purely visual — the
/// buff is applied server-side by HasteExecutor.
/// </summary>
public class CosmeticBuffZone : MonoBehaviour
{
    [SerializeField] private SpriteRenderer zoneRenderer;

    [Tooltip("The countdown ring's diameter as a multiple of the zone's: just outside its edge.")]
    [SerializeField] private float clockScale = 1.07f;

    public void Initialize(Vector2 position, float range, float duration)
    {
        transform.position = new Vector3(position.x, position.y, 0f);

        if (zoneRenderer == null) zoneRenderer = GetComponent<SpriteRenderer>();
        if (zoneRenderer != null)
            zoneRenderer.size = new Vector2(range * 2f, range * 2f);

        Present(range, duration);
        Destroy(gameObject, duration);
    }

    /// <summary>
    /// SpellCast zone: it blooms in from the middle (ease-pop) with a ring thrown to its edge and a whoosh; a thin
    /// ring around it counts its real duration down; then it fades out growing a little (dur-panel, ease-in) — it
    /// never just vanishes. Everything on scaled time, like the buff it shows.
    /// </summary>
    private void Present(float range, float duration)
    {
        if (zoneRenderer == null || duration <= 0f) return;

        JuiceTokensSO t = Juice.Tokens;
        Transform zone = zoneRenderer.transform;
        Vector3 rest = zone.localScale;
        Color color = zoneRenderer.color;

        WorldFx.Ring(transform.position, Color.Lerp(color, Color.white, 0.3f), range * 0.3f, range * 1.05f, t.Durations.Settle);
        if (CameraFeedback.IsOnScreen(transform.position)) Juice.Sfx(SfxId.Whoosh);

        float bloom = Mathf.Min(t.Durations.Pop, duration * 0.3f);
        float fade = Mathf.Min(t.Durations.Panel, duration * 0.2f);
        float fadeAt = Mathf.Max(bloom, duration - fade);

        // Built at the zone's resting size: measured mid-bloom it would come out 2.5 times too big.
        Image clock = BuildClock(range);

        zone.localScale = rest * 0.4f;
        Sequence s = DOTween.Sequence().SetLink(gameObject)
            .Append(zone.DOScale(rest, bloom).SetEase(t.Eases.Pop));

        // The clock: full at the cast, empty as the buff ends, at an even pace.
        if (clock != null)
        {
            s.Insert(0f, clock.DOFillAmount(0f, duration).SetEase(Ease.Linear));
            s.Insert(fadeAt, clock.DOFade(0f, fade).SetEase(t.Eases.In));
        }

        s.Insert(fadeAt, zoneRenderer.DOFade(0f, fade).SetEase(t.Eases.In));
        s.Insert(fadeAt, zone.DOScale(rest * 1.08f, fade).SetEase(t.Eases.In));
    }

    /// <summary>
    /// A thin ring just outside the zone, radially filled from the top, on a world-space canvas of its own: the one
    /// way to draw a partial ring on the board without a custom material.
    /// </summary>
    private Image BuildClock(float range)
    {
        Transform zone = zoneRenderer.transform;
        GameObject go = new GameObject("ZoneClock", typeof(RectTransform), typeof(Canvas), typeof(Image));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(zone, false);

        // World space first: a new canvas starts as an overlay, which drives its rect to the screen's size.
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingLayerID = zoneRenderer.sortingLayerID;
        canvas.sortingOrder = zoneRenderer.sortingOrder + 1;

        float diameter = range * 2f * clockScale / Mathf.Max(0.0001f, zone.lossyScale.x);
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
        Color ink = Juice.Tokens.Colors.Ink;
        ink.a = 0.85f;
        image.color = ink;
        image.raycastTarget = false;
        return image;
    }
}
