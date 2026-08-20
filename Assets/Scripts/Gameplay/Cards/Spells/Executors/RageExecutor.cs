using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rage spell. Cast on the enemy field, it drops a buff zone that lasts Duration seconds and grants
/// +MoveSpeedBonus move speed to every troop attacking that field — both scripted-wave enemies and
/// enemies a player sent — while they are inside its radius. The bonus is applied once per troop per cast
/// (a single zone can't self-stack), and it lingers LingerDuration seconds after a troop leaves the radius
/// or after the zone expires. Independent Rage casts are independent sources, so overlapping zones stack
/// additively. Server-authoritative; the faster movement replicates to clients via PathProgress.
/// </summary>
public class RageExecutor : ISpellExecutor
{
    private const float TickInterval = 0.1f;

    public void Execute(SpellExecutionContext context)
    {
        if (context.SpellData is not SpellRageDataSO data)
        {
            GameLog.Error("RageExecutor: SpellData is not SpellRageDataSO");
            return;
        }

        context.CoroutineRunner.StartCoroutine(
            RunRageZone(context.ServerPosition, context.CasterTeam, data, context.Scale));
    }

    private IEnumerator RunRageZone(Vector2 position, TeamType casterTeam, SpellRageDataSO data,
        CardLevelScale scale)
    {
        // Resolved ONCE for the whole cast: AddSpeedBuff and RemoveSpeedBuff must cancel exactly, or a
        // troop keeps a permanent speed stack. Linger is a feel knob and deliberately does not scale.
        float bonus = data.MoveSpeedBonus * scale.EffectBonus;
        float radius = data.Range * scale.Range;
        float duration = data.Duration * scale.Duration;

        yield return new WaitForSeconds(data.TravelTime);

        // troop -> Time.time at which this zone's bonus is removed if it is still outside.
        // Mathf.Infinity means "currently inside" (not yet lingering).
        Dictionary<EnemyManager, float> buffed = new Dictionary<EnemyManager, float>();
        HashSet<EnemyManager> insideNow = new HashSet<EnemyManager>();
        List<EnemyManager> scratch = new List<EnemyManager>();

        float zoneEnd = Time.time + duration;

        while (true)
        {
            float now = Time.time;
            bool zoneAlive = now < zoneEnd;

            // 1. While the zone is alive, (re)apply to every eligible troop currently inside the radius.
            insideNow.Clear();
            if (zoneAlive)
            {
                IReadOnlyList<EnemyManager> enemies = EnemyRegistry.ActiveEnemies;
                for (int i = 0; i < enemies.Count; i++)
                {
                    EnemyManager enemy = enemies[i];
                    if (!IsEligible(enemy, casterTeam)) continue;
                    if (Vector2.Distance(position, enemy.transform.position) > radius) continue;

                    insideNow.Add(enemy);
                    if (!buffed.ContainsKey(enemy))
                        enemy.ServerMovement.AddSpeedBuff(bonus); // apply once per zone
                    buffed[enemy] = Mathf.Infinity;                             // inside: not lingering
                }
            }

            // 2. Reconcile troops no longer inside (left the radius, or the zone expired): start their
            //    linger, then strip this zone's bonus once the linger elapses.
            scratch.Clear();
            foreach (KeyValuePair<EnemyManager, float> kv in buffed) scratch.Add(kv.Key);

            for (int i = 0; i < scratch.Count; i++)
            {
                EnemyManager enemy = scratch[i];

                // Despawned (reached base / killed): its movement and bonus died with it — just forget it.
                if (enemy == null || enemy.NetworkObject == null || !enemy.NetworkObject.IsSpawned)
                {
                    buffed.Remove(enemy);
                    continue;
                }

                if (insideNow.Contains(enemy)) continue; // still covered; deadline stays Infinity

                float deadline = buffed[enemy];
                if (float.IsInfinity(deadline))
                    buffed[enemy] = now + data.LingerDuration;                 // just left: start the linger
                else if (now >= deadline)
                {
                    enemy.ServerMovement.RemoveSpeedBuff(bonus);                // linger elapsed: remove our stack
                    buffed.Remove(enemy);
                }
            }

            if (!zoneAlive && buffed.Count == 0) yield break;

            yield return new WaitForSeconds(TickInterval);
        }
    }

    /// <summary>
    /// Eligible = a troop attacking the targeted (enemy) field: any enemy whose team is neither the
    /// caster's nor None. In this 2-player game that is exactly the opponent's field, and it covers both
    /// wave enemies and player-sent enemies, since both are tagged with the team of the map they walk.
    /// </summary>
    private static bool IsEligible(EnemyManager enemy, TeamType casterTeam)
    {
        if (enemy == null || enemy.NetworkObject == null || !enemy.NetworkObject.IsSpawned) return false;
        TeamType enemyTeam = enemy.Team.GetTeamType();
        return enemyTeam != TeamType.None && enemyTeam != casterTeam;
    }
}
