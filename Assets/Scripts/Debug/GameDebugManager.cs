using UnityEngine;

public class GameDebugManager : MonoBehaviour
{
    private PlayersDataManager_SinglePlayerDEBUG playersDataManagerDEBUG;
    [SerializeField] private DebugHand debugHand;
    
    public void Awake()
    {
        playersDataManagerDEBUG = new PlayersDataManager_SinglePlayerDEBUG();
        playersDataManagerDEBUG.DebugHand = debugHand;
        
        ServiceLocator.Register<BasePlayersDataManager>(playersDataManagerDEBUG);
    }
}
