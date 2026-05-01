using System;
using System.Collections.Generic;
using DG.Tweening;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public abstract class AbstractCard : NetworkBehaviour, ICardActivatable, IBeginDragHandler, IDragHandler, IEndDragHandler
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
    protected BaseTowerPlacementFeedbackManager  _towerPlacementFeedbackManager;

    private static int uniqueID;
    public int uniqueRuntimeId { get; private set; } = uniqueID++;

    protected virtual void Start()
    {
        originalPosition = rectTransform.anchoredPosition;
        _originalParent = transform.parent;
        _cameraMain = Camera.main;
        
        _clientManaManager = ServiceLocator.Get<BaseClientManaManager>();
        _towerPlacementFeedbackManager  = ServiceLocator.Get<BaseTowerPlacementFeedbackManager>();
    }

    public void Initialize(CardUIFactoryData factoryData)
    {
        _canvasArea = factoryData.CardsCanvas;
        _safeArea = factoryData.SafeAreaParent;
        _blockingRaycaster = factoryData.BlockCardsCanvas;
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
}