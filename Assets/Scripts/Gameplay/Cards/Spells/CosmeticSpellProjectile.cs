using DG.Tweening;
using UnityEngine;

public class CosmeticSpellProjectile : MonoBehaviour
{
    [SerializeField] private float spawnHeight = 5f;

    public void Initialize(Vector2 targetPosition, float travelTime)
    {
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
}
