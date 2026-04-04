using UnityEngine;

public class BuildableCard : AbstractCard
{
    [Header("Buildable Settings")]
    [SerializeField] private float castRadius = 0.5f;
    [SerializeField] private LayerMask placeableLayer;
    [SerializeField] private LayerMask blockedLayer;
    
    [SerializeField] private ParticleSystem loadingPlaceEffectPrefab;
    [SerializeField] private ParticleSystem validPlaceEffectPrefab;
    [SerializeField] private ParticleSystem invalidPlaceEffectPrefab;

    private bool _waitingResult;

    public override CardValidation CanPlayCard()
    {
        if (_waitingResult) return CardValidation.Invalid(CardInvalidReason.WaitingForServer);

        return base.CanPlayCard();
    }

    public override CardValidation CanPlayCardAt(Vector2 worldPosition)
    {
        var baseCheck = base.CanPlayCardAt(worldPosition);
        if (!baseCheck) return baseCheck;

        if (!HasPlaceableNearby(worldPosition))
            return CardValidation.Invalid(CardInvalidReason.InvalidTarget);

        return CardValidation.Valid;
    }

    public override void ActivateCard(Vector2 worldPosition)
    {
        _waitingResult = true;
        CardTowerDeployer.Instance.OnPlaceResult += HandlePlaceResult;
        ClientManaManager.Instance.PredictSpend(cardDataSo.Cost);

        ClientVisualEffect(worldPosition);
        Vector2 serverPos = MapTranslator.Instance.LocalToServer(worldPosition);
        CardTowerDeployer.Instance.RequestPlaceCardServerRpc(cardDataSo.CardType, serverPos);
    }

    private void HandlePlaceResult(PlaceResult result)
    {
        if (!_waitingResult || result.CardType != cardDataSo.CardType) return;
        
        _waitingResult = false;
        CardTowerDeployer.Instance.OnPlaceResult -= HandlePlaceResult;

        Vector3 localPos = MapTranslator.Instance.ServerToLocal(result.Position, TeamManager.Instance.GetLocalTeam());

        switch (result.Validation.Reason)
        {
            case TowerReason.Success:
                ClientManaManager.Instance.ConfirmSpend(cardDataSo.Cost);
                Instantiate(validPlaceEffectPrefab, localPos, Quaternion.identity);
                Destroy(gameObject);
                break;
            case TowerReason.LevelUp:
                ClientManaManager.Instance.ConfirmSpend(cardDataSo.Cost);
                Destroy(gameObject);
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
        var hits = Physics2D.CircleCastAll(origin, castRadius, Vector2.zero, 10f, placeableLayer);
        foreach (var hit in hits)
        {
            if (hit.collider.GetComponentInParent<IPlaceable>() != null)
                return true;
        }
        return false;
    }
    
    #if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, castRadius);
    }
    #endif
}
