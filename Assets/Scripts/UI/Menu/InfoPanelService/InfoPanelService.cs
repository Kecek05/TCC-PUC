using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InfoPanelCanvas : BaseInfoPanelService
{
    [Title("References")] 
    [SerializeField] private GameObject contentObject;
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private TextMeshProUGUI title;
    [SerializeField] private TextMeshProUGUI description;
    [SerializeField] private Image panelImage;
    [SerializeField] private Button closeButton;
    
    [Title("Settings")]
    [SerializeField] private Ease fadeInEase =  Ease.OutBack;
    [SerializeField] private float fadeInDuration = 1f;
    
    private Tween fadeInTween;
    
    private void Awake()
    {
        ServiceLocator.Register<BaseInfoPanelService>(this);
        InitializeButtons();
        contentObject.SetActive(false);
    }

    private void OnDestroy()
    {
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
        
        
        TriggerOnInfoPanelShow();
    }

    [Button]
    public override void HideInfoPanel()
    {
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
