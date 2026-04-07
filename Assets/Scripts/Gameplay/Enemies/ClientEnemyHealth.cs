using DG.Tweening;
using Unity.Netcode;
using UnityEngine;

public class ClientEnemyHealth : NetworkBehaviour
{
    [SerializeField] private EnemyManager enemyManager;
    [SerializeField] private SpriteRenderer healthBarRenderer;
    [SerializeField] private float tweenDuration = 0.3f;
    [SerializeField] private Ease tweenEase = Ease.OutQuad;
    
    private ServerEnemyHealth _serverHealth;
    private MaterialPropertyBlock _propertyBlock;
    private Tweener _healthTween;
    private float _currentDisplayHealth;

    private static readonly int HealthNormalized = Shader.PropertyToID("_HealthNormalized");

    public override void OnNetworkSpawn()
    {
        if (IsServer && !IsClient)
        {
            enabled = false;
            return;
        }

        _propertyBlock = new MaterialPropertyBlock();
        _serverHealth = GetComponent<ServerEnemyHealth>();
        _serverHealth.CurrentHealth.OnValueChanged += OnHealthChanged;

        _currentDisplayHealth = Mathf.Clamp01(_serverHealth.CurrentHealth.Value / enemyManager.Data.MaxHealth);
        SetHealthProperty(_currentDisplayHealth);
    }

    public override void OnNetworkDespawn()
    {
        if (_serverHealth != null)
            _serverHealth.CurrentHealth.OnValueChanged -= OnHealthChanged;

        _healthTween?.Kill();
    }

    private void OnHealthChanged(float previousValue, float newValue)
    {
        float target = Mathf.Clamp01(newValue / enemyManager.Data.MaxHealth);

        _healthTween?.Kill();
        _healthTween = DOTween.To(
            () => _currentDisplayHealth,
            x =>
            {
                _currentDisplayHealth = x;
                SetHealthProperty(x);
            },
            target,
            tweenDuration
        ).SetEase(tweenEase);
    }

    private void SetHealthProperty(float normalized)
    {
        healthBarRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(HealthNormalized, normalized);
        healthBarRenderer.SetPropertyBlock(_propertyBlock);
    }
}
