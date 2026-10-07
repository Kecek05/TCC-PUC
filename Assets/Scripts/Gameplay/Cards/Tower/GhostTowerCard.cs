using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

/// <summary>
/// The tower card's preview on the board (TowerPlacement, TowerLevelUp). Placing: the tower in the card's own tint,
/// its reach as a ring with a check badge, over dashed frames on every free slot; a slot answers when the ghost
/// snaps to it. Over a spot that will refuse it: a hatched red reach and a cross, wobbling. Over a tower it can
/// upgrade: the current and the next reach, and a chip saying what the level buys.
/// </summary>
/// <remarks>
/// Valid and blocked never differ by hue alone: a solid ring and a check against hatching and a cross. The extra
/// pieces are built in code beside the authored ones, so the scene only carries the chip's font.
/// </remarks>
public class GhostTowerCard : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private GameObject gfxObject;
    [SerializeField] private Transform rangeGfx;
    [Tooltip("Second ring shown only while hovering an upgradeable tower: the range it gains after upgrade.")]
    [SerializeField] private Transform nextRangeGfx;
    [SerializeField] private SpriteRenderer gfxSprite;

    [Title("Placement")]
    [Tooltip("The ghost's opacity in the card's tint.")]
    [SerializeField, Range(0f, 1f)] private float ghostAlpha = 0.43f;
    [Tooltip("The free-slot frames' colour while a tower card is held.")]
    [SerializeField] private Color slotColor = new(1f, 1f, 1f, 0.2f);
    [Tooltip("A slot frame's size as a share of the distance between neighbouring slots: small enough to leave a " +
             "clear gap between frames and to sit inside the placeable ground.")]
    [SerializeField, Range(0.3f, 1f)] private float slotScale = 0.6f;

    [Title("Upgrade Chip")]
    [Tooltip("The game's font, for the chip over a tower this card would upgrade.")]
    [SerializeField] private TMP_FontAsset chipFont;

    private Color _ghostColor = Color.white;
    private Tween _wobble;
    private bool _blocked;

    private SpriteRenderer _rangeFill;
    private SpriteRenderer _rangeRing;
    private SpriteRenderer _hatch;
    private SpriteRenderer _badge;
    private SpriteRenderer _badgeGlyph;
    private bool _built;

    private Transform _slotRoot;
    private readonly List<SpriteRenderer> _slots = new();
    private SpriteRenderer _flashedSlot;

    private Transform _chip;
    private SpriteRenderer _chipBack;
    private TextMeshPro _chipText;

    /// <summary>Where a 256px JuiceSprites ring's line sits from its centre, in world units at scale 1.</summary>
    private const float RingRadius = 1.28f * 0.95f;

    private void Awake()
    {
        if (gfxSprite != null) _ghostColor = gfxSprite.color;
        SetVisible(false);
    }

    private void OnDestroy()
    {
        _wobble?.Kill();
        if (gfxObject != null) gfxObject.transform.DOKill();
        if (_slotRoot != null) Destroy(_slotRoot.gameObject);
    }

    /// <summary>The held card's tint: the ghost is the tower it will place.</summary>
    public void SetTint(Color tint)
    {
        tint.a = ghostAlpha;
        _ghostColor = tint;
        if (!_blocked && gfxSprite != null) gfxSprite.color = tint;
    }

    /// <summary>
    /// TowerPlacement blocked state: the ghost over a spot that will refuse it, in the deny colour at
    /// ghost-blocked opacity, wobbling, its reach hatched in red under a cross badge.
    /// </summary>
    public void ShowBlocked(Vector2 position)
    {
        Build();
        HideChip();
        SetPosition(position);
        gfxObject.SetActive(true);
        rangeGfx.gameObject.SetActive(true);
        if (nextRangeGfx != null) nextRangeGfx.gameObject.SetActive(false);

        JuiceTokensSO t = Juice.Tokens;
        if (gfxSprite != null)
        {
            Color deny = t.Colors.Deny;
            deny.a = t.Opacity.GhostBlocked + 0.25f;
            gfxSprite.color = deny;
        }

        SetRangeStyle(blocked: true);

        if (_blocked) return;
        _blocked = true;

        _wobble?.Kill();
        if (!Juice.ReduceMotion)
            _wobble = gfxObject.transform.DOShakeRotation(t.Durations.Deny, new Vector3(0f, 0f, 10f), 14, 0f, true,
                ShakeRandomnessMode.Harmonic).SetUpdate(true);
    }

    /// <summary>The ghost snapped onto a slot: a quick pulse, the "this is where it goes" beat.</summary>
    public void PulseSnap()
    {
        if (gfxObject == null) return;
        JuiceFx.Punch(gfxObject.transform, Juice.Tokens.Scales.PunchSoft, Juice.Tokens.Durations.Pop);
    }

    private void ClearBlocked()
    {
        if (!_blocked) return;
        _blocked = false;
        _wobble?.Kill();
        _wobble = null;
        if (gfxObject != null) gfxObject.transform.localRotation = Quaternion.identity;
        if (gfxSprite != null) gfxSprite.color = _ghostColor;
        SetRangeStyle(blocked: false);
    }

    public void SetPosition(Vector2 position)
    {
        transform.SetPositionAndRotation(position, Quaternion.identity);
    }

    public void SetRange(float range)
    {
        rangeGfx.localScale = Vector3.one * (range * 2);
    }

    public void SetNextRange(float range)
    {
        if (nextRangeGfx != null) nextRangeGfx.localScale = Vector3.one * (range * 2);
    }

    public void SetVisible(bool visible)
    {
        Build();
        ClearBlocked();
        HideChip();
        SetRangeStyle(blocked: false);
        gfxObject.SetActive(visible);
        rangeGfx.gameObject.SetActive(visible);
        SetBadge(visible, ok: true);
        // The "next range" ring is upgrade-hover only — never part of the normal placement ghost.
        if (nextRangeGfx != null) nextRangeGfx.gameObject.SetActive(false);
    }

    /// <summary>
    /// Upgrade-hover preview: no tower sprite (the tower already exists), the base ring shows the tower's
    /// current range and the second ring shows the range it would have after this upgrade, under a chip naming
    /// the level and what it buys. At max level <paramref name="hasNext"/> is false and only the current ring is
    /// shown.
    /// </summary>
    public void ShowUpgradePreview(Vector2 position, float currentRange, float nextRange, bool hasNext,
        string chipTitle = null, string chipGain = null)
    {
        Build();
        ClearBlocked();
        SetPosition(position);
        gfxObject.SetActive(false);
        SetBadge(false, ok: true);

        rangeGfx.gameObject.SetActive(true);
        SetRangeStyle(blocked: false);
        SetRange(currentRange);

        if (nextRangeGfx != null)
        {
            nextRangeGfx.gameObject.SetActive(hasNext);
            if (hasNext) SetNextRange(nextRange);
        }

        if (hasNext && !string.IsNullOrEmpty(chipTitle)) ShowChip(chipTitle, chipGain);
        else HideChip();
    }

    public void SetSprite(Sprite sprite)
    {
        gfxSprite.sprite = sprite;
    }

    // ---------- Free slots ----------

    /// <summary>A dashed frame on every free slot while a tower card is held: where it can go.</summary>
    public void ShowSlots(IReadOnlyList<Vector2> positions)
    {
        HideSlots();
        if (positions == null || positions.Count == 0) return;

        if (_slotRoot == null) _slotRoot = new GameObject("[Juice] PlacementSlots").transform;

        // Sized from the board's grid, not from the held tower's art: ghost sprites differ in canvas and pixels per
        // unit from tower to tower, and sizing off one blew some frames up to two cells wide.
        float size = SlotSpacing(positions) * slotScale;
        Sprite frame = JuiceSprites.DashedRoundRect(64, 64, 14, 9, 6, 3f);

        for (int i = 0; i < positions.Count; i++)
        {
            SpriteRenderer slot = i < _slots.Count ? _slots[i] : NewSlot(frame);
            slot.transform.position = positions[i];
            slot.transform.localScale = Vector3.one * (size / 0.64f);
            slot.color = new Color(slotColor.r, slotColor.g, slotColor.b, 0f);
            slot.gameObject.SetActive(true);
            slot.DOKill();
            slot.DOFade(slotColor.a, Juice.Tokens.Durations.Select).SetUpdate(true).SetLink(slot.gameObject);
        }
    }

    /// <summary>The distance between the two closest slots: one cell of the board's grid.</summary>
    private static float SlotSpacing(IReadOnlyList<Vector2> positions)
    {
        float best = float.MaxValue;
        for (int i = 0; i < positions.Count; i++)
        for (int j = i + 1; j < positions.Count; j++)
        {
            float d = (positions[i] - positions[j]).sqrMagnitude;
            if (d > 0.0001f && d < best) best = d;
        }

        return best < float.MaxValue ? Mathf.Sqrt(best) : 0.75f;
    }

    public void HideSlots()
    {
        _flashedSlot = null;
        foreach (SpriteRenderer slot in _slots)
        {
            if (slot == null) continue;
            slot.DOKill();
            slot.transform.DOKill();
            slot.gameObject.SetActive(false);
        }
    }

    /// <summary>The slot the ghost just snapped to answers: its frame lights in the attention colour and pulses.</summary>
    public void FlashSlot(Vector2 position)
    {
        SpriteRenderer slot = null;
        float best = 0.25f;
        foreach (SpriteRenderer candidate in _slots)
        {
            if (candidate == null || !candidate.gameObject.activeSelf) continue;
            float d = ((Vector2)candidate.transform.position - position).sqrMagnitude;
            if (d < best)
            {
                best = d;
                slot = candidate;
            }
        }

        if (_flashedSlot != null && _flashedSlot != slot) _flashedSlot.DOColor(slotColor, Juice.Tokens.Durations.Select).SetUpdate(true);
        _flashedSlot = slot;
        if (slot == null) return;

        JuiceTokensSO t = Juice.Tokens;
        slot.DOKill();
        slot.color = t.Colors.Attention;
        Transform tr = slot.transform;
        tr.DOKill();
        Vector3 rest = tr.localScale;
        tr.DOPunchScale(rest * 0.25f, t.Durations.Pop, 6, 0.5f).SetUpdate(true).SetLink(slot.gameObject)
            .OnKill(() => tr.localScale = rest);
    }

    private SpriteRenderer NewSlot(Sprite frame)
    {
        GameObject go = new GameObject("Slot", typeof(SpriteRenderer));
        go.transform.SetParent(_slotRoot, false);
        SpriteRenderer slot = go.GetComponent<SpriteRenderer>();
        slot.sprite = frame;

        // On the ground: under the towers and the enemies, over the board.
        slot.sortingLayerName = "Enemy";
        slot.sortingOrder = -20;
        _slots.Add(slot);
        return slot;
    }

    // ---------- Range ring, hatching, badge ----------

    private void Build()
    {
        if (_built || rangeGfx == null) return;
        _built = true;

        _rangeFill = rangeGfx.GetComponent<SpriteRenderer>();
        int layer = _rangeFill != null ? _rangeFill.sortingLayerID : 0;
        int order = _rangeFill != null ? _rangeFill.sortingOrder : 0;

        // The ring and the hatching sit exactly on the reach: half the range sprite's drawn width at scale 1.
        float across = _rangeFill == null ? 1f
            : _rangeFill.drawMode == SpriteDrawMode.Simple ? _rangeFill.sprite.bounds.size.x : _rangeFill.size.x;
        float toReach = across * 0.5f / RingRadius;
        _rangeRing = Part("RangeRing", rangeGfx, JuiceSprites.Outline, layer, order + 2, toReach);
        _hatch = Part("RangeHatch", rangeGfx, JuiceSprites.HatchedDisc, layer, order, toReach);

        // The badge rides the ghost's top-right corner.
        Transform ghost = gfxObject.transform;
        float half = gfxSprite != null && gfxSprite.sprite != null ? gfxSprite.sprite.bounds.extents.x : 0.3f;
        _badge = Part("Badge", ghost, JuiceSprites.Disc, gfxSprite.sortingLayerID, gfxSprite.sortingOrder + 2, 0.24f / 1.28f);
        _badge.transform.localPosition = new Vector3(half * 0.85f, half * 0.85f, 0f);
        _badgeGlyph = Part("Glyph", _badge.transform, JuiceSprites.Check, gfxSprite.sortingLayerID, gfxSprite.sortingOrder + 3, 0.8f);

        SetRangeStyle(blocked: false);
    }

    /// <summary>Valid: a soft fill and a solid ring. Blocked: hatching and a dashed ring, in the deny colour.</summary>
    private void SetRangeStyle(bool blocked)
    {
        if (!_built) return;
        JuiceTokensSO t = Juice.Tokens;

        if (_rangeFill != null) _rangeFill.enabled = !blocked;
        if (_hatch != null)
        {
            _hatch.enabled = blocked;
            Color hatch = t.Colors.Deny;
            hatch.a = 0.7f;
            _hatch.color = hatch;
        }

        if (_rangeRing != null)
        {
            _rangeRing.sprite = blocked ? JuiceSprites.DashedRing : JuiceSprites.Outline;
            Color ring = blocked ? t.Colors.Deny : t.Colors.PlaceOk;
            ring.a = 0.9f;
            _rangeRing.color = ring;
        }

        SetBadge(_badge != null && _badge.gameObject.activeSelf || blocked, ok: !blocked);
    }

    private void SetBadge(bool visible, bool ok)
    {
        if (_badge == null) return;
        _badge.gameObject.SetActive(visible);
        if (!visible) return;

        JuiceTokensSO t = Juice.Tokens;
        _badge.color = ok ? t.Colors.PlaceOk : t.Colors.Deny;
        _badgeGlyph.sprite = ok ? JuiceSprites.Check : JuiceSprites.Cross;
        _badgeGlyph.color = ok ? t.Colors.Nav : t.Colors.Ink;
    }

    private static SpriteRenderer Part(string name, Transform parent, Sprite sprite, int layer, int order, float scale)
    {
        GameObject go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * scale;
        SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerID = layer;
        renderer.sortingOrder = order;
        return renderer;
    }

    // ---------- Upgrade chip ----------

    /// <summary>"LEVEL 2 +29% dmg" over the tower, on a dark pill: what dropping the card here buys.</summary>
    private void ShowChip(string title, string gain)
    {
        if (_chip == null) BuildChip();
        if (_chipText == null) return;

        _chipText.text = string.IsNullOrEmpty(gain)
            ? title
            : $"{title}  <color=#{ColorUtility.ToHtmlStringRGB(Juice.Tokens.Colors.Gain)}>{gain}</color>";
        // textBounds, not GetRenderedValues: on a freshly built world-space label the latter can come back as garbage.
        _chipText.ForceMeshUpdate();
        Vector2 size = _chipText.textBounds.size;
        float width = size.x + 0.36f;
        float height = Mathf.Max(0.42f, size.y + 0.16f);

        // A sliced SpriteRenderer, unlike a UI Image, does not shrink its borders to fit: drawn shorter than the
        // pill's two round ends it collapses to nothing. So it is drawn exactly as tall as the ends and scaled.
        const float ends = 0.64f;
        float k = height / ends;
        _chipBack.size = new Vector2(Mathf.Max(ends, width / k), ends);
        _chipBack.transform.localScale = Vector3.one * k;

        bool appearing = !_chip.gameObject.activeSelf;
        _chip.gameObject.SetActive(true);
        if (!appearing) return;

        _chip.DOKill();
        _chip.localScale = Vector3.one * 0.6f;
        _chip.DOScale(1f, Juice.Tokens.Durations.Pop).SetEase(Juice.Tokens.Eases.Pop).SetUpdate(true).SetLink(_chip.gameObject);
    }

    private void HideChip()
    {
        if (_chip != null) _chip.gameObject.SetActive(false);
    }

    private void BuildChip()
    {
        GameObject root = new GameObject("UpgradeChip");
        _chip = root.transform;
        _chip.SetParent(transform, false);
        _chip.localPosition = new Vector3(0f, 0.75f, 0f);

        _chipBack = new GameObject("Back", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
        _chipBack.transform.SetParent(_chip, false);
        _chipBack.sprite = JuiceSprites.Pill;
        _chipBack.drawMode = SpriteDrawMode.Sliced;
        _chipBack.color = Juice.Tokens.Colors.Sheet;
        _chipBack.sortingLayerName = "UI";
        _chipBack.sortingOrder = 90;

        GameObject label = new GameObject("Label", typeof(RectTransform));
        label.transform.SetParent(_chip, false);
        _chipText = label.AddComponent<TextMeshPro>();
        if (chipFont != null) _chipText.font = chipFont;
        _chipText.fontSize = 2.4f;
        _chipText.alignment = TextAlignmentOptions.Center;
        _chipText.textWrappingMode = TextWrappingModes.NoWrap;
        _chipText.color = Juice.Tokens.Colors.Ink;
        _chipText.rectTransform.sizeDelta = new Vector2(6f, 0.6f);
        _chipText.sortingLayerID = SortingLayer.NameToID("UI");
        _chipText.sortingOrder = 91;
        root.SetActive(false);
    }
}
