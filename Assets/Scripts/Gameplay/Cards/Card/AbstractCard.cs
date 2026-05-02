using System;
using System.Collections.Generic;
using DG.Tweening;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public abstract class AbstractCard : MonoBehaviour, ICardActivatable, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Card Properties")]
    [SerializeField] protected CardDataSO cardDataSo;
    [SerializeField] protected LayersSettingsSO layersSettings;
    [Space(5f)]
    
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private CanvasGroup selfCanvasGroup;
    private Canvas _canvasArea;
    private Transform _safeArea;
    private GraphicRaycaster _blockingRaycaster;

    private readonly List<RaycastResult> _blockingRaycastResults = new();
    private Vector2 originalPosition;
    private Transform _originalParent;
    protected bool _waitingResult;
    protected Camera _cameraMain;
    
    protected BaseClientManaManager  _clientManaManager;
    protected BaseCardContainer _cardContainer;
    protected BaseMapTranslator _mapTranslator;
    
    private static int uniqueID;
    public int uniqueRuntimeId { get; private set; } = uniqueID++;

    protected virtual void Start()
    {
        _cameraMain = Camera.main;
        
        _clientManaManager = ServiceLocator.Get<BaseClientManaManager>();
        _mapTranslator = ServiceLocator.Get<BaseMapTranslator>();
    }

    public void Initialize(CardUIFactoryData factoryData, BaseCardContainer cardContainer)
    {
        _cardContainer = cardContainer;
        
        _canvasArea = factoryData.CardsCanvas;
        _safeArea = factoryData.SafeAreaParent;
        _blockingRaycaster = factoryData.BlockCardsCanvas;

        transform.SetParent(factoryData.CardParent);
        RectTransform slotTransform = (RectTransform)_cardContainer.AddCardToSlot(this);
        rectTransform.anchoredPosition = slotTransform.anchoredPosition;
        originalPosition = slotTransform.anchoredPosition;
        _originalParent = factoryData.CardParent;
    }
    
    public virtual void OnBeginDrag(PointerEventData eventData)
    {
        transform.DOKill();
        rectTransform.DOKill();
        transform.SetParent(_safeArea.transform);
        transform.SetAsLastSibling();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasArea.transform as RectTransform, eventData.position, _canvasArea.worldCamera, out Vector2 localPoint);
        rectTransform.anchoredPosition = localPoint;

        selfCanvasGroup.blocksRaycasts = false;
    }

    public virtual void OnDrag(PointerEventData eventData)
    {
        rectTransform.anchoredPosition += eventData.delta / _canvasArea.scaleFactor;
    }

    public virtual void OnEndDrag(PointerEventData eventData)
    {
        Vector2 worldPos = GetWorldPosition(eventData);

        if (CanPlayCardAt(worldPos) && CanPlayCardAtCanvas(eventData.position))
        {
            _waitingResult = true;
            ActivateCard(worldPos);
        }

        selfCanvasGroup.blocksRaycasts = true;
        transform.SetParent(_originalParent);
        rectTransform.DOAnchorPos(originalPosition, 0.4f).SetEase(Ease.OutExpo);
    }

    protected Vector2 GetWorldPosition(PointerEventData eventData)
    {
        return _cameraMain.ScreenToWorldPoint(eventData.position);
    }
    
    protected bool IsEnemyMap(Vector2 position)
    {
        RaycastHit2D[] hits = Physics2D.CircleCastAll(position, layersSettings.PlaceableRadius, Vector2.zero, 10f, layersSettings.EnemyMapLayer);
        return hits.Length > 0;
    }

    protected bool IsLocalMap(Vector2 position)
    {
        RaycastHit2D[] hits = Physics2D.CircleCastAll(position, layersSettings.PlaceableRadius, Vector2.zero, 10f, layersSettings.PlaceableLayer);
        return hits.Length > 0;
    }

    public virtual CardValidation CanPlayCard()
    {
        if (!_clientManaManager.CanAffordLocally(cardDataSo.Cost))
            return CardValidation.Invalid(CardInvalidReason.NotEnoughMana);

        if (_waitingResult) return CardValidation.Invalid(CardInvalidReason.WaitingForServer);
        
        return CardValidation.Valid;
    }

    public virtual CardValidation CanPlayCardAt(Vector2 worldPosition)
    {
        return CanPlayCard();
    }

    public virtual CardValidation CanPlayCardAtCanvas(Vector2 screenPosition)
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = screenPosition };
        _blockingRaycastResults.Clear();
        _blockingRaycaster.Raycast(pointerData, _blockingRaycastResults);

        if (_blockingRaycastResults.Count > 0)
            return CardValidation.Invalid(CardInvalidReason.BlockedByUI);

        return CardValidation.Valid;
    }

    public abstract void ActivateCard(Vector2 worldPosition);

    protected virtual void DiscardSelfCard()
    {
        _cardContainer.Unoccupy(this);
        Destroy(gameObject);
    }
}