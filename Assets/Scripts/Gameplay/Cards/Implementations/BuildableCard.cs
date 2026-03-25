using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

public class BuildableCard : AbstractCard
{
    [Title("Buildable Settings")]
    [SerializeField] private float castRadius = 0.5f;
    [SerializeField] private LayerMask placeableLayer;
    [SerializeField] private ParticleSystem loadingPlaceEffectPrefab;
    [SerializeField] private ParticleSystem validPlaceEffectPrefab;
    [SerializeField] private ParticleSystem invalidPlaceEffectPrefab;

    private bool waitingResult = false;

    public override void ActivateCard(RaycastResult pointerRaycast)
    {
        if (pointerRaycast.gameObject == null) return;
        
        if (!HasPlaceableNearby(pointerRaycast.worldPosition)) return;

        waitingResult = true;
        CardDeployer.Instance.OnPlaceResult += HandlePlaceResult;

        ClientVisualEffect(pointerRaycast.worldPosition);
        Vector2 serverPos = MapTranslator.Instance.LocalToServer(pointerRaycast.worldPosition);
        CardDeployer.Instance.RequestPlaceCardServerRpc(cardDataSo.CardId, serverPos);
    }

    private void HandlePlaceResult(PlaceResult result)
    {
        if (!waitingResult || result.CardId != cardDataSo.CardId) return;
        
        waitingResult = false;
        CardDeployer.Instance.OnPlaceResult -= HandlePlaceResult;

        Vector3 localPos = MapTranslator.Instance.ServerToLocal(result.Position);

        if (result.Success)
        {
            Instantiate(validPlaceEffectPrefab, localPos, Quaternion.identity);
            Destroy(gameObject);
        }
        else
            Instantiate(invalidPlaceEffectPrefab, localPos, Quaternion.identity);

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
