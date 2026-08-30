using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Beacon tower. A long-range beam that ticks fast and grows stronger the longer it stays locked on one
/// enemy. Anything that forces it to switch target — a swarm, a decoy — throws the ramp away, which is the
/// entire reason it is the best answer to a boss and a bad answer to everything else.
/// </summary>
public class ServerBeaconTowerCombat : BaseServerTowerCombat
{
    [Title("Beacon Tower Combat References")]
    [SerializeField] private ClientCircleTowerCombat clientCircleCombat;

    private float _rampPerSecond;
    private float _maxRamp = 1f;

    private EnemyManager _rampTarget;
    private float _rampMultiplier = 1f;
    private float _lastTickTime;

    protected override void UpdateData()
    {
        base.UpdateData();

        if (_towerData is not BeaconTowerDataSO beaconData)
        {
            GameLog.Error($"TowerDataSO for {GetType().Name} is not of type BeaconTowerDataSO");
            return;
        }

        _rampPerSecond = beaconData.RampPerSecond;
        _maxRamp = Mathf.Max(1f, beaconData.MaxRampMultiplier);
        _rampMultiplier = Mathf.Min(_rampMultiplier, _maxRamp);
    }

    protected override bool TryTriggerShot()
    {
        EnemyManager target = FindClosestEnemyToEnd();

        if (target == null)
        {
            // The beam broke. Returning false leaves the cooldown ready, so it re-acquires the instant
            // something walks in — at ramp 1, exactly as if it had never fired.
            ResetRamp();
            return false;
        }

        if (target != _rampTarget)
        {
            _rampTarget = target;
            _rampMultiplier = 1f;
        }
        else
        {
            // Measured in real seconds rather than in ticks, so a haste buff shortening the cooldown makes
            // the beam ramp in the same wall-clock time instead of secretly ramping faster.
            float held = Time.time - _lastTickTime;
            _rampMultiplier = Mathf.Min(_maxRamp, _rampMultiplier + _rampPerSecond * held);
        }

        _lastTickTime = Time.time;

        DealDamage(target, _damage * _rampMultiplier);

        clientCircleCombat.FireBulletRpc(
            transform.position,
            _bulletSpeed,
            target.GetComponent<NetworkObject>()
        );

        return true;
    }

    private void ResetRamp()
    {
        _rampTarget = null;
        _rampMultiplier = 1f;
    }
}
