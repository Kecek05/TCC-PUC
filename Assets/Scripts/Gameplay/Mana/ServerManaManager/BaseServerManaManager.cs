using System;
using Unity.Netcode;

public abstract class BaseServerManaManager : NetworkBehaviour, IMaxManaProvider
{
    public NetworkVariable<float> BlueMana = new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<float> RedMana = new(writePerm: NetworkVariableWritePermission.Server);

    public NetworkVariable<float> BlueMaxMana = new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<float> RedMaxMana = new(writePerm: NetworkVariableWritePermission.Server);

    /// <summary>
    /// Fires on every peer when a team's max mana changes. Consumers subscribe here
    /// instead of depending on the underlying NetworkVariable, keeping them decoupled
    /// from Netcode and from BaseServerManaManager's concrete type.
    /// </summary>
    public event Action<TeamType, float> OnMaxManaChanged;

    public abstract float GetMana(TeamType team);
    public abstract float GetMaxMana(TeamType team);
    public abstract NetworkVariable<float> GetMaxManaNetworkVariable(TeamType team);
    public abstract bool CanAfford(TeamType team, int cost);
    public abstract bool TrySpendMana(TeamType team, int cost);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        BlueMaxMana.OnValueChanged += OnBlueMaxManaSynced;
        RedMaxMana.OnValueChanged += OnRedMaxManaSynced;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        BlueMaxMana.OnValueChanged -= OnBlueMaxManaSynced;
        RedMaxMana.OnValueChanged -= OnRedMaxManaSynced;
    }

    private void OnBlueMaxManaSynced(float previousValue, float newValue) => OnMaxManaChanged?.Invoke(TeamType.Blue, newValue);
    private void OnRedMaxManaSynced(float previousValue, float newValue) => OnMaxManaChanged?.Invoke(TeamType.Red, newValue);
}
