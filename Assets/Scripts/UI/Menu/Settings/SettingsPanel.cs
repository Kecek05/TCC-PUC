using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The comfort settings page: Sound, Music (only when the game has music), Vibration, Screen shake and Reduce
/// motion. Every switch writes straight to <see cref="BaseGameSettingsService"/>, which the feedback services
/// read, so a change takes effect on the next sound, buzz or shake.
/// </summary>
/// <remarks>
/// Built in code under its own canvas, so the page needs no prefab and a new setting is one line in
/// <see cref="Build"/>. Opened by the gear in the Main Menu's top bar.
/// </remarks>
public class SettingsPanel : MonoBehaviour
{
    [Title("Opens it")]
    [SerializeField, Required] private Button openButton;

    [Title("Look")]
    [SerializeField] private TMP_FontAsset font;
    [Tooltip("The info panel's 9-sliced background.")]
    [SerializeField] private Sprite panelSprite;
    [Tooltip("A 9-sliced pill: rows and switch tracks.")]
    [SerializeField] private Sprite pillSprite;
    [SerializeField] private Sprite circleSprite;
    [SerializeField] private Sprite closeSprite;

    private readonly List<(JuiceToggle toggle, Func<BaseGameSettingsService, bool> read)> _toggles = new();
    private RectTransform _content;
    private PopupAnimator _popup;
    private BaseGameSettingsService _settings;
    private bool _open;

    private void Awake()
    {
        Build();
        _content.gameObject.SetActive(false);
    }

    private void Start()
    {
        ServiceLocator.TryGet(out _settings);
        if (openButton != null) openButton.onClick.AddListener(Open);
    }

    private void OnDestroy()
    {
        if (openButton != null) openButton.onClick.RemoveListener(Open);
    }

    public void Open()
    {
        if (_open) return;
        _open = true;

        foreach ((JuiceToggle toggle, Func<BaseGameSettingsService, bool> read) in _toggles)
            toggle.Set(_settings == null || read(_settings), false);

        _content.gameObject.SetActive(true);
        _popup.Open();
    }

    public void Close()
    {
        if (!_open) return;
        _open = false;
        _popup.Close(() => _content.gameObject.SetActive(false));
    }

    private void Build()
    {
        JuiceTokensSO tokens = Juice.Tokens;

        _content = NewRect("Content", transform);
        Stretch(_content);

        // A tap outside the panel closes it, like every other modal.
        RectTransform scrimRect = NewRect("Scrim", _content);
        Stretch(scrimRect);
        Image scrim = scrimRect.gameObject.AddComponent<Image>();
        scrim.color = tokens.Colors.Scrim;
        CanvasGroup scrimGroup = scrimRect.gameObject.AddComponent<CanvasGroup>();
        Button scrimButton = scrimRect.gameObject.AddComponent<Button>();
        scrimButton.transition = Selectable.Transition.None;
        scrimButton.onClick.AddListener(Close);

        RectTransform panel = NewRect("Panel", _content);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(820f, 0f);
        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.sprite = panelSprite;
        panelImage.type = panelSprite != null && panelSprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        panelImage.color = tokens.Colors.Panel;

        VerticalLayoutGroup column = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(70, 70, 50, 70);
        column.spacing = 20f;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        ContentSizeFitter fit = panel.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TextMeshProUGUI title = NewText("Title", panel, "Settings", 64f);
        title.alignment = TextAlignmentOptions.Center;
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 96f;

        AddRow(panel, "Sound", s => s.SoundEnabled, (s, v) => s.SetSoundEnabled(v));
        SfxLibrarySO library = Resources.Load<SfxLibrarySO>(SfxLibrarySO.ResourcePath);
        if (library != null && library.HasMusic) AddRow(panel, "Music", s => s.MusicEnabled, (s, v) => s.SetMusicEnabled(v));
        AddRow(panel, "Vibration", s => s.VibrationEnabled, (s, v) => s.SetVibrationEnabled(v));
        AddRow(panel, "Screen shake", s => s.ScreenShakeEnabled, (s, v) => s.SetScreenShakeEnabled(v));
        AddRow(panel, "Reduce motion", s => s.ReduceMotion, (s, v) => s.SetReduceMotion(v));

        // Close: top-right corner, outside the column's layout.
        RectTransform close = NewRect("Close", panel);
        close.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        close.anchorMin = close.anchorMax = close.pivot = new Vector2(1f, 1f);
        close.anchoredPosition = new Vector2(-26f, -26f);
        close.sizeDelta = new Vector2(84f, 84f);
        Image closeImage = close.gameObject.AddComponent<Image>();
        closeImage.sprite = closeSprite;
        closeImage.preserveAspect = true;
        Button closeButton = close.gameObject.AddComponent<Button>();
        closeButton.onClick.AddListener(Close);
        close.gameObject.AddComponent<JuicyButton>();

        _popup = gameObject.AddComponent<PopupAnimator>();
        _popup.Configure(panel, scrimGroup, panel, false);
    }

    private void AddRow(RectTransform parent, string label, Func<BaseGameSettingsService, bool> read,
        Action<BaseGameSettingsService, bool> write)
    {
        RectTransform row = NewRect(label, parent);
        Image background = row.gameObject.AddComponent<Image>();
        background.sprite = pillSprite;
        background.type = pillSprite != null && pillSprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        background.pixelsPerUnitMultiplier = 2f;
        background.color = Juice.Tokens.Colors.Stat;
        background.raycastTarget = false;

        HorizontalLayoutGroup line = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        line.padding = new RectOffset(34, 22, 12, 12);
        line.spacing = 20f;
        line.childAlignment = TextAnchor.MiddleLeft;
        line.childControlWidth = line.childControlHeight = true;
        line.childForceExpandWidth = false;
        line.childForceExpandHeight = false;
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 104f;

        TextMeshProUGUI text = NewText("Label", row, label, 46f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        JuiceToggle toggle = JuiceToggle.Create(row, pillSprite, circleSprite, new Vector2(156f, 80f));
        toggle.Bind(true, value => { if (_settings != null) write(_settings, value); });
        _toggles.Add((toggle, read));
    }

    private TextMeshProUGUI NewText(string objectName, Transform parent, string value, float size)
    {
        RectTransform rect = NewRect(objectName, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = Juice.Tokens.Colors.Ink;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static RectTransform NewRect(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
