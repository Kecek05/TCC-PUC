using UnityEngine;
using UnityEngine.EventSystems;

public class BuildableCard : AbstractCard
{
    [Header("Buildable Settings")]
    [SerializeField] private float castRadius = 0.5f;
    [SerializeField] private GhostTowerCard ghostTowerCard;

    [Header("GFXs")] 
    [SerializeField] private GameObject cardGFX;
    [SerializeField] private ParticleSystem loadingPlaceEffectPrefab;
    [SerializeField] private ParticleSystem validPlaceEffectPrefab;
    [SerializeField] private ParticleSystem invalidPlaceEffectPrefab;

    private bool _enabledTowerGFX = false;
    private IPlaceable _currentPlaceable;
    
    public override void OnBeginDrag(PointerEventData eventData)
    {
        base.OnBeginDrag(eventData);
        DisableGhostTowerGFX();
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
        
        EnableGhostTowerGFX(worldPosition);
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        base.OnEndDrag(eventData);
        DisableGhostTowerGFX();
    }

    private void AnimateTowerGFX()
    {
        // TODO: Animate Card GFX and Tower GFX Ghost 
    }
    
    private void EnableGhostTowerGFX(Vector2 worldPosition)
    {
        _enabledTowerGFX = true;
        
        IPlaceable closestPlaceable = GetClosestPlaceable(worldPosition);
        
        if (closestPlaceable == null) return;
        _currentPlaceable = closestPlaceable;
        cardGFX.SetActive(false);

        if (cardDataSo is not TowerCardDataSO towerCardData)
        {
            Debug.LogError($"CardDataSO: {cardDataSo.CardType} is not TowerCardDataSO");
            return;
        }
        
        ghostTowerCard.SetSprite(towerCardData.TowerGhostSprite);
        
        ghostTowerCard.SetVisible(true);
        ghostTowerCard.SetPosition(closestPlaceable.PlaceablePoint.position);
    }

    private IPlaceable GetClosestPlaceable(Vector2 worldPosition)
    {
        RaycastHit2D[] hits = Physics2D.CircleCastAll(worldPosition, castRadius, Vector2.zero, 10f, layersSettings.PlaceableLayer);

        IPlaceable closest = null;
        float closestDist = float.MaxValue;

        foreach (RaycastHit2D hit in hits)
        {
            TeamIdentifier team = hit.collider.GetComponentInParent<TeamIdentifier>();
            if (team == null || team.TeamType != TeamManager.Instance.GetLocalTeam()) continue;

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
        _enabledTowerGFX = false;
        _currentPlaceable = null;
        ghostTowerCard.SetVisible(false);
        cardGFX.SetActive(true);
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
        CardTowerDeployer.Instance.OnPlaceResult += HandlePlaceResult;
        ClientManaManager.Instance.PredictSpend(cardDataSo.Cost);

        Vector2 position = worldPosition;
        
        if (_currentPlaceable != null)
        {
            position = _currentPlaceable.PlaceablePoint.position;
        }
        
        ClientVisualEffect(position);
        
        Vector2 serverPosition = MapTranslator.Instance.LocalToServer(position);
        CardTowerDeployer.Instance.RequestPlaceCardServerRpc(cardDataSo.CardType, serverPosition);
    }

    private void HandlePlaceResult(PlaceResult result)
    {
        if (!_waitingResult || result.CardType != cardDataSo.CardType) return;
        Debug.Log($"Received place result for {cardDataSo.CardType}: Valid: {result.Validation.IsValid} - {result.Validation.Reason}");
        
        _waitingResult = false;
        CardTowerDeployer.Instance.OnPlaceResult -= HandlePlaceResult;

        Vector3 localPos = MapTranslator.Instance.ServerToLocal(result.Position, TeamManager.Instance.GetLocalTeam());

        switch (result.Validation.Reason)
        {
            case TowerReason.Success:
                ClientManaManager.Instance.ConfirmSpend(cardDataSo.Cost);
                Instantiate(validPlaceEffectPrefab, localPos, Quaternion.identity);
                // Destroy(gameObject);
                break;
            case TowerReason.LevelUp:
                ClientManaManager.Instance.ConfirmSpend(cardDataSo.Cost);
                // Destroy(gameObject);
                break;
            case TowerReason.NotSuccess:
                ClientManaManager.Instance.RevertSpend(cardDataSo.Cost);
                Instantiate(invalidPlaceEffectPrefab, localPos, Quaternion.identity);
                break;
            case TowerReason.NotSuccessMaxLevel:
                ClientManaManager.Instance.RevertSpend(cardDataSo.Cost);
                break;
            default:
                Debug.LogError("UnHandled tower reason: " + result.Validation.Reason);
                ClientManaManager.Instance.RevertSpend(cardDataSo.Cost);
                Instantiate(invalidPlaceEffectPrefab, localPos, Quaternion.identity);
                break;
        }
    }

    private void ClientVisualEffect(Vector3 position)
    {
        Instantiate(loadingPlaceEffectPrefab, position, Quaternion.identity);
    }
    
    private bool HasPlaceableNearby(Vector2 origin)
    {
        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, castRadius, Vector2.zero, 10f, layersSettings.PlaceableLayer);
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider.GetComponentInParent<IPlaceable>() != null)
                return true;
        }
        return false;
    }
    
    private bool IsEnemyMap(Vector2 position)
    {
        RaycastHit2D[] hits = Physics2D.CircleCastAll(position, castRadius, Vector2.zero, 10f, layersSettings.EnemyMapLayer);
        return hits.Length > 0;
    }
    
    #if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, castRadius);
    }
    #endif
}
