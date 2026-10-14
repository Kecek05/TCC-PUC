using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The card page's tier strip (Card Art v2): a tower drawn at each of its in-match tiers on the mint ground, with
/// the stats that change from tier to tier printed under the tier they belong to. It ties "Damage Lvl 2" to the
/// tower the player will actually see on the board at tier 2.
/// </summary>
/// <remarks>
/// Built in code under the stats page, so the scene needs no new objects. Deliberately a view: it is handed the
/// sprites and the already-resolved <see cref="CardStatProgress"/> rows and knows nothing about levels or growth.
/// The thumbnails are the prefab's own level sprites in their own colours, stacked the way the prefab stacks
/// them, so the strip cannot drift from what the tower looks like.
/// </remarks>
public class TowerTierStrip : MonoBehaviour
{
    /// <summary>One stat that changes with the tier: its name without the "Lvl n", and one row per tier.</summary>
    public readonly struct TierStat
    {
        public readonly string Label;
        public readonly IReadOnlyList<CardStatProgress> Tiers;

        public TierStat(string label, IReadOnlyList<CardStatProgress> tiers)
        {
            Label = label;
            Tiers = tiers;
        }
    }

    private const float CaptionHeight = 30f;
    private const float TileHeight = 124f;
    private const float TileGap = 10f;
    private const float StatHeight = 62f;
    private const float ColumnGap = 24f;
    private const float ArtSize = 108f;

    private static readonly Color Mint = new(0.176f, 0.949f, 0.773f, 1f);
    private static readonly Color MintShade = new(0.153f, 0.698f, 0.576f, 1f);
    private static readonly string[] LevelNames = { "Level1", "Level2", "Level3" };

    private readonly List<Column> _columns = new();
    private RectTransform _rect;
    private TMP_Text _style;
    private string _gainHex;

    private class Column
    {
        public RectTransform Root;
        public TextMeshProUGUI Caption;
        public readonly List<Image> Art = new();
        public readonly List<TextMeshProUGUI> Values = new();
        public readonly List<TextMeshProUGUI> Labels = new();
    }

    /// <summary>Creates the strip under <paramref name="parent"/>, hidden. <paramref name="style"/> lends the game's outlined font.</summary>
    public static TowerTierStrip Create(RectTransform parent, TMP_Text style)
    {
        GameObject go = new GameObject("TierStrip", typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);

        TowerTierStrip strip = go.AddComponent<TowerTierStrip>();
        strip._rect = rect;
        strip._style = style;
        strip._gainHex = ColorUtility.ToHtmlStringRGB(Juice.Tokens.Colors.Upgrade);
        go.SetActive(false);
        return strip;
    }

    public RectTransform Rect => _rect;

    /// <summary>The strip's height at scale 1 for a tower reporting <paramref name="statCount"/> per-tier stats.</summary>
    public static float HeightFor(int statCount) => CaptionHeight + TileHeight + TileGap + statCount * StatHeight;

    public void Hide() => gameObject.SetActive(false);

    /// <summary>
    /// Draws the tower at every tier with its per-tier stats beneath. The caller places and scales the strip.
    /// </summary>
    /// <param name="width">The width the three columns share, at scale 1.</param>
    public void Show(TowerCardDataSO card, IReadOnlyList<TierStat> stats, float width)
    {
        int tiers = ResolveLevels(card, out Sprite[] sprites, out Color[] colors);
        if (tiers == 0)
        {
            Hide();
            return;
        }

        gameObject.SetActive(true);
        _rect.sizeDelta = new Vector2(width, HeightFor(stats.Count));

        float columnWidth = (width - ColumnGap * (tiers - 1)) / tiers;
        while (_columns.Count < tiers) _columns.Add(BuildColumn(_columns.Count));

        for (int i = 0; i < _columns.Count; i++)
        {
            Column column = _columns[i];
            bool used = i < tiers;
            column.Root.gameObject.SetActive(used);
            if (!used) continue;

            column.Root.sizeDelta = new Vector2(columnWidth, _rect.sizeDelta.y);
            column.Root.anchoredPosition = new Vector2(i * (columnWidth + ColumnGap), 0f);
            column.Caption.text = $"TIER {i + 1}";

            // A tier is every level sprite up to it, stacked: exactly what the prefab switches on.
            for (int level = 0; level < column.Art.Count; level++)
            {
                bool drawn = level <= i && level < sprites.Length && sprites[level] != null;
                column.Art[level].enabled = drawn;
                if (!drawn) continue;
                column.Art[level].sprite = sprites[level];
                column.Art[level].color = colors[level];
            }

            while (column.Values.Count < stats.Count) BuildStat(column);
            for (int s = 0; s < column.Values.Count; s++)
            {
                bool shown = s < stats.Count && i < stats[s].Tiers.Count;
                column.Values[s].gameObject.SetActive(shown);
                column.Labels[s].gameObject.SetActive(shown);
                if (!shown) continue;

                CardStatProgress stat = stats[s].Tiers[i];
                column.Values[s].text = stat.HasUpgrade
                    ? $"{stat.CurrentDisplay} <size=58%><color=#{_gainHex}>{stat.UpgradeDisplay}</color></size>"
                    : stat.CurrentDisplay;
                column.Labels[s].text = stats[s].Label;
            }
        }
    }

    /// <summary>A soft punch on every number: the strip's answer to an upgrade, after the values have moved.</summary>
    public void Pulse()
    {
        if (!gameObject.activeSelf) return;

        foreach (Column column in _columns)
            foreach (TextMeshProUGUI value in column.Values)
                if (value.gameObject.activeInHierarchy)
                    JuiceFx.Punch(value.transform, Juice.Tokens.Scales.PunchSoft);
    }

    /// <summary>The prefab's Level1..3 renderers: their sprites and the colours they are authored with.</summary>
    private static int ResolveLevels(TowerCardDataSO card, out Sprite[] sprites, out Color[] colors)
    {
        sprites = new Sprite[LevelNames.Length];
        colors = new Color[LevelNames.Length];
        if (card == null || card.TowerPrefab == null) return 0;

        TowerDataSO data = card.ResolveTowerData();
        int tiers = Mathf.Clamp(data != null ? data.MaxLevel : LevelNames.Length, 1, LevelNames.Length);

        int found = 0;
        foreach (SpriteRenderer renderer in card.TowerPrefab.GetComponentsInChildren<SpriteRenderer>(true))
        {
            int level = System.Array.IndexOf(LevelNames, renderer.gameObject.name);
            if (level < 0 || sprites[level] != null) continue;

            sprites[level] = renderer.sprite;
            colors[level] = renderer.color;
            found++;
        }

        return found == 0 ? 0 : tiers;
    }

    private Column BuildColumn(int index)
    {
        Column column = new();

        GameObject go = new GameObject($"Tier{index + 1}", typeof(RectTransform));
        column.Root = (RectTransform)go.transform;
        column.Root.SetParent(_rect, false);
        column.Root.anchorMin = column.Root.anchorMax = column.Root.pivot = new Vector2(0f, 1f);

        column.Caption = Label(column.Root, "Caption", 22f, Juice.Tokens.Colors.Ink);
        Place(column.Caption.rectTransform, 0f, CaptionHeight);

        // The mint ground with its darker edge, the way the map's tiles are drawn.
        GameObject tileGo = new GameObject("Tile", typeof(RectTransform), typeof(Image), typeof(Shadow));
        RectTransform tile = (RectTransform)tileGo.transform;
        tile.SetParent(column.Root, false);
        Place(tile, CaptionHeight, TileHeight - 6f);
        tile.offsetMin = new Vector2(0f, tile.offsetMin.y);
        tile.offsetMax = new Vector2(-5f, tile.offsetMax.y);

        Image ground = tileGo.GetComponent<Image>();
        ground.sprite = JuiceSprites.Panel;
        ground.type = Image.Type.Sliced;
        ground.pixelsPerUnitMultiplier = 1.4f;
        ground.color = Mint;
        ground.raycastTarget = false;

        Shadow edge = tileGo.GetComponent<Shadow>();
        edge.effectColor = MintShade;
        edge.effectDistance = new Vector2(5f, -6f);
        edge.useGraphicAlpha = true;

        for (int level = 0; level < LevelNames.Length; level++)
        {
            GameObject artGo = new GameObject(LevelNames[level], typeof(RectTransform), typeof(Image));
            RectTransform art = (RectTransform)artGo.transform;
            art.SetParent(tile, false);
            art.anchorMin = art.anchorMax = art.pivot = new Vector2(0.5f, 0.5f);
            art.sizeDelta = Vector2.one * ArtSize;

            Image image = artGo.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            column.Art.Add(image);
        }

        return column;
    }

    private void BuildStat(Column column)
    {
        int index = column.Values.Count;
        float top = CaptionHeight + TileHeight + TileGap + index * StatHeight;

        TextMeshProUGUI value = Label(column.Root, $"Value{index}", 36f, Juice.Tokens.Colors.Ink);
        value.richText = true;
        Place(value.rectTransform, top, 38f);

        TextMeshProUGUI label = Label(column.Root, $"Label{index}", 19f, Juice.Tokens.Colors.Ink);
        Place(label.rectTransform, top + 36f, 22f);

        column.Values.Add(value);
        column.Labels.Add(label);
    }

    private TextMeshProUGUI Label(RectTransform parent, string name, float size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (_style != null)
        {
            text.font = _style.font;
            text.fontSharedMaterial = _style.fontSharedMaterial;
        }

        text.fontSize = size;
        text.enableAutoSizing = false;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.color = color;
        return text;
    }

    /// <summary>Stretches a child across the column, <paramref name="top"/> below its top edge.</summary>
    private static void Place(RectTransform rect, float top, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(0f, -(top + height));
        rect.offsetMax = new Vector2(0f, -top);
    }
}
