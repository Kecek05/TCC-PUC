using System;
using Unity.Netcode;

public abstract class BaseServerPlayerHealthManager : NetworkBehaviour
{
    public NetworkVariable<float> BlueHealth = new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<float> RedHealth = new(writePerm: NetworkVariableWritePermission.Server);

    public event Action<TeamType> OnPlayerDeath;

    protected void RaisePlayerDeath(TeamType team) => OnPlayerDeath?.Invoke(team);

    public abstract void DamageBase(float damage, TeamType teamType);
}
