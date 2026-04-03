using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "TowerData", menuName = "Scriptable Objects/TowerData")]
public class TowerDataSO : ScriptableObject
{
    [Title("General")]
    [SerializeField] private TowerType towerType;
    
    [Title("Level 1")]
    [SerializeField] private float damageLevel1 = 15f;
    [SerializeField] private float rangeLevel1 = 4f;
    [SerializeField] private float shootCooldownLevel1 = 1f;
    [SerializeField] private float bulletSpeedLevel1 = 15f;
    
    [Title("Level 2")]
    [SerializeField] private float damageLevel2 = 20f;
    [SerializeField] private float rangeLevel2 = 5f;
    [SerializeField] private float shootCooldownLevel2 = 0.9f;
    [SerializeField] private float bulletSpeedLevel2 = 20f;
    
    [Title("Level 3")]
    [SerializeField] private float damageLevel3 = 25f;
    [SerializeField] private float rangeLevel3 = 5.5f;
    [SerializeField] private float shootCooldownLevel3 = 0.8f;
    [SerializeField] private float bulletSpeedLevel3 = 25f;
    
    public TowerType TowerType => towerType;
    public float DamageLevel1 => damageLevel1;
    public float RangeLevel1 => rangeLevel1;
    public float ShootCooldownLevel1 => shootCooldownLevel1;
    public float BulletSpeedLevel1 => bulletSpeedLevel1;
    
    public float DamageLevel2 => damageLevel2;
    public float RangeLevel2 => rangeLevel2;
    public float ShootCooldownLevel2 => shootCooldownLevel2;
    public float BulletSpeedLevel2 => bulletSpeedLevel2;
    
    public float DamageLevel3 => damageLevel3;
    public float RangeLevel3 => rangeLevel3;
    public float ShootCooldownLevel3 => shootCooldownLevel3;
    public float BulletSpeedLevel3 => bulletSpeedLevel3;

    public float GetDamageByLevel(int level)
    {
        switch (level)
        {  
            case 1: return damageLevel1;
            case 2: return damageLevel2;
            case 3: return damageLevel3;
            default: 
                Debug.LogWarning($"Invalid tower level {level}. Returning level 1 damage.");
                return damageLevel1;
        }
    }

    public float GetRangeByLevel(int level)
    {
        switch (level)
        {
            case 1: return rangeLevel1;
            case 2: return rangeLevel2;
            case 3: return rangeLevel3;
            default: 
                Debug.LogWarning($"Invalid tower level {level}. Returning level 1 range.");
                return rangeLevel1;
        }
    }

    public float GetShootCooldownByLevel(int level)
    {
        switch (level)
        {
            case 1: return shootCooldownLevel1;
            case 2: return shootCooldownLevel2;
            case 3: return shootCooldownLevel3;
            default: 
                Debug.LogWarning($"Invalid tower level {level}. Returning level 1 shoot cooldown.");
                return shootCooldownLevel1;
        }
    }

    public float GetBulletSpeedByLevel(int level)
    {
        switch (level)
        {
            case 1: return bulletSpeedLevel1;
            case 2: return bulletSpeedLevel2;
            case 3: return bulletSpeedLevel3;
            default: 
                Debug.LogWarning($"Invalid tower level {level}. Returning level 1 bullet speed.");
                return bulletSpeedLevel1;
        }
    }
}
