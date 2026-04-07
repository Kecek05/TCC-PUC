using UnityEngine;

/// <summary>
/// Local-only visual bullet. No collider, no physics, no NetworkObject.
/// Lerps from origin to target position, plays impact VFX, then returns to pool.
/// Damage is already applied on the server — this is purely eye candy.
/// </summary>
public class CosmeticBullet : MonoBehaviour
{
    private Vector3 _origin;
    private Transform _target;
    private Vector3 _lastTargetPos;
    private float _journeyLength;
    private float _distanceTraveled;
    private bool _active;

    private float _lastSpeed;
    
    private CosmeticBulletPool _pool;

    public void Initialize(CosmeticBulletPool pool)
    {
        _pool = pool;
    }

    public void Fire(Vector3 origin, Transform target, float bulletSpeed)
    {
        _origin = origin;
        _target = target;
        _lastTargetPos = target != null ? target.position : origin;
        _journeyLength = Vector3.Distance(_origin, _lastTargetPos);
        _distanceTraveled = 0f;
        _lastSpeed =  bulletSpeed;

        transform.position = origin;
        LookAtTarget();
        
        gameObject.SetActive(true);
        _active = true;
    }

    private void Update()
    {
        if (!_active) return;

        // Track the moving target if it's still alive
        if (_target != null)
            _lastTargetPos = _target.position;

        _distanceTraveled += _lastSpeed * Time.deltaTime;
        _journeyLength = Vector3.Distance(_origin, _lastTargetPos);

        if (_journeyLength <= 0.01f)
        {
            Complete();
            return;
        }

        float t = Mathf.Clamp01(_distanceTraveled / _journeyLength);
        transform.position = Vector3.Lerp(_origin, _lastTargetPos, t);
        LookAtTarget();

        if (t >= 1f)
            Complete();
    }

    private void LookAtTarget()
    {
        Vector3 dir = _lastTargetPos - transform.position;
        if (dir.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
        }
    }

    private void Complete()
    {
        _active = false;
        // TODO: Play impact VFX / particle at _lastTargetPos

        if (_pool != null)
            _pool.Return(this);
        else
            gameObject.SetActive(false);
    }
}
