using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InfoPanelCanvas : BaseInfoPanelService
{
    [Title("References")] 
    [SerializeField] private GameObject contentObject;
    [SerializeField] private GameObject panelObject;
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private TextMeshProUGUI title;
    [SerializeField] private TextMeshProUGUI description;
    [SerializeField] private Image panelImage;
    [SerializeField] private Button closeButton;

    [Tooltip("Optional. The dimmed area behind the panel — tapping it closes, the standard modal gesture. " +
             "Leave empty and only the close button dismisses.")]
    [SerializeField] private Button backgroundButton;

    [Title("Stats")]
    [InfoBox("One StatPrefab is instantiated into StatsParent per stat the card reports. Optional: with " +
             "either reference empty the panel simply shows no stat table.")]
    [SerializeField] private Transform statsParent;
    [SerializeField] private StatEntryUI statEntryPrefab;

    [Title("Settings")]
    [SerializeField] private Ease fadeInEase =  Ease.OutBack;
    [SerializeField] private float fadeInDuration = 1f;

    [SerializeField] private Ease bumpEase = Ease.OutBack;
    [SerializeField] private float bumpDuration = 0.35f;
    [SerializeField, Tooltip("Scale the panel pops from on show; it tweens up to 1, and bumpEase's overshoot is the bump.")]
    private float bumpStartScale = 0.8f;
    
    private Tween fadeInTween;
    private Tween bumpTween;

    /// <summary>Spawned stat rows, kept between shows rather than destroyed — see <see cref="BuildStats"/>.</summary>
    private readonly List<StatEntryUI> statEntries = new();

    private void Awake()
    {
        ServiceLocator.Register<BaseInfoPanelService>(this);
        InitializeButtons();
        contentObject.SetActive(false);
    }

    private void OnDestroy()
    {
        fadeInTween?.Kill();
        bumpTween?.Kill();
        ServiceLocator.Unregister<BaseInfoPanelService>();
    }

    private void InitializeButtons()
    {
        closeButton.onClick.AddListener(() =>
        {
            HideInfoPanel();
        });

        // Tapping the dimmed backdrop dismisses too. A tap on the card itself can never reach this: the
        // Panel's own Background is a raycast target, so it absorbs the click before it falls through.
        if (backgroundButton != null) backgroundButton.onClick.AddListener(HideInfoPanel);
    }

    [Button]
    public override void ShowInfoPanel(InfoPanelData infoPanelData)
    {
        SetupPanel(infoPanelData);

        // Restart cleanly if a previous fade is still running, then fade alpha 0 -> 1.
        fadeInTween?.Kill();
        panelCanvasGroup.alpha = 0f;
        contentObject.SetActive(true);
        fadeInTween = panelCanvasGroup.DOFade(1f, fadeInDuration)
            .SetEase(fadeInEase)
            .SetUpdate(true) // unscaled time: still fades if the game is paused (timeScale 0)
            .OnKill(() => fadeInTween = null); // fires on manual kill AND auto-kill, so the ref never dangles into a recycled tween

        // Bump only the panel (not the dimmed background): pop its scale from bumpStartScale up to 1.
        bumpTween?.Kill();
        panelObject.transform.localScale = Vector3.one * bumpStartScale;
        bumpTween = panelObject.transform.DOScale(1f, bumpDuration)
            .SetEase(bumpEase)
            .SetUpdate(true)
            .OnKill(() => bumpTween = null);

        TriggerOnInfoPanelShow();
    }

    [Button]
    public override void HideInfoPanel()
    {
        // No tween on hide — and if the show animations are mid-flight, stop them so they can't keep running.
        fadeInTween?.Kill();
        bumpTween?.Kill();
        contentObject.SetActive(false);
        TriggerOnInfoPanelHide();
    }

    private void SetupPanel(InfoPanelData infoPanelData)
    {
        title.text = infoPanelData.Title;
        description.text = infoPanelData.Description;
        panelImage.sprite = infoPanelData.Icon;

        BuildStats(infoPanelData.Stats);
    }

    /// <summary>
    /// Fills the stat grid, one row per stat. Rows are reused and deactivated rather than destroyed and
    /// re-instantiated: this panel opens on every card tap, cards differ by only a row or two, and a
    /// GridLayoutGroup skips inactive children, so a surplus row leaves no hole in the layout.
    /// </summary>
    private void BuildStats(IReadOnlyList<CardStatProgress> stats)
    {
        if (statsParent == null || statEntryPrefab == null) return;

        int count = stats?.Count ?? 0;

        while (statEntries.Count < count)
            statEntries.Add(Instantiate(statEntryPrefab, statsParent));

        for (int i = 0; i < statEntries.Count; i++)
        {
            bool used = i < count;

            statEntries[i].gameObject.SetActive(used);
            if (used) statEntries[i].SetStat(stats[i]);
        }
    }
}
