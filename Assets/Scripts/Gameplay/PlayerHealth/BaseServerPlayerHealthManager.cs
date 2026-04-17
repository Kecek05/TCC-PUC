using System;
using Unity.Netcode;

public abstract class BaseServerPlayerHealthManager : NetworkBehaviour
{
    public event Action<TeamType> OnTeamDeath;
    
    public NetworkVariable<float> BlueHealth = new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<float> RedHealth = new(writePerm: NetworkVariableWritePermission.Server);
    
    public abstract void DamageBase(float damage, TeamType teamType);
    
    protected void TriggerOnTeamDeath(TeamType team) => OnTeamDeath?.Invoke(team);
}
