using Unity.Netcode;

public abstract class BaseServerManaManager : NetworkBehaviour
{
    public NetworkVariable<float> BlueMana = new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<float> RedMana = new(writePerm: NetworkVariableWritePermission.Server);

    public NetworkVariable<float> BlueMaxMana = new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<float> RedMaxMana = new(writePerm: NetworkVariableWritePermission.Server);

    public abstract float GetMana(TeamType team);
    public abstract float GetMaxMana(TeamType team);
    public abstract NetworkVariable<float> GetMaxManaNetworkVariable(TeamType team);
    public abstract bool CanAfford(TeamType team, int cost);
    public abstract bool TrySpendMana(TeamType team, int cost);
}
