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
    [SerializeField] protected CardGFXController gfxController;
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
    protected BaseTeamManager _teamManager;

    private static int uniqueID;
    public int uniqueRuntimeId { get; private set; } = uniqueID++;

    /// <summary>The local verdict on a drop. Valid means the play was sent to the server; invalid carries why
    /// it was not. Raised for presentation — the card itself has already acted on it.</summary>
    public event Action<CardValidation> OnDropResolved;

    /// <summary>The server's verdict on a play this card sent. When valid, the card is retired right after
    /// this is raised; when invalid, it carries the refusal in card terms.</summary>
    public event Action<CardValidation> OnPlayResolved;

    /// <summary>This card just became visible on a hand slot — drawn, or handed the slot it was waiting for.</summary>
    public event Action OnPlacedInSlot;

    /// <summary>
    /// Any card in the hand was refused a play, either by its own drop check or by the server, and why. For
    /// listeners outside the hand — the mana bar flashes when the reason is mana. A drop back onto the hand
    /// itself is a cancel, not a refusal, and does not raise this.
    /// </summary>
    public static event Action<CardInvalidReason> AnyPlayRefused;

    /// <summary>What this card is. Read by anything that has to reason about the hand from outside it —
    /// the tutorial, which has to find "a tower card" to point the player at.</summary>
    public CardDataSO CardData => cardDataSo;

    /// <summary>This card as a UI rect, for a highlight to frame.</summary>
    public RectTransform Rect => rectTransform;

    /// <summary>Whether something outside the hand is currently holding this card back — see
    /// <see cref="SetInteractable"/>.</summary>
    public bool InteractionBlocked { get; private set; }

    /// <summary>
    /// Opens or closes this card to the player's finger. Used by the tutorial to narrow the hand to the one
    /// card a step is asking for, so a misdrop cannot spend what that step is waiting on.
    /// </summary>
    /// <remarks>
    /// Blocked at the raycast, so no drag ever begins and none of the per-family drag code (a spell's ghost,
    /// a tower's placement preview) runs on a card the player may not play. It is deliberately idempotent
    /// and tracked separately from <c>blocksRaycasts</c>, which a drag in progress also owns: re-opening a
    /// card that was never closed must not write over the drag's own state and drop it mid-flight.
    /// </remarks>
    public void SetInteractable(bool interactable)
    {
        if (InteractionBlocked == !interactable) return;

        InteractionBlocked = !interactable;
        selfCanvasGroup.blocksRaycasts = interactable;
    }

    protected virtual void Start()
    {
        _cameraMain = Camera.main;

        _clientManaManager = ServiceLocator.Get<BaseClientManaManager>();
        _mapTranslator = ServiceLocator.Get<BaseMapTranslator>();
        _teamManager = ServiceLocator.Get<BaseTeamManager>();
    }

    public void Initialize(CardUIFactoryData factoryData, BaseCardContainer cardContainer)
    {
        _cardContainer = cardContainer;
        
        _canvasArea = factoryData.CardsCanvas;
        _safeArea = factoryData.SafeAreaParent;
        _blockingRaycaster = factoryData.BlockCardsCanvas;

        transform.SetParent(factoryData.CardParent);
        _originalParent = factoryData.CardParent;

        if (_clientManaManager == null)
            _clientManaManager = ServiceLocator.Get<BaseClientManaManager>();
        
        gfxController.Initialize(cardDataSo, _clientManaManager);

        // A free slot is not guaranteed yet: the server announces the replacement card before it answers
        // the play that retires the one still holding the slot. The container parks this card and calls
        // PlaceInSlot the moment a slot opens - destroying it here would shrink the hand for good.
        PlaceInSlot(_cardContainer.AddCardToSlot(this));
    }

    /// <summary>
    /// Puts this card on a hand slot. A null slot parks it out of sight and out of reach instead, until
    /// the container hands it a real one.
    /// </summary>
    public void PlaceInSlot(Transform slotTransform)
    {
        if (slotTransform == null)
        {
            selfCanvasGroup.alpha = 0f;
            selfCanvasGroup.blocksRaycasts = false;
            return;
        }

        RectTransform slotRect = (RectTransform)slotTransform;
        rectTransform.anchoredPosition = slotRect.anchoredPosition;
        originalPosition = slotRect.anchoredPosition;

        selfCanvasGroup.alpha = 1f;
        selfCanvasGroup.blocksRaycasts = true;

        // A card arriving on a slot is open by definition, and this writes the raycast flag directly — so
        // the block has to be cleared with it, or a recycled card would read as closed while being open.
        InteractionBlocked = false;

        OnPlacedInSlot?.Invoke();
    }
    
    public virtual void OnBeginDrag(PointerEventData eventData)
    {
        // Grabbing any card dismisses a lingering tower range readout (UI interaction takes over).
        ClientTowerRangeGFX.HideSelection();

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

        // The hand check first: a card let go over the hand is a cancel, whatever else would also be wrong
        // with that spot, and must read as one rather than as a refused play.
        CardValidation validation = CanPlayCardAtCanvas(eventData.position);
        if (validation) validation = CanPlayCardAt(worldPos);

        if (validation)
        {
            _waitingResult = true;
            ActivateCard(worldPos);
        }

        selfCanvasGroup.blocksRaycasts = true;
        transform.SetParent(_originalParent);
        // Unscaled: the tutorial freezes the world while it waits for this very drag, and a scaled tween
        // would leave the card hanging wherever it was dropped instead of sliding home.
        rectTransform.DOAnchorPos(originalPosition, 0.4f).SetEase(Ease.OutExpo).SetUpdate(true);

        OnDropResolved?.Invoke(validation);
        if (!validation && validation.Reason != CardInvalidReason.BlockedByUI)
            AnyPlayRefused?.Invoke(validation.Reason);
    }

    /// <summary>
    /// Subclasses report the server's answer here, before retiring the card on success. The refusal reason is
    /// in card terms (each deployer speaks its own enum), so listeners outside the card need only one.
    /// </summary>
    protected void RaisePlayResolved(bool accepted, CardInvalidReason refusal = CardInvalidReason.None)
    {
        OnPlayResolved?.Invoke(accepted ? CardValidation.Valid : CardValidation.Invalid(refusal));
        if (!accepted) AnyPlayRefused?.Invoke(refusal);
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
        TeamType localTeam = _teamManager.GetLocalTeam();
        foreach (RaycastHit2D hit in hits)
        {
            TeamIdentifier team = hit.collider.GetComponentInParent<TeamIdentifier>();
            if (team != null && team.TeamType == localTeam) return true;
        }
        return false;
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