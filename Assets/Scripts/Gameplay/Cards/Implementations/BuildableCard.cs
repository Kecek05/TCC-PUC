using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

public class BuildableCard : AbstractCard
{
    [Title("Buildable Settings")]
    [SerializeField] private float castRadius = 0.5f;
    [SerializeField] private LayerMask placeableLayer;

    //Debug
    private Vector2 lastCastOrigin;
    private float lastClosestDist;
    private bool hasDebugData;

    private void BuildCard(Transform placeablePoint)
    {
        Instantiate(cardDataSo.CardPrefab, placeablePoint.position, Quaternion.identity);
    }

    public override void ActivateCard(RaycastResult pointerRaycast)
    {
        if (pointerRaycast.gameObject == null) return;

        IPlaceable placeable = FindClosestPlaceable(pointerRaycast.worldPosition);
        if (placeable == null) return;

        if (placeable.IsOccupied()) return;

        placeable.Place();
        BuildCard(placeable.PlaceablePoint);
    }

    private IPlaceable FindClosestPlaceable(Vector2 origin)
    {
        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, castRadius, Vector2.zero, 10f, placeableLayer);

        //Debug
        lastCastOrigin = origin;
        lastClosestDist = 0f;
        hasDebugData = true;

        IPlaceable closest = null;
        float closestDist = float.MaxValue;

        foreach (RaycastHit2D hit in hits)
        {
            IPlaceable placeable = hit.collider.GetComponentInParent<IPlaceable>();

            if (placeable == null)  continue;

            float dist = Vector2.Distance(origin, hit.collider.transform.position);
            if (!(dist < closestDist)) continue;

            closestDist = dist;
            closest = placeable;
        }

        lastClosestDist = closestDist == float.MaxValue ? 0f : closestDist;
        return closest;
    }
    
    #if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!hasDebugData) return;
        
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(lastCastOrigin, castRadius);
        
        if (lastClosestDist > 0f)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(lastCastOrigin, lastClosestDist);
        }
    }
    #endif
}
