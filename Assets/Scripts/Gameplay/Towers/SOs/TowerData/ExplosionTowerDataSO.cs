using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "ExplosionTowerData", menuName = "Scriptable Objects/Data/ExplosionTowerData")]
public class ExplosionTowerDataSO : TowerDataSO
{
    [Title("Explosion Tower Data")]
    [Title("Level 1")] 
    public float ExplosionRangeLevel1 = 0.2f;
    
    [Title("Level 2")]
    public float ExplosionRangeLevel2 = 0.3f;
    
    [Title("Level 3")]
    public float ExplosionRangeLevel3 = 0.4f;
    
    public float GetExplosionRangeByLevel(int level)
    {
        switch (level)
        {  
            case 1: return ExplosionRangeLevel1;
            case 2: return ExplosionRangeLevel2;
            case 3: return ExplosionRangeLevel3;
            default: 
                GameLog.Warn($"Invalid tower level {level}. Returning level 1 explosion range.");
                return ExplosionRangeLevel1;
        }
    }
}
