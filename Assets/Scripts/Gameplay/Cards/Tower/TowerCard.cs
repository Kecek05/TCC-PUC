using System;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.EventSystems;

public class TowerCard : AbstractCard
{
    [Header("Tower Settings")]
    [SerializeField] private GhostTowerCard ghostTowerCard;
    [Space(5f)]
    
    [Header("GFXs")] 
    [SerializeField] private MMF_Player fadeOutFeedback;
    [SerializeField] private MMF_Player fadeInFeedback;
    [Space(5f)]

    private bool _enabledTowerGFX = false;
    private IPlaceable _currentPlaceable;
    
    private BaseCardTowerDeployer _cardTowerDeployer;

    protected override void Start()
    {
        base.Start();
        _cardTowerDeployer = ServiceLocator.Get<BaseCardTowerDeployer>();
    }

    public override void OnBeginDrag(PointerEventData eventData)
    {
        base.OnBeginDrag(eventData);
        DisableGhostTowerGFX();
        ghostTowerCard.SetSprite(GetTowerCardDataSO().TowerGhostSprite);
    }

    public override void OnDrag(PointerEventData eventData)
    {
        base.OnDrag(eventData);
        
        Vector2 worldPosition = GetWorldPosition(eventData);

        if (IsEnemyMap(worldPosition) || !CanPlayCardAtCanvas(eventData.position))
        {
            DisableGhostTowerGFX();
            return;
        }
        
        AnimateFadeOut();
        EnableGhostTowerGFX(worldPosition);
        
        if (!IsPlaceableAvailable(worldPosition))
        {
            ghostTowerCard.SetVisible(false);
        }
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        base.OnEndDrag(eventData);
        DisableGhostTowerGFX();
    }

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

        TowerCardDataSO towerCardData = GetTowerCardDataSO();
        
        ghostTowerCard.SetVisible(true);
        ghostTowerCard.SetPosition(closestPlaceable.PlaceablePoint.position);
    }

    private IPlaceable GetClosestPlaceable(Vector2 worldPosition)
    {
        RaycastHit2D[] hits = Physics2D.CircleCastAll(worldPosition, layersSettings.PlaceableRadius, Vector2.zero, 10f, layersSettings.PlaceableLayer);

        IPlaceable closest = null;
        float closestDist = float.MaxValue;

        foreach (RaycastHit2D hit in hits)
        {
            TeamIdentifier team = hit.collider.GetComponentInParent<TeamIdentifier>();
            if (team == null || team.TeamType != ServiceLocator.Get<BaseTeamManager>().GetLocalTeam()) continue;

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
        ghostTowerCard.SetVisible(false);
    }

    public override CardValidation CanPlayCardAt(Vector2 worldPosition)
    {
        var baseCheck = base.CanPlayCardAt(worldPosition);
        if (!baseCheck) return baseCheck;

        if (!HasPlaceableNearby(worldPosition) && _currentPlaceable == null)
            return CardValidation.Invalid(CardInvalidReason.InvalidTarget);

        if (IsEnemyMap(worldPosition))
            return CardValidation.Invalid(CardInvalidReason.EnemyMap);
        
        return CardValidation.Valid;
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
        
        Vector2 serverPosition = ServiceLocator.Get<BaseMapTranslator>().LocalToServer(position);
        _cardTowerDeployer.RequestPlaceCardServer(cardDataSo.CardType, serverPosition);
    }

    private void HandlePlaceResult(TowerPlaceResult result)
    {
        if (!_waitingResult || result.CardType != cardDataSo.CardType) return;
        
        _waitingResult = false;
        _cardTowerDeployer.OnPlaceResult -= HandlePlaceResult;

        Vector3 localPos = ServiceLocator.Get<BaseMapTranslator>().ServerToLocal(result.Position, ServiceLocator.Get<BaseTeamManager>().GetLocalTeam());

        _towerPlacementFeedbackManager.StopPredictSpawn(uniqueRuntimeId);
        
        switch (result.Validation.Reason)
        {
            case TowerReason.Success:
                _clientManaManager.ConfirmSpend(cardDataSo.Cost);
                OccupyPlaceable(localPos);
                // Destroy(gameObject);
                break;
            case TowerReason.LevelUp:
                _clientManaManager.ConfirmSpend(cardDataSo.Cost);
                // Destroy(gameObject);
                break;
            case TowerReason.NotSuccess:
                _clientManaManager.RevertSpend(cardDataSo.Cost);
                break;
            case TowerReason.NotSuccessMaxLevel:
                _clientManaManager.RevertSpend(cardDataSo.Cost);
                break;
            default:
                GameLog.Error("UnHandled tower reason: " + result.Validation.Reason);
                _clientManaManager.RevertSpend(cardDataSo.Cost);
                break;
        }
    }

    private void OccupyPlaceable(Vector2 worldPosition)
    {
        if (IsHost) return;
        
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
        if (cardDataSo is not TowerCardDataSO towerCardData)
        {
            GameLog.Error($"CardDataSO: {cardDataSo.CardType} is not TowerCardDataSO");
            return null;
        }

        return towerCardData;
    }
    
    #if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, layersSettings.PlaceableRadius);
    }
    #endif
}
