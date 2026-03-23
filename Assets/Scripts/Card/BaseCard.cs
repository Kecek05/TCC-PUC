using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

public class BaseCard : MonoBehaviour, ICardActivatable, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Title("Card Properties")]
    [SerializeField] private string cardName;
    [SerializeField] private string description;
    [SerializeField] private Sprite cardImage;
    [SerializeField] private int manaCost;

    private RectTransform rectTransform;
    private Canvas canvas;
    private Vector2 originalPosition;
    private Transform originalParent;
    private Vector3 originalScale;
    
    
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalPosition = rectTransform.anchoredPosition;
        originalParent = transform.parent;
        originalScale = transform.localScale;
        
        transform.SetParent(canvas.transform);
        transform.SetAsLastSibling();
        transform.DOScale(1.3f, 0.4f).SetEase(Ease.InOutExpo);
        
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.transform as RectTransform, eventData.position, canvas.worldCamera, out Vector2 localPoint);
        rectTransform.anchoredPosition = localPoint;
        
        GetComponent<CanvasGroup>().blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        ActivateCard();
        GetComponent<CanvasGroup>().blocksRaycasts = true;

        transform.SetParent(originalParent);
        rectTransform.DOAnchorPos(originalPosition, 0.4f).SetEase(Ease.OutExpo);
        transform.DOScale(originalScale, 0.4f).SetEase(Ease.OutQuint);
        
    }

    public virtual void ActivateCard()
    {
        Debug.Log("Activating BaseCard: " + cardName);
    }
}
