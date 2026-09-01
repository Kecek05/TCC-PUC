using UnityEngine;

/// <summary>
/// Espelho tower. It has no target on its own field: every cooldown it sends a weak enemy down the
/// OPPONENT's lane. A defensive slot spent on permanent, unattended offence - which is why it neither
/// defends nor deals damage at home, and why it only pays off in a match long enough for the drip to add up.
/// </summary>
/// <remarks>
/// It reaches the wave manager directly rather than through <c>SendEnemyFromPlayer</c>, because that
/// resolves the destination from a sending player's auth id and a tower only knows its own team. The
/// destination here is simply the other map.
///
/// Sends carry the tower's own card scale, so a levelled Espelho sends tougher bodies - consistent with
/// every other card whose output scales, and the reason the send goes through the same cardScale parameter
/// a player-summoned troop uses.
/// </remarks>
public class ServerEspelhoTowerCombat : BaseServerTowerCombat
{
    private BaseServerWaveManager _waveManager;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer) return;

        _waveManager = ServiceLocator.Get<BaseServerWaveManager>();
    }

    protected override bool TryTriggerShot()
    {
        if (_towerData is not MirrorTowerDataSO mirrorData)
        {
            GameLog.Error("ServerEspelhoTowerCombat: TowerData is not MirrorTowerDataSO");
            return false;
        }

        if (mirrorData.SentEnemy == null)
        {
            GameLog.Error("ServerEspelhoTowerCombat: SentEnemy is not set on the tower data.");
            return false;
        }

        if (_waveManager == null)
        {
            _waveManager = ServiceLocator.Get<BaseServerWaveManager>();
            if (_waveManager == null) return false;
        }

        TeamType ownTeam = towerManager.Team.GetTeamType();
        if (ownTeam == TeamType.None) return false;

        TeamType targetMap = ownTeam == TeamType.Blue ? TeamType.Red : TeamType.Blue;

        int count = mirrorData.GetSendCountByLevel(_towerLevel.Value);
        for (int i = 0; i < count; i++)
        {
            // fromPlayer: true - the send is a player's doing, so it walks the lane the same direction a
            // card-summoned troop does and stays out of the victim's wave bookkeeping.
            _waveManager.SpawnEnemy(mirrorData.SentEnemy, targetMap, true, _cardScale);
        }

        return true;
    }
}
