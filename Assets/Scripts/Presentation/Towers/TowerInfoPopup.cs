using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The card a tapped tower shows above itself mid-match: its name and placement tier, then the numbers that matter
/// for what it is. An attacker leads with its DPS, then damage and hits per second; a support tower shows its own
/// effect (slow, armour break, aura, hold, mana, troops sent) and how often it acts; every tower shows its reach,
/// and a haste or a freeze on it right now. It follows the tower as the camera moves and refreshes while open, so
/// a level-up or a buff shows in its numbers at once.
/// </summary>
/// <remarks>
/// Built in code on its own overlay canvas with no raycaster: it is information, never a target, so a tap on the
/// board under it still reaches the board. The numbers come from the tower's data at its tier, scaled by its card
/// level and the live buffs the host's tower combat holds; a joining client reads the authored numbers instead.
/// Opened and closed by <see cref="ClientTowerRangeGFX"/>, which owns the selection.
/// </remarks>
public class TowerInfoPopup : MonoBehaviour
{
    private const int SortingOrder = 4;
    private const float RefreshSeconds = 0.25f;
    private const float Width = 400f;
    private const float Padding = 18f;
    private const float TitleHeight = 58f;
    private const float RowHeight = 46f;
    private const float PointerSize = 30f;

    private struct Line
    {
        public string Label;
        public string Value;
        public Color Color;
    }

    private Canvas _canvas;
    private RectTransform _root;
    private RectTransform _panel;
    private CanvasGroup _group;
    private RectTransform _pointer;
    private TextMeshProUGUI _title;
    private Image _tierPill;
    private TextMeshProUGUI _tier;
    private readonly List<TextMeshProUGUI> _labels = new();
    private readonly List<TextMeshProUGUI> _values = new();
    private readonly List<Line> _lines = new();
    private TMP_FontAsset _font;

    private TowerManager _tower;
    private float _nextRefresh;
    private Sequence _show;
    private bool _open;

    private static readonly Vector3[] Corners = new Vector3[4];

    public static TowerInfoPopup Create()
    {
        GameObject go = new GameObject("[Juice] TowerInfo");
        SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
        TowerInfoPopup popup = go.AddComponent<TowerInfoPopup>();
        popup.Build();
        return popup;
    }

    /// <summary>Opens over <paramref name="tower"/>, or moves there from the tower it was open on.</summary>
    public void Show(TowerManager tower)
    {
        if (tower == null)
        {
            Hide();
            return;
        }

        bool moving = _open && _tower != tower;
        _tower = tower;
        _open = true;
        Refresh();
        Place();

        JuiceTokensSO t = Juice.Tokens;
        _show?.Kill();
        _panel.gameObject.SetActive(true);
        _group.alpha = moving ? _group.alpha : 0f;
        _panel.localScale = Vector3.one * (Juice.ReduceMotion ? 1f : t.Scales.PanelFrom);
        _show = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .Join(_group.DOFade(1f, t.Durations.Select))
            .Join(_panel.DOScale(1f, t.Durations.Pop).SetEase(t.Eases.Pop));
    }

    /// <summary>Closes quickly and quietly, as every popup does.</summary>
    public void Hide()
    {
        if (!_open) return;
        _open = false;
        _tower = null;

        JuiceTokensSO t = Juice.Tokens;
        _show?.Kill();
        _show = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .Join(_group.DOFade(0f, t.Durations.Select).SetEase(t.Eases.In))
            .Join(_panel.DOScale(Juice.ReduceMotion ? 1f : 0.92f, t.Durations.Select).SetEase(t.Eases.In))
            .OnComplete(() => _panel.gameObject.SetActive(false));
    }

    private void LateUpdate()
    {
        if (!_open) return;

        // The tower was sold, destroyed, or the match ended under it.
        if (_tower == null)
        {
            Hide();
            return;
        }

        if (Time.unscaledTime >= _nextRefresh) Refresh();
        Place();
    }

    private void OnDestroy() => _show?.Kill();

    // ---------- Numbers ----------

    private void Refresh()
    {
        _nextRefresh = Time.unscaledTime + RefreshSeconds;
        TowerDataSO data = _tower.Data;
        if (data == null) return;

        BaseServerTowerCombat combat = _tower.ServerTowerCombat;
        int tier = combat != null ? Mathf.Clamp(combat.TowerLevel.Value, 1, data.MaxLevel) : 1;
        bool live = combat != null && combat.IsServer;
        CardLevelScale scale = live ? combat.CardScale : CardLevelScale.One;
        float speedBuff = live ? combat.AttackSpeedBuffPercent : 0f;
        float damageBuff = live ? combat.DamageBuffPercent : 0f;

        JuiceTokensSO t = Juice.Tokens;
        _lines.Clear();

        float cooldown = data.GetShootCooldownByLevel(tier) / Mathf.Max(0.0001f, scale.AttackSpeed) / (1f + speedBuff);
        float rate = cooldown > 0f ? 1f / cooldown : 0f;
        float damage = data.GetDamageByLevel(tier) * scale.Damage * (1f + damageBuff);

        if (damage > 0f)
        {
            AddLine("DPS", (damage * rate).ToString("0.#"), t.Colors.Attention);
            AddLine("Damage", damage.ToString("0.#"));
            AddLine("Hits per second", rate.ToString("0.00"));
        }

        switch (data)
        {
            case SlowTowerDataSO slow:
                AddLine("Slow", Percent(slow.GetSlowPercentByLevel(tier) * scale.EffectBonus));
                break;
            case ResistTowerDataSO resist:
                AddLine("Armor break", Percent(Mathf.Clamp01(resist.GetResistClearPercentByLevel(tier) * scale.EffectBonus)));
                break;
            case AuraTowerDataSO aura:
                AddLine("Ally damage", "+" + Percent(aura.GetDamageBonusByLevel(tier) * scale.EffectBonus));
                AddLine("Ally attack speed", "+" + Percent(aura.GetAttackSpeedBonusByLevel(tier) * scale.EffectBonus));
                break;
            case AnchorTowerDataSO anchor:
                AddLine("Hold", (anchor.GetHoldDurationByLevel(tier) * scale.Duration).ToString("0.0") + "s");
                AddLine("Every", cooldown.ToString("0.#") + "s");
                break;
            case ManaTowerDataSO mana:
                AddLine("Mana", "+" + (mana.GetManaPerTickByLevel(tier) * scale.EffectBonus).ToString("0.##"), t.Colors.Mana);
                AddLine("Every", cooldown.ToString("0.#") + "s");
                break;
            case MirrorTowerDataSO mirror:
                AddLine("Troops sent", mirror.GetSendCountByLevel(tier).ToString());
                AddLine("Every", cooldown.ToString("0.#") + "s");
                break;
        }

        AddLine("Range", (data.GetRangeByLevel(tier) * scale.Range).ToString("0.##"));

        if (combat != null && combat.IsFrozen.Value) AddLine("Status", "Frozen", t.Colors.Ice);
        else if (combat != null && combat.IsHasted.Value) AddLine("Status", "Hasted", t.Colors.Hasted);

        Draw(data, tier);
    }

    private void AddLine(string label, string value, Color? color = null) =>
        _lines.Add(new Line { Label = label, Value = value, Color = color ?? Juice.Tokens.Colors.Ink });

    private static string Percent(float fraction) => Mathf.RoundToInt(fraction * 100f) + "%";

    private void Draw(TowerDataSO data, int tier)
    {
        JuiceTokensSO t = Juice.Tokens;
        _title.text = data.TowerType.ToString();
        _tier.text = $"LVL {tier}";

        // The tier pill in the tower's own colour, like its level pips.
        ClientTowerGFX gfx = _tower.GetComponentInChildren<ClientTowerGFX>(true);
        Color tint = gfx != null ? gfx.Tint : t.Colors.Ink;
        tint.a = 1f;
        _tierPill.color = tint;

        while (_labels.Count < _lines.Count) AddRow();
        for (int i = 0; i < _labels.Count; i++)
        {
            bool used = i < _lines.Count;
            _labels[i].gameObject.SetActive(used);
            _values[i].gameObject.SetActive(used);
            if (!used) continue;

            _labels[i].text = _lines[i].Label;
            _values[i].text = _lines[i].Value;
            _values[i].color = _lines[i].Color;
        }

        _panel.sizeDelta = new Vector2(Width, Padding * 2f + TitleHeight + _lines.Count * RowHeight);
    }

    // ---------- Placement ----------

    /// <summary>Over the tower, its pointer on it; kept on screen, and under the tower when there is no room above.</summary>
    private void Place()
    {
        Camera camera = Camera.main;
        if (camera == null) return;

        // Just over the tower's top edge, or just under its bottom one.
        Vector3 tower = _tower.transform.position;
        if (!ToLocal(camera, tower + Vector3.up * 0.42f, out Vector2 top) ||
            !ToLocal(camera, tower - Vector3.up * 0.42f, out Vector2 bottom))
            return;

        Rect area = _root.rect;
        Vector2 size = _panel.sizeDelta;
        float lift = PointerSize * 0.7f + size.y * 0.5f;
        bool below = top.y + lift + size.y * 0.5f > area.yMax - 12f;

        Vector2 centre = below ? bottom - Vector2.up * lift : top + Vector2.up * lift;
        float half = size.x * 0.5f + 16f;
        centre.x = Mathf.Clamp(centre.x, area.xMin + half, area.xMax - half);
        _panel.anchoredPosition = centre;

        // The pointer stays on the tower even when the panel slides to keep on screen.
        float edge = size.y * 0.5f + PointerSize * 0.35f;
        _pointer.anchoredPosition = new Vector2(top.x - centre.x, below ? edge : -edge);
        _pointer.localRotation = Quaternion.Euler(0f, 0f, below ? 180f : 0f);
    }

    private bool ToLocal(Camera camera, Vector3 world, out Vector2 local)
    {
        Vector2 screen = camera.WorldToScreenPoint(world);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out local);
    }

    // ---------- Build ----------

    private void Build()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        _root = (RectTransform)transform;
        RefusalCopySO copy = Resources.Load<RefusalCopySO>(RefusalCopySO.ResourcePath);
        _font = copy != null ? copy.Font : null;
        JuiceTokensSO t = Juice.Tokens;

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        _panel = (RectTransform)panel.transform;
        _panel.SetParent(_root, false);
        _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(0.5f, 0.5f);
        _panel.sizeDelta = new Vector2(Width, 200f);
        Image sheet = panel.GetComponent<Image>();
        sheet.sprite = JuiceSprites.Panel;
        sheet.type = Image.Type.Sliced;
        sheet.color = t.Colors.Sheet;
        sheet.raycastTarget = false;
        _group = panel.GetComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        _group.interactable = false;

        GameObject pointer = new GameObject("Pointer", typeof(RectTransform), typeof(Image));
        _pointer = (RectTransform)pointer.transform;
        _pointer.SetParent(_panel, false);
        _pointer.sizeDelta = Vector2.one * PointerSize;
        Image arrow = pointer.GetComponent<Image>();
        arrow.sprite = JuiceSprites.ArrowDown;
        arrow.color = t.Colors.Sheet;
        arrow.raycastTarget = false;

        _title = Label("Title", _panel, 38f, t.Colors.Ink, TextAlignmentOptions.MidlineLeft);
        RectTransform titleRect = _title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(Padding + 4f, -Padding - TitleHeight);
        titleRect.offsetMax = new Vector2(-130f, -Padding);

        GameObject pill = new GameObject("Tier", typeof(RectTransform), typeof(Image));
        RectTransform pillRect = (RectTransform)pill.transform;
        pillRect.SetParent(_panel, false);
        pillRect.anchorMin = pillRect.anchorMax = new Vector2(1f, 1f);
        pillRect.pivot = new Vector2(1f, 1f);
        pillRect.sizeDelta = new Vector2(104f, 44f);
        pillRect.anchoredPosition = new Vector2(-Padding, -Padding - 7f);
        _tierPill = pill.GetComponent<Image>();
        _tierPill.sprite = JuiceSprites.Pill;
        _tierPill.type = Image.Type.Sliced;
        _tierPill.raycastTarget = false;
        _tier = Label("Label", pillRect, 28f, t.Colors.Nav, TextAlignmentOptions.Center);
        _tier.rectTransform.anchorMin = Vector2.zero;
        _tier.rectTransform.anchorMax = Vector2.one;
        _tier.rectTransform.offsetMin = _tier.rectTransform.offsetMax = Vector2.zero;

        panel.SetActive(false);
    }

    private void AddRow()
    {
        int index = _labels.Count;
        float y = -Padding - TitleHeight - index * RowHeight;
        JuiceTokensSO t = Juice.Tokens;

        TextMeshProUGUI label = Label("Stat", _panel, 28f, t.Colors.InkMuted, TextAlignmentOptions.MidlineLeft);
        Row(label.rectTransform, y, Padding + 4f, -Width * 0.42f);
        TextMeshProUGUI value = Label("Value", _panel, 34f, t.Colors.Ink, TextAlignmentOptions.MidlineRight);
        Row(value.rectTransform, y, Width * 0.5f, -Padding - 4f);

        _labels.Add(label);
        _values.Add(value);
    }

    private static void Row(RectTransform rect, float y, float left, float right)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, y - RowHeight);
        rect.offsetMax = new Vector2(right, y);
    }

    private TextMeshProUGUI Label(string name, Transform parent, float size, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (_font != null) text.font = _font;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }
}
