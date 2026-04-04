using DG.Tweening;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

public abstract class AbstractCard : NetworkBehaviour, ICardActivatable, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Card Properties")]
    [SerializeField] protected CardDataSO cardDataSo;

    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Canvas canvasArea;
    [SerializeField] private Transform safeArea;
    [SerializeField] private CanvasGroup selfCanvasGroup;
    
    private Vector2 originalPosition;
    private Transform originalParent;
    private Vector3 originalScale;

    private void Start()
    {
        originalPosition = rectTransform.anchoredPosition;
        originalParent = transform.parent;
        originalScale = transform.localScale;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        transform.DOKill();
        rectTransform.DOKill();
        transform.SetParent(safeArea.transform);
        transform.SetAsLastSibling();
        transform.DOScale(1.15f, 0.4f).SetEase(Ease.OutCirc);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasArea.transform as RectTransform, eventData.position, canvasArea.worldCamera, out Vector2 localPoint);
        rectTransform.anchoredPosition = localPoint;

        selfCanvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        rectTransform.anchoredPosition += eventData.delta / canvasArea.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        var raycast = eventData.pointerCurrentRaycast;
        if (CanPlayCardAt(raycast))
            ActivateCard(raycast);

        selfCanvasGroup.blocksRaycasts = true;
        transform.SetParent(originalParent);
        rectTransform.DOAnchorPos(originalPosition, 0.4f).SetEase(Ease.OutExpo);
        transform.DOScale(originalScale, 0.4f).SetEase(Ease.OutQuint);
    }

    public virtual CardValidation CanPlayCard()
    {
        if (!ClientManaManager.Instance.CanAffordLocally(cardDataSo.Cost))
            return CardValidation.Invalid(CardInvalidReason.NotEnoughMana);

        return CardValidation.Valid;
    }

    public virtual CardValidation CanPlayCardAt(RaycastResult target)
    {
        return CanPlayCard();
    }

    public abstract void ActivateCard(RaycastResult target);
}