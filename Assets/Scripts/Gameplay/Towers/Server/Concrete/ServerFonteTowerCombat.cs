using UnityEngine;

/// <summary>
/// Fonte tower. It has no target and no projectile: every cooldown it banks mana for its owner. The payout
/// IS its shot, so the shared ShootCooldown column is the tick interval and every existing rate modifier -
/// a placement upgrade, a Sobrecarga, an Anel - speeds up the income for free.
/// </summary>
/// <remarks>
/// Mana is granted through <c>BaseServerManaManager.GrantMana</c> rather than by touching the pools, so the
/// max-mana clamp stays in one place: a Fonte can raise how fast the ceiling is reached, never the ceiling
/// itself. That is the balance rule the card depends on - it accelerates an expensive deck, it does not
/// unlock one.
/// </remarks>
public class ServerFonteTowerCombat : BaseServerTowerCombat
{
    private BaseServerManaManager _manaManager;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer) return;

        _manaManager = ServiceLocator.Get<BaseServerManaManager>();
    }

    protected override bool TryTriggerShot()
    {
        if (_towerData is not ManaTowerDataSO manaData)
        {
            GameLog.Error("ServerFonteTowerCombat: TowerData is not ManaTowerDataSO");
            return false;
        }

        if (_manaManager == null)
        {
            _manaManager = ServiceLocator.Get<BaseServerManaManager>();
            if (_manaManager == null) return false;
        }

        TeamType ownTeam = towerManager.Team.GetTeamType();
        if (ownTeam == TeamType.None) return false;

        // EffectBonus rather than Damage: what the card level buys here is a bigger payout, and this tower
        // has no damage of its own for the damage multiplier to mean anything against.
        float amount = manaData.GetManaPerTickByLevel(_towerLevel.Value) * _cardScale.EffectBonus;
        if (amount <= 0f) return false;

        _manaManager.GrantMana(ownTeam, amount);
        return true;
    }
}
