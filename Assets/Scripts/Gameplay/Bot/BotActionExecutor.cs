using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Dispatches a chosen ScoredCandidate to the right deployer's server-internal entry point.
/// All deploys are server-owned for bot-driven plays (no real client to assign ownership to).
/// </summary>
public class BotActionExecutor
{
    private readonly BaseCardTowerDeployer _towerDeployer;
    private readonly BaseCardSpellDeployer _spellDeployer;
    private readonly BaseCardSpawnEnemyDeployer _spawnDeployer;

    public BotActionExecutor(
        BaseCardTowerDeployer towerDeployer,
        BaseCardSpellDeployer spellDeployer,
        BaseCardSpawnEnemyDeployer spawnDeployer)
    {
        _towerDeployer = towerDeployer;
        _spellDeployer = spellDeployer;
        _spawnDeployer = spawnDeployer;
    }

    public void Execute(TeamType team, string authId, CardDataSO data, Vector2 position)
    {
        if (data == null) return;

        switch (data)
        {
            case TowerCardDataSO tower:
            {
                TowerPlaceResult r = _towerDeployer.TryPlaceTowerInternal(
                    team, NetworkManager.ServerClientId, tower.CardType, position);
                GameLog.Info($"[Bot {team}] tower {tower.CardType} @ {position} -> {r.Validation.Reason}");
                break;
            }
            case SpellCardDataSO spell:
            {
                SpellSpawnResult r = _spellDeployer.TrySpellInternal(team, spell.CardType, position);
                GameLog.Info($"[Bot {team}] spell {spell.CardType} @ {position} -> {r.Validation.Reason}");
                break;
            }
            case SpawnEnemyCardDataSO spawn:
            {
                SpawnEnemyResult r = _spawnDeployer.TrySpawnEnemyInternal(team, authId, spawn.CardType);
                GameLog.Info($"[Bot {team}] spawn-enemy {spawn.CardType} -> {r.Validation.Reason}");
                break;
            }
            default:
                GameLog.Warn($"[Bot {team}] unhandled card type {data.CardType}");
                break;
        }
    }
}
