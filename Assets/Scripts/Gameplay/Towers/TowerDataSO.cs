using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "TowerData", menuName = "Scriptable Objects/Data/TowerData")]
public class TowerDataSO : ScriptableObject
{
    [Title("General")]
    [SerializeField] public TowerType TowerType;

    [SerializeField] public int MaxLevel = 3;
    
    [Title("Level 1")]
    [SerializeField] public float DamageLevel1 = 15f;
    [SerializeField] public float RangeLevel1 = 4f;
    [SerializeField] public float ShootCooldownLevel1 = 1f;
    [SerializeField] public float BulletSpeedLevel1 = 15f;
    
    [Title("Level 2")]
    [SerializeField] public float DamageLevel2 = 20f;
    [SerializeField] public float RangeLevel2 = 5f;
    [SerializeField] public float ShootCooldownLevel2 = 0.9f;
    [SerializeField] public float BulletSpeedLevel2 = 20f;
    
    [Title("Level 3")]
    [SerializeField] public float DamageLevel3 = 25f;
    [SerializeField] public float RangeLevel3 = 5.5f;
    [SerializeField] public float ShootCooldownLevel3 = 0.8f;
    [SerializeField] public float BulletSpeedLevel3 = 25f;

    public float GetDamageByLevel(int level)
    {
        switch (level)
        {  
            case 1: return DamageLevel1;
            case 2: return DamageLevel2;
            case 3: return DamageLevel3;
            default: 
                GameLog.Warn($"Invalid tower level {level}. Returning level 1 damage.");
                return DamageLevel1;
        }
    }

    public float GetRangeByLevel(int level)
    {
        switch (level)
        {
            case 1: return RangeLevel1;
            case 2: return RangeLevel2;
            case 3: return RangeLevel3;
            default: 
                GameLog.Warn($"Invalid tower level {level}. Returning level 1 range.");
                return RangeLevel1;
        }
    }

    public float GetShootCooldownByLevel(int level)
    {
        switch (level)
        {
            case 1: return ShootCooldownLevel1;
            case 2: return ShootCooldownLevel2;
            case 3: return ShootCooldownLevel3;
            default: 
                GameLog.Warn($"Invalid tower level {level}. Returning level 1 shoot cooldown.");
                return ShootCooldownLevel1;
        }
    }

    public float GetBulletSpeedByLevel(int level)
    {
        switch (level)
        {
            case 1: return BulletSpeedLevel1;
            case 2: return BulletSpeedLevel2;
            case 3: return BulletSpeedLevel3;
            default: 
                GameLog.Warn($"Invalid tower level {level}. Returning level 1 bullet speed.");
                return BulletSpeedLevel1;
        }
    }
}
