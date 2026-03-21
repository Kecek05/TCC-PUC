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

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalPosition = rectTransform.anchoredPosition;
        originalParent = transform.parent;
        
        transform.SetParent(canvas.transform);
        transform.SetAsLastSibling();
        
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.transform as RectTransform, eventData.position, canvas.worldCamera, out Vector2 localPoint);
        rectTransform.anchoredPosition = localPoint;
        // rectTransform.DOAnchorPos(localPoint, 0.15f).SetEase(Ease.OutQuad);
        
        GetComponent<CanvasGroup>().blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        GetComponent<CanvasGroup>().blocksRaycasts = true;

        transform.SetParent(originalParent);
        rectTransform.DOAnchorPos(originalPosition, 0.4f).SetEase(Ease.OutBack);
    }

    public virtual void ActivateCard()
    {
        Debug.Log("Activating BaseCard: " + cardName);
    }
}
