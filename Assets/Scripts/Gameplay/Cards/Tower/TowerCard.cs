using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TowerCard : AbstractCard
{
    private GhostTowerCard _ghostTowerCard;
    private TowerDataSO _cachedTowerDataSO;
    private TowerCardDataSO _cachedTowerCardDataSO;
    [Space(5f)]
    
    [Header("GFXs")] 
    [SerializeField] private MMF_Player fadeOutFeedback;
    [SerializeField] private MMF_Player fadeInFeedback;
    [Space(5f)]

    private bool _enabledTowerGFX = false;
    private IPlaceable _currentPlaceable;

    /// <summary>The slot the ghost last snapped to, so each new slot ticks once (TowerPlacement).</summary>
    private IPlaceable _snappedPlaceable;

    private readonly List<Vector2> _freeSlots = new();
    
    private BaseCardTowerDeployer _cardTowerDeployer;
    private BaseTowerPlacementFeedbackManager  _towerPlacementFeedbackManager;

    protected override void Start()
    {
        base.Start();
        _cardTowerDeployer = ServiceLocator.Get<BaseCardTowerDeployer>();
        _towerPlacementFeedbackManager = ServiceLocator.Get<BaseTowerPlacementFeedbackManager>();
    }

    public void Initialize(CardUIFactoryData factoryData, BaseCardContainer cardContainer, GhostTowerCard ghostTowerCard)
    {
        base.Initialize(factoryData, cardContainer);
        _ghostTowerCard = ghostTowerCard;
    }

    public override void OnBeginDrag(PointerEventData eventData)
    {
        base.OnBeginDrag(eventData);
        DisableGhostTowerGFX();
        _ghostTowerCard.SetSprite(GetTowerCardDataSO().TowerGhostSprite);
        _ghostTowerCard.SetTint(cardDataSo.CardColor);
        _ghostTowerCard.SetRange(GetTowerDataSO().RangeLevel1);
        _ghostTowerCard.ShowSlots(FreeSlots());
    }

    /// <summary>The local field's empty slots: framed on the board while this card is held (TowerPlacement).</summary>
    private IReadOnlyList<Vector2> FreeSlots()
    {
        _freeSlots.Clear();
        TeamType local = _teamManager.GetLocalTeam();

        foreach (AbstractPlaceable placeable in FindObjectsByType<AbstractPlaceable>(FindObjectsSortMode.None))
        {
            if (placeable == null || placeable.IsOccupied() || placeable.PlaceablePoint == null) continue;

            TeamIdentifier team = placeable.GetComponentInParent<TeamIdentifier>();
            if (team == null || team.TeamType != local) continue;

            _freeSlots.Add(placeable.PlaceablePoint.position);
        }

        return _freeSlots;
    }

    public override void OnDrag(PointerEventData eventData)
    {
        base.OnDrag(eventData);

        Vector2 worldPosition = GetWorldPosition(eventData);

        if (IsEnemyMap(worldPosition) || !CanPlayCardAtCanvas(eventData.position))
        {
            DisableGhostTowerGFX();
            _ghostTowerCard.SetVisible(false);
            _snappedPlaceable = null;
            return;
        }

        if (!IsPlaceableAvailable(worldPosition))
        {
            // Occupied spot: preview the upgrade (current + next range) if it holds an upgradeable tower of
            // this card's type; otherwise there's nothing to show here. Return either way so the placement
            // ghost below doesn't overwrite the upgrade rings (it would re-hide nextRangeGfx).
            if (!TryShowUpgradePreview(worldPosition))
            {
                DisableGhostTowerGFX();
                ShowBlockedGhost(worldPosition);
            }
            else
            {
                TickOnSnap(_currentPlaceable);
            }
            return;
        }

        AnimateFadeOut();
        EnableGhostTowerGFX(worldPosition);
        TickOnSnap(_currentPlaceable);
    }

    /// <summary>
    /// A spot that will refuse this tower (another tower there, or this one already at its top level) shows the
    /// ghost in the deny colour, wobbling, instead of hiding it: the player sees the no before letting go.
    /// </summary>
    private void ShowBlockedGhost(Vector2 worldPosition)
    {
        IPlaceable placeable = GetClosestPlaceable(worldPosition);
        if (placeable == null || !placeable.IsOccupied())
        {
            _ghostTowerCard.SetVisible(false);
            _snappedPlaceable = null;
            return;
        }

        _ghostTowerCard.ShowBlocked(placeable.PlaceablePoint.position);
        if (_snappedPlaceable != placeable)
        {
            _snappedPlaceable = placeable;
            Juice.Sfx(SfxId.Deny, 0, 0.5f);
        }
    }

    /// <summary>The ghost snapped onto a new slot: the slot lights, a tick, a Selection buzz and a pulse on the ghost.</summary>
    private void TickOnSnap(IPlaceable placeable)
    {
        if (placeable == null || placeable == _snappedPlaceable) return;

        _snappedPlaceable = placeable;
        _ghostTowerCard.PulseSnap();
        _ghostTowerCard.FlashSlot(placeable.PlaceablePoint.position);
        Juice.Sfx(SfxId.Tick);
        Juice.Haptic(HapticRole.Select);
    }

    private bool TryShowUpgradePreview(Vector2 worldPosition)
    {
        IPlaceable placeable = GetClosestPlaceable(worldPosition);
        if (placeable == null || !placeable.IsOccupied()) return false;

        // OccupiedTower is server-authoritative (clients occupy with a null tower in OccupyPlaceable),
        // so resolve the occupying tower from the client-side registry — works on host and client alike.
        TowerManager tower = ResolveTowerAt(placeable.PlaceablePoint.position);
        TowerDataSO cardData = GetTowerDataSO();
        if (tower == null || tower.Data == null || cardData == null) return false;

        // Only a same-type tower can be upgraded by this card (matches the server's LevelUp rule).
        if (tower.Data.TowerType != cardData.TowerType) return false;

        int level = 1;
        if (tower.ServerTowerCombat != null)
            level = Mathf.Clamp(tower.ServerTowerCombat.TowerLevel.Value, 1, tower.Data.MaxLevel);

        bool hasNext = level < tower.Data.MaxLevel;

        // A tower already at its top level is a spot that will refuse this card: the blocked ghost says so
        // before the drop does.
        if (!hasNext) return false;

        float currentRange = tower.Data.GetRangeByLevel(level);
        float nextRange = tower.Data.GetRangeByLevel(level + 1);

        // TowerLevelUp's gain chip: the level it reaches and what that buys, in damage or else in reach.
        string chipTitle = $"LEVEL {level + 1}";
        string chipGain = null;
        float damage = tower.Data.GetDamageByLevel(level);
        float nextDamage = tower.Data.GetDamageByLevel(level + 1);
        if (damage > 0f && nextDamage > damage)
            chipGain = $"+{Mathf.RoundToInt((nextDamage / damage - 1f) * 100f)}% dmg";
        else if (currentRange > 0f && nextRange > currentRange)
            chipGain = $"+{Mathf.RoundToInt((nextRange / currentRange - 1f) * 100f)}% range";

        // Enter "upgrade ghost" mode — fade the card out and remember the placeable so the drop resolves to
        // it (mirrors EnableGhostTowerGFX for placement) — then show the current + next range rings. Must
        // run before _enabledTowerGFX is set so AnimateFadeOut plays once on the first hover frame.
        AnimateFadeOut();
        _enabledTowerGFX = true;
        _currentPlaceable = placeable;
        _ghostTowerCard.ShowUpgradePreview(placeable.PlaceablePoint.position, currentRange, nextRange, true, chipTitle, chipGain);
        return true;
    }

    // Finds the tower occupying a placeable by proximity to its point, using the client-side registry.
    // The placeable's OccupiedTower is only populated on the server/host, so this is what makes the
    // upgrade preview resolve the tower on clients too.
    private TowerManager ResolveTowerAt(Vector2 position)
    {
        TowerManager best = null;
        float bestSqr = layersSettings.PlaceableRadius * layersSettings.PlaceableRadius;

        IReadOnlyList<TowerManager> towers = ClientTowerRegistry.ActiveTowers;
        for (int i = towers.Count - 1; i >= 0; i--)
        {
            TowerManager tower = towers[i];
            if (tower == null) continue;

            float sqr = ((Vector2)tower.transform.position - position).sqrMagnitude;
            if (sqr <= bestSqr)
            {
                bestSqr = sqr;
                best = tower;
            }
        }

        return best;
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        base.OnEndDrag(eventData);
        DisableGhostTowerGFX();
        _ghostTowerCard.SetVisible(false);
        _ghostTowerCard.HideSlots();
        _snappedPlaceable = null;
    }

    /// <summary>
    /// This should only be called by the <see cref="DisableGhostTowerGFX"/>
    /// </summary>
    private void AnimateFadeOut()
    {
        if (_enabledTowerGFX) return;
        fadeInFeedback?.StopFeedbacks();
        fadeOutFeedback?.PlayFeedbacks();
    }
    
    private void AnimateFadeIn()
    {
        if (!_enabledTowerGFX) return;
        fadeOutFeedback?.StopFeedbacks();
        fadeInFeedback?.PlayFeedbacks();
    }
    
    private void EnableGhostTowerGFX(Vector2 worldPosition)
    {
        _enabledTowerGFX = true;
        
        IPlaceable closestPlaceable = GetClosestPlaceable(worldPosition);
        
        if (closestPlaceable == null) return;
        _currentPlaceable = closestPlaceable;

        _ghostTowerCard.SetVisible(true);
        // Reset to the card's base range — an earlier upgrade-hover may have left the ring rescaled.
        TowerDataSO data = GetTowerDataSO();
        if (data != null) _ghostTowerCard.SetRange(data.RangeLevel1);
        _ghostTowerCard.SetPosition(closestPlaceable.PlaceablePoint.position);
    }

    private IPlaceable GetClosestPlaceable(Vector2 worldPosition)
    {
        RaycastHit2D[] hits = Physics2D.CircleCastAll(worldPosition, layersSettings.PlaceableRadius, Vector2.zero, 10f, layersSettings.PlaceableLayer);

        IPlaceable closest = null;
        float closestDist = float.MaxValue;

        foreach (RaycastHit2D hit in hits)
        {
            TeamIdentifier team = hit.collider.GetComponentInParent<TeamIdentifier>();
            if (team == null || team.TeamType != _teamManager.GetLocalTeam()) continue;

            IPlaceable placeable = hit.collider.GetComponentInParent<IPlaceable>();
            if (placeable == null) continue;

            float dist = Vector2.Distance(worldPosition, hit.collider.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = placeable;
            }
        }

        return closest;
    }

    private void DisableGhostTowerGFX()
    {
        if (!_enabledTowerGFX) return;
        AnimateFadeIn();
        _enabledTowerGFX = false;
        _currentPlaceable = null;
        _ghostTowerCard.SetVisible(false);
    }

    public override CardValidation CanPlayCardAt(Vector2 worldPosition)
    {
        var baseCheck = base.CanPlayCardAt(worldPosition);
        if (!baseCheck) return baseCheck;

        if (IsEnemyMap(worldPosition))
            return CardValidation.Invalid(CardInvalidReason.EnemyMap);

        if (!HasPlaceableNearby(worldPosition) && _currentPlaceable == null)
            return CardValidation.Invalid(CardInvalidReason.NoPlaceable);

        // The two refusals the server would send back for an occupied spot, answered here instead, so the
        // player hears why at once rather than after a round trip.
        CardValidation occupied = CheckOccupiedSpot(_currentPlaceable ?? GetClosestPlaceable(worldPosition));
        if (!occupied) return occupied;

        return CardValidation.Valid;
    }

    /// <summary>
    /// A spot that already holds a tower only takes this card if it is the same tower and not yet at its top
    /// level (the server LevelUp rule). Resolved from the client-side registry, like the upgrade preview.
    /// </summary>
    private CardValidation CheckOccupiedSpot(IPlaceable placeable)
    {
        if (placeable == null || !placeable.IsOccupied()) return CardValidation.Valid;

        TowerManager tower = ResolveTowerAt(placeable.PlaceablePoint.position);
        TowerDataSO cardData = GetTowerDataSO();
        if (tower == null || tower.Data == null || cardData == null) return CardValidation.Valid;

        if (tower.Data.TowerType != cardData.TowerType)
            return CardValidation.Invalid(CardInvalidReason.DifferentTower);

        int level = tower.ServerTowerCombat != null ? tower.ServerTowerCombat.TowerLevel.Value : 1;
        return level >= tower.Data.MaxLevel
            ? CardValidation.Invalid(CardInvalidReason.TowerMaxLevel)
            : CardValidation.Valid;
    }

    public override void ActivateCard(Vector2 worldPosition)
    {
        _cardTowerDeployer.OnPlaceResult += HandlePlaceResult;
        _clientManaManager.PredictSpend(cardDataSo.Cost);

        Vector2 position = worldPosition;
        
        if (_currentPlaceable != null)
        {
            position = _currentPlaceable.PlaceablePoint.position;
        }
        
        _towerPlacementFeedbackManager.PredictSpawn(GetTowerCardDataSO().TowerGhostSprite, position, uniqueRuntimeId);
        
        Vector2 serverPosition = _mapTranslator.LocalToServer(position);
        _cardTowerDeployer.RequestPlaceCardServer(cardDataSo.CardType, serverPosition);
    }

    private void HandlePlaceResult(TowerPlaceResult result)
    {
        if (!_waitingResult || result.CardType != cardDataSo.CardType) return;
        
        _waitingResult = false;
        _cardTowerDeployer.OnPlaceResult -= HandlePlaceResult;

        Vector3 localPos = _mapTranslator.ServerToLocal(result.Position, _teamManager.GetLocalTeam());

        _towerPlacementFeedbackManager.StopPredictSpawn(uniqueRuntimeId);
        
        GameLog.Info($"Tower place result: {result.Validation.Reason} at {localPos}");
        
        switch (result.Validation.Reason)
        {
            case TowerReason.Success:
                _clientManaManager.ConfirmSpend(cardDataSo.Cost);
                OccupyPlaceable(localPos);
                RaisePlayResolved(true);
                DiscardSelfCard();
                break;
            case TowerReason.LevelUp:
                _clientManaManager.ConfirmSpend(cardDataSo.Cost);
                RaisePlayResolved(true);
                DiscardSelfCard();
                break;
            case TowerReason.NotSuccess:
                _clientManaManager.RevertSpend(cardDataSo.Cost);
                RaisePlayResolved(false, CardInvalidReason.InvalidTarget);
                break;
            case TowerReason.NotSuccessMaxLevel:
                _clientManaManager.RevertSpend(cardDataSo.Cost);
                RaisePlayResolved(false, CardInvalidReason.TowerMaxLevel);
                break;
            case TowerReason.AlreadyOccupied:
                _clientManaManager.RevertSpend(cardDataSo.Cost);
                RaisePlayResolved(false, CardInvalidReason.DifferentTower);
                break;
            case TowerReason.NotInHand:
                _clientManaManager.RevertSpend(cardDataSo.Cost);
                RaisePlayResolved(false, CardInvalidReason.NotInHand);
                break;
            case TowerReason.NotEnoughMana:
                // The local prediction said yes and the server said no: a race, not an error.
                _clientManaManager.RevertSpend(cardDataSo.Cost);
                RaisePlayResolved(false, CardInvalidReason.NotEnoughMana);
                break;
            default:
                GameLog.Error("UnHandled tower reason: " + result.Validation.Reason);
                _clientManaManager.RevertSpend(cardDataSo.Cost);
                RaisePlayResolved(false, ToCardReason(result.Validation.Reason));
                break;
        }
    }

    private static CardInvalidReason ToCardReason(TowerReason reason) => reason switch
    {
        TowerReason.NotEnoughMana => CardInvalidReason.NotEnoughMana,
        TowerReason.NotInHand => CardInvalidReason.NotInHand,
        _ => CardInvalidReason.InvalidTarget,
    };

    private void OccupyPlaceable(Vector2 worldPosition)
    {
        if (NetworkManager.Singleton.IsHost) return;
        
        IPlaceable closestPlaceable = GetClosestPlaceable(worldPosition);
        if (closestPlaceable == null) return;
        closestPlaceable.Occupy(null);
    }
    
    private bool HasPlaceableNearby(Vector2 origin)
    {
        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, layersSettings.PlaceableRadius, Vector2.zero, 10f, layersSettings.PlaceableLayer);
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider.GetComponentInParent<IPlaceable>() != null)
                return true;
        }
        return false;
    }
    
    private bool IsPlaceableAvailable(Vector2 origin)
    {
        IPlaceable closestPlaceable = GetClosestPlaceable(origin);
        
        if (closestPlaceable == null) return false;
        
        return !closestPlaceable.Occupied;
    }

    private TowerCardDataSO GetTowerCardDataSO()
    {
        if (_cachedTowerCardDataSO != null)
            return _cachedTowerCardDataSO;
        
        if (cardDataSo is not TowerCardDataSO towerCardData)
        {
            GameLog.Error($"CardDataSO: {cardDataSo.CardType} is not TowerCardDataSO");
            return null;
        }
        
        _cachedTowerCardDataSO = towerCardData; 
        return _cachedTowerCardDataSO;
    }

    private TowerDataSO GetTowerDataSO()
    {
        if (_cachedTowerDataSO != null)
            return  _cachedTowerDataSO;
        
        if (cardDataSo is not TowerCardDataSO towerCardData)
        {
            GameLog.Error($"CardDataSO: {cardDataSo.CardType} is not TowerCardDataSO");
            return null;
        }

        if (towerCardData.TowerPrefab.TryGetComponent(out TowerManager towerManager))
        {
            _cachedTowerDataSO = towerManager.Data;
            return _cachedTowerDataSO;
        }
        GameLog.Error($"TowerCardDataSO: {cardDataSo.CardType} prefab doesn't have TowerManager");
        return null;
    }
    
    #if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, layersSettings.PlaceableRadius);
    }
    #endif
}
