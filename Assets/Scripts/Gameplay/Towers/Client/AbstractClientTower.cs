using UnityEngine;

public abstract class AbstractClientTower : MonoBehaviour
{
    [SerializeField] private Collider2D _rangeCollider;
    [SerializeField] private Bullet _bulletPrefab;
    
    [SerializeField] private float _shootCooldown = 1f;
    private float _currentShootCooldown = 1f;
    private bool _canShoot = true;

    private void Update()
    {
        if (!_canShoot)
        {
            _currentShootCooldown -= Time.deltaTime;
            if (_currentShootCooldown <= 0f)
            {
                _canShoot = true;
                _currentShootCooldown = _shootCooldown;
            }
        }
    }

    private void shoot(Vector2 direction)
    {
        Bullet bullet = Instantiate(_bulletPrefab, transform.position, Quaternion.identity);
        bullet.SetDirection(direction.normalized);
        _canShoot = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        IDamageable damageable = other.attachedRigidbody.GetComponent<IDamageable>();
        if (damageable != null)
        {
            if (!_canShoot) return;
            shoot(other.attachedRigidbody.transform.position - transform.position);
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        IDamageable damageable = other.attachedRigidbody.GetComponent<IDamageable>();
        if (damageable != null)
        {
            if (!_canShoot) return;
            shoot(other.attachedRigidbody.transform.position - transform.position);
        }
    }
}
