using UnityEngine;

/// <summary>
/// Simple world-space health bar for enemies. Attach to a child GameObject
/// with a SpriteRenderer (fill bar). Driven by ClientEnemyHealth.
/// </summary>
public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] private Transform fillBar;
    [SerializeField] private Vector3 offset = new(0f, 0.5f, 0f);

    private Transform _followTarget;
    private float _maxHealth;
    private bool _initialized;

    public void Initialize(Transform target, float maxHealth)
    {
        _followTarget = target;
        _maxHealth = maxHealth;
        _initialized = true;
        SetFill(1f);
    }

    public void SetHealth(float currentHealth)
    {
        if (!_initialized || _maxHealth <= 0f) return;
        SetFill(Mathf.Clamp01(currentHealth / _maxHealth));
    }

    private void SetFill(float normalizedHealth)
    {
        if (fillBar == null) return;
        fillBar.localScale = new Vector3(normalizedHealth, fillBar.localScale.y, fillBar.localScale.z);
    }

    private void LateUpdate()
    {
        if (!_initialized || _followTarget == null) return;
        transform.position = _followTarget.position + offset;
    }
}
