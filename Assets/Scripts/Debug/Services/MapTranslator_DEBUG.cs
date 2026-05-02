using UnityEngine;

public class MapTranslator_DEBUG : BaseMapTranslator
{
    [SerializeField] private float mapOffset = 11f;
    
    private void Awake()
    {
        ServiceLocator.Register<BaseMapTranslator>(this);
    }
    
    private bool isInitialized = true;
    
    private bool bothPlayersInitialized = true;

    public override bool IsInitialized => isInitialized;
    public override bool BothPlayersInitialized => bothPlayersInitialized;
    public override Vector3 LocalToServer(Vector3 localPos)
    {
        return localPos;
    }

    public override Vector3 ServerToLocal(Vector3 serverPos, TeamType teamType)
    {
        return new Vector3(serverPos.x, serverPos.y, serverPos.z);
    }
}
