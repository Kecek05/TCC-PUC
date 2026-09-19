using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public abstract class BaseServerPlayerHealthManager : NetworkBehaviour
{
    public event Action<TeamType> OnTeamDeath;

    public NetworkVariable<float> BlueHealth = new(writePerm: NetworkVariableWritePermission.Server);
    public NetworkVariable<float> RedHealth = new(writePerm: NetworkVariableWritePermission.Server);

    /// <summary>The lowest each team's base may be brought to. Absent means 0 — the normal rule.</summary>
    private readonly Dictionary<TeamType, float> _healthFloors = new();

    public abstract void DamageBase(float damage, TeamType teamType);

    public abstract NetworkVariable<float> GetLocalHealth();
    public abstract NetworkVariable<float> GetEnemyHealth();

    /// <summary>
    /// Server-only. Stops <paramref name="team"/>'s base being damaged below <paramref name="floor"/>, so any
    /// floor above zero means that base cannot die. What guarantees the tutorial's first match is never
    /// lost; 0 restores the normal rule.
    /// </summary>
    public void SetHealthFloor(TeamType team, float floor) => _healthFloors[team] = Mathf.Max(0f, floor);

    /// <summary>How far a hit may take <paramref name="team"/>'s base — 0 unless a floor was set.</summary>
    protected float HealthFloor(TeamType team) => _healthFloors.TryGetValue(team, out float floor) ? floor : 0f;

    protected void TriggerOnTeamDeath(TeamType team) => OnTeamDeath?.Invoke(team);
}
