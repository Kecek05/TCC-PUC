using System;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    [SerializeField] private float _speed = 5f;
    [SerializeField] private Rigidbody2D _rigidbody2D;

    private bool _damaged = false;
    
    private void Start()
    {
        Destroy(gameObject, 4f);
    }

    public void SetDirection(Vector2 direction)
    {
        gameObject.transform.up = direction;
        _rigidbody2D.linearVelocity = direction * _speed;
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        IDamageable damageable = other.rigidbody.GetComponent<IDamageable>();
        if (damageable != null)
        {
            if (_damaged) return;
            _damaged = true;
            damageable.TakeDamage(10f);
        }
    }
}
