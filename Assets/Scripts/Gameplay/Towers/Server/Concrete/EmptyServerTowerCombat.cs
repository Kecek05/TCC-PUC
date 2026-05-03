using UnityEngine;

public class EmptyServerTowerCombat : BaseServerTowerCombat
{
    protected override bool TryTriggerShot()
    {
        GameLog.Info("Empty Server Tower Combat TryTriggerShot");
        return true;
    }
}
