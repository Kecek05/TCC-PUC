using UnityEngine;

public class TowerDataHolder : MonoBehaviour
{
    [SerializeField] private TowerDataSO towerData;

    public TowerDataSO TowerData => towerData;

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (towerData == null) return;
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, towerData.Range);
    }
#endif
}
