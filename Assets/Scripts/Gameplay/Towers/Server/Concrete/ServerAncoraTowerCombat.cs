using System.Collections;
using UnityEngine;

/// <summary>
/// Ancora tower. It deals no damage. On each cooldown it grabs the single most advanced enemy in range,
/// drags it back down its own lane and pins it there for a fixed time, then lets go and looks for the next
/// one. Exactly one enemy is ever held, which is both the card's strength and its ceiling: it buys a wave's
/// worth of time against one body and does nothing at all against a swarm.
/// </summary>
/// <remarks>
/// A hold is not a slow. Slows accumulate into <c>ServerEnemyMovement</c>'s capped total precisely so that
/// control can never fully replace damage; the Ancora is the deliberate exception, which is why it goes
/// through the separate <c>AddHold</c>/<c>RemoveHold</c> pair instead.
///
/// The pull is backwards ALONG the lane rather than toward the tower. Enemies here are pinned to a path,
/// not free agents, so dragging one off it is not expressible - but pulling it back down the path produces
/// the effect the card is actually after, which is the followers piling up behind it.
///
/// The grip is released by the coroutine that took it, and again on despawn, so an enemy can never outlive
/// the tower holding it.
/// </remarks>
public class ServerAncoraTowerCombat : BaseServerTowerCombat
{
    private EnemyManager _held;
    private Coroutine _holdRoutine;

    public override void OnNetworkDespawn()
    {
        // Let go before the base unregisters us - a held enemy must never outlive its anchor.
        if (IsServer) ReleaseHeld();
        base.OnNetworkDespawn();
    }

    protected override bool TryTriggerShot()
    {
        // Still gripping someone: the cooldown keeps ticking but there is nothing to take.
        if (_held != null) return false;

        if (_towerData is not AnchorTowerDataSO anchorData)
        {
            GameLog.Error("ServerAncoraTowerCombat: TowerData is not AnchorTowerDataSO");
            return false;
        }

        EnemyManager target = FindClosestEnemyToEnd();
        if (target == null) return false;

        float holdDuration = anchorData.GetHoldDurationByLevel(_towerLevel.Value) * _cardScale.Duration;
        float pullDistance = anchorData.GetPullDistanceByLevel(_towerLevel.Value) * _cardScale.Range;

        _held = target;
        target.ServerMovement.PullBack(pullDistance);
        target.ServerMovement.AddHold();

        _holdRoutine = StartCoroutine(HoldRoutine(target, holdDuration));
        return true;
    }

    private IEnumerator HoldRoutine(EnemyManager target, float duration)
    {
        yield return new WaitForSeconds(duration);

        // The enemy may have died mid-hold; a despawn already reset its own counter, so only a live one
        // needs the explicit release.
        if (IsAlive(target)) target.ServerMovement.RemoveHold();

        if (_held == target) _held = null;
        _holdRoutine = null;

        // Restart the cadence on RELEASE, not on the grab. TryTriggerShot returns false for the whole hold,
        // which leaves the cooldown sitting full, so without this the anchor would re-grab on the very next
        // frame and the ShootCooldown column would never gate anything - an unbroken lock with no window
        // for the attacker to push through.
        _currentShootCooldown = 0f;
    }

    private void ReleaseHeld()
    {
        if (_holdRoutine != null)
        {
            StopCoroutine(_holdRoutine);
            _holdRoutine = null;
        }

        if (IsAlive(_held)) _held.ServerMovement.RemoveHold();
        _held = null;
    }

    private static bool IsAlive(EnemyManager enemy) =>
        enemy != null && enemy.NetworkObject != null && enemy.NetworkObject.IsSpawned;
}
