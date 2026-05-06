using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class MenuNavButton : MonoBehaviour
{
    [Title("References")]
    [SerializeField] private Button button;
    [SerializeField] private Transform scaleTarget;
    [SerializeField] private Image colorTarget;

    [Title("Selected State")]
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private Color selectedColor = Color.white;

    [Title("Deselected State")]
    [SerializeField] private float deselectedScale = 1f;
    [SerializeField] private Color deselectedColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    [Title("Tween Settings")]
    [SerializeField] private float selectScaleDuration = 0.3f;
    [SerializeField] private Ease selectScaleEase = Ease.OutBack;
    [SerializeField] private float deselectScaleDuration = 0.25f;
    [SerializeField] private Ease deselectScaleEase = Ease.OutQuad;
    [SerializeField] private float colorDuration = 0.25f;
    [SerializeField] private Ease colorEase = Ease.OutQuad;

    public Button Button => button;

    private bool _isSelected;

    private void Reset()
    {
        button = GetComponent<Button>();
        scaleTarget = transform;
        colorTarget = GetComponent<Image>();
    }

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (scaleTarget == null) scaleTarget = transform;
    }

    public void SetSelected(bool selected, bool animated)
    {
        _isSelected = selected;

        float targetScale = selected ? selectedScale : deselectedScale;
        Color targetColor = selected ? selectedColor : deselectedColor;

        if (animated)
        {
            scaleTarget.DOKill();
            float duration = selected ? selectScaleDuration : deselectScaleDuration;
            Ease ease = selected ? selectScaleEase : deselectScaleEase;
            scaleTarget.DOScale(targetScale, duration).SetEase(ease);

            if (colorTarget != null)
            {
                colorTarget.DOKill();
                colorTarget.DOColor(targetColor, colorDuration).SetEase(colorEase);
            }
        }
        else
        {
            scaleTarget.DOKill();
            scaleTarget.localScale = Vector3.one * targetScale;

            if (colorTarget != null)
            {
                colorTarget.DOKill();
                colorTarget.color = targetColor;
            }
        }
    }
}
