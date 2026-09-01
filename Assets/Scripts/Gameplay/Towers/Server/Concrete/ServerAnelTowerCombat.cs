using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Anel tower. It never shoots: on every cooldown tick it re-scans its radius and holds a damage and
/// attack-speed buff on the ALLY TOWERS inside it, releasing a tower's buff the moment it leaves the
/// radius or the ring itself is removed.
///
/// It is the mirror image of ServerPrismTowerCombat — same acquire-and-release shape, but it changes the
/// defender rather than the attacker. That is what makes it worth nothing on an empty field and enormous on
/// a finished one, and it is why the card asks the player to bunch their towers up: the cluster the Anel
/// wants is exactly the cluster Aríete and Erosão are built to punish.
/// </summary>
/// <remarks>
/// A ring never buffs another ring. Without that rule two Anéis would inflate each other for free and the
/// dominant opening would be a wall of rings, which is a worse game than the one the card is for.
///
/// The applied amounts are stored PER TOWER rather than recomputed, so a placement upgrade that changes the
/// aura strength mid-life can never strand a stack the ring is unable to remove — the same reason Prism
/// stores its slow per enemy.
/// </remarks>
public class ServerAnelTowerCombat : BaseServerTowerCombat
{
    private readonly Dictionary<TowerManager, (float damage, float attackSpeed)> _buffed = new();
    private readonly List<TowerManager> _toRelease = new();

    public override void OnNetworkDespawn()
    {
        // Release before the base unregisters us: a tower must never outlive the ring holding its buff.
        if (IsServer) ReleaseAll();
        base.OnNetworkDespawn();
    }

    protected override bool TryTriggerShot()
    {
        if (_towerData is not AuraTowerDataSO auraData)
        {
            GameLog.Error("ServerAnelTowerCombat: TowerData is not AuraTowerDataSO");
            return false;
        }

        float damageBonus = auraData.GetDamageBonusByLevel(_towerLevel.Value) * _cardScale.EffectBonus;
        float attackSpeedBonus = auraData.GetAttackSpeedBonusByLevel(_towerLevel.Value) * _cardScale.EffectBonus;

        ReleaseTowersOutOfRange();
        AcquireTowersInRange(damageBonus, attackSpeedBonus);

        // Always "fires", so the cooldown paces the re-scan instead of gating a shot.
        return true;
    }

    private void ReleaseTowersOutOfRange()
    {
        _toRelease.Clear();

        foreach (KeyValuePair<TowerManager, (float damage, float attackSpeed)> entry in _buffed)
        {
            TowerManager tower = entry.Key;

            if (!IsAlive(tower))
            {
                _toRelease.Add(tower);
                continue;
            }

            if (Vector2.Distance(transform.position, tower.transform.position) > _range)
                _toRelease.Add(tower);
        }

        for (int i = 0; i < _toRelease.Count; i++)
        {
            TowerManager tower = _toRelease[i];

            if (IsAlive(tower))
            {
                (float damage, float attackSpeed) = _buffed[tower];
                tower.ServerTowerCombat.RemoveDamageBuff(damage);
                tower.ServerTowerCombat.RemoveAttackSpeedBuff(attackSpeed);
            }

            _buffed.Remove(tower);
        }

        _toRelease.Clear();
    }

    private void AcquireTowersInRange(float damageBonus, float attackSpeedBonus)
    {
        TowerRegistry.Cleanup();
        IReadOnlyList<TowerManager> towers = TowerRegistry.ActiveTowers;

        for (int i = towers.Count - 1; i >= 0; i--)
        {
            TowerManager tower = towers[i];

            if (!IsValidTarget(tower)) continue;
            if (_buffed.ContainsKey(tower)) continue;
            if (Vector2.Distance(transform.position, tower.transform.position) > _range) continue;

            tower.ServerTowerCombat.AddDamageBuff(damageBonus);
            tower.ServerTowerCombat.AddAttackSpeedBuff(attackSpeedBonus);
            _buffed[tower] = (damageBonus, attackSpeedBonus);
        }
    }

    private bool IsValidTarget(TowerManager tower)
    {
        if (!IsAlive(tower)) return false;

        // Never itself, and never another ring — see the remarks on the class.
        if (tower == towerManager) return false;
        if (tower.Data != null && tower.Data.TowerType == TowerType.Anel) return false;

        if (tower.Team.GetTeamType() != towerManager.Team.GetTeamType()) return false;

        return true;
    }

    private static bool IsAlive(TowerManager tower) =>
        tower != null && tower.NetworkObject != null && tower.NetworkObject.IsSpawned
        && tower.ServerTowerCombat != null;

    private void ReleaseAll()
    {
        foreach (KeyValuePair<TowerManager, (float damage, float attackSpeed)> entry in _buffed)
        {
            TowerManager tower = entry.Key;
            if (!IsAlive(tower)) continue;

            tower.ServerTowerCombat.RemoveDamageBuff(entry.Value.damage);
            tower.ServerTowerCombat.RemoveAttackSpeedBuff(entry.Value.attackSpeed);
        }

        _buffed.Clear();
    }
}
