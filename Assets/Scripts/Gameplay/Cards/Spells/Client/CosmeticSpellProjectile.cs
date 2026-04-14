using DG.Tweening;
using UnityEngine;

public class CosmeticSpellProjectile : MonoBehaviour
{
    [SerializeField] private float spawnHeight = 5f;

    private Vector2 _targetPosition;
    private float _range;

    public void Initialize(Vector2 targetPosition, float travelTime, float range)
    {
        _targetPosition = targetPosition;
        _range = range;

        Vector3 startPos = new Vector3(targetPosition.x, targetPosition.y + spawnHeight, 0f);
        transform.position = startPos;

        transform.DOMove(new Vector3(targetPosition.x, targetPosition.y, 0f), travelTime)
            .SetEase(Ease.InQuad)
            .OnComplete(OnImpact);
    }

    private void OnImpact()
    {
        // TODO: Play explosion VFX / particle system here
        Destroy(gameObject);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(_targetPosition, _range);
    }
#endif
}
