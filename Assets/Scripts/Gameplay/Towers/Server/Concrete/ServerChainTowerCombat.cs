using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Chain tower. The bolt hits a primary target and then jumps to the nearest enemy it has not hit yet,
/// losing damage every jump. Once it is inside the wave it travels along the wave, not back to the tower, so
/// its value scales with how tightly packed the wave is — and against one lone enemy it is simply the most
/// expensive single-target shot in the set.
/// </summary>
public class ServerChainTowerCombat : BaseServerTowerCombat
{
    [Title("Chain Tower Combat References")]
    [SerializeField] private ClientCircleTowerCombat clientCircleCombat;

    private int _maxHops;
    private float _hopRadius;
    private float _falloff;

    private readonly HashSet<EnemyManager> _alreadyHit = new();

    // Where each hit of the current chain landed, in order, for the one Rpc that draws it.
    private readonly List<Vector2> _chainNodes = new();
    private readonly List<Vector3> _localNodes = new();
    private ClientTowerGFX _gfx;

    protected override void UpdateData()
    {
        base.UpdateData();

        if (_towerData is not ChainTowerDataSO chainData)
        {
            GameLog.Error($"TowerDataSO for {GetType().Name} is not of type ChainTowerDataSO");
            return;
        }

        _maxHops = chainData.GetMaxHopsByLevel(_towerLevel.Value);
        _hopRadius = chainData.HopRadius * _cardScale.Range;
        _falloff = Mathf.Clamp01(chainData.DamageFalloffPercent);
    }

    protected override bool TryTriggerShot()
    {
        EnemyManager target = FindClosestEnemyToEnd();
        if (target == null) return false;

        float distance = Vector2.Distance(transform.position, target.transform.position);
        float travelTime = distance / _bulletSpeed;

        StartCoroutine(ApplyChainAfterDelay(target, _damage, travelTime));

        // The first leg is an ordinary bullet. The jumps are server-resolved in one frame when it lands, and are
        // drawn from the single Rpc ApplyChainAfterDelay sends with the whole path (never an Rpc per jump).
        clientCircleCombat.FireBulletRpc(
            transform.position,
            _bulletSpeed,
            target.GetComponent<NetworkObject>()
        );

        return true;
    }

    private IEnumerator ApplyChainAfterDelay(EnemyManager first, float damage, float delay)
    {
        yield return new WaitForSeconds(delay);

        _alreadyHit.Clear();
        _chainNodes.Clear();

        EnemyManager current = first;
        float currentDamage = damage;

        // One primary plus _maxHops jumps.
        for (int hop = 0; hop <= _maxHops; hop++)
        {
            if (!IsValidEnemy(current)) break;

            Vector2 from = current.transform.position;

            DealDamage(current, currentDamage);
            _alreadyHit.Add(current);
            _chainNodes.Add(from);

            currentDamage *= 1f - _falloff;
            if (currentDamage <= 0.01f) break;

            current = FindNearestUnhit(from);
        }

        _alreadyHit.Clear();

        // A lone target is just a shot: there is a chain to draw only if the bolt jumped at least once.
        if (_chainNodes.Count >= 2) PlayChainRpc(_chainNodes.ToArray());
    }

    /// <summary>
    /// The path the bolt took through the wave, hit by hit, for every client to draw (ChainLightningFx). Positions
    /// rather than enemy references: the chain's kills are despawning as this arrives, and a reference to one of
    /// them would no longer resolve. Server space on the wire, translated here the way a bullet's origin is.
    /// </summary>
    /// <remarks>
    /// On the server class, unlike the bullet's Rpc, because this tower has no client combat of its own (it borrows
    /// <see cref="ClientCircleTowerCombat"/> for the first leg) and the chain is the one thing only it does.
    /// </remarks>
    [Rpc(SendTo.ClientsAndHost)]
    private void PlayChainRpc(Vector2[] serverNodes)
    {
        if (serverNodes == null || serverNodes.Length < 2) return;

        ServiceLocator.TryGet(out BaseMapTranslator translator);
        bool translate = translator != null && translator.IsInitialized && towerManager.Team != null;
        TeamType team = towerManager.Team != null ? towerManager.Team.GetTeamType() : TeamType.None;

        _localNodes.Clear();
        foreach (Vector2 node in serverNodes)
        {
            Vector3 local = translate ? translator.ServerToLocal(node, team) : (Vector3)node;
            _localNodes.Add(new Vector3(local.x, local.y, 0f));
        }

        if (_gfx == null) _gfx = towerManager.GetComponentInChildren<ClientTowerGFX>();
        Color tint = _gfx != null ? _gfx.Tint : Color.white;
        float falloff = towerManager.Data is ChainTowerDataSO chain ? chain.DamageFalloffPercent : 0.25f;
        ChainLightningFx.Play(_localNodes, tint, falloff);
    }

    private EnemyManager FindNearestUnhit(Vector2 from)
    {
        EnemyRegistry.Cleanup();
        IReadOnlyList<EnemyManager> active = EnemyRegistry.ActiveEnemies;

        EnemyManager closest = null;
        float closestDist = float.MaxValue;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            EnemyManager enemy = active[i];
            if (!IsValidEnemy(enemy)) continue;
            if (_alreadyHit.Contains(enemy)) continue;

            float dist = Vector2.Distance(from, enemy.transform.position);
            if (dist > _hopRadius || dist >= closestDist) continue;

            closest = enemy;
            closestDist = dist;
        }

        return closest;
    }
}
