using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "TowerData", menuName = "Scriptable Objects/TowerData")]
public class TowerDataSO : ScriptableObject
{
    [Title("Combat")]
    [SerializeField] private float damage = 10f;
    [SerializeField] private float range = 3f;
    [SerializeField] private float shootCooldown = 1f;

    [Title("Visuals")]
    [SerializeField] private Sprite towerSprite;

    public float Damage => damage;
    public float Range => range;
    public float ShootCooldown => shootCooldown;
    public Sprite TowerSprite => towerSprite;
}
