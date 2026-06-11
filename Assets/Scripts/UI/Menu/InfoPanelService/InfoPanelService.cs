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
    
    [Title("Settings")]
    [SerializeField] private Ease fadeInEase =  Ease.OutBack;
    [SerializeField] private float fadeInDuration = 1f;

    [SerializeField] private Ease bumpEase = Ease.OutBack;
    [SerializeField] private float bumpDuration = 0.35f;
    [SerializeField, Tooltip("Scale the panel pops from on show; it tweens up to 1, and bumpEase's overshoot is the bump.")]
    private float bumpStartScale = 0.8f;
    
    private Tween fadeInTween;
    private Tween bumpTween;
    
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
    }
}
