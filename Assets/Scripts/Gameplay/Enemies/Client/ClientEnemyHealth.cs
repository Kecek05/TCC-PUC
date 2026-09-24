using Unity.Netcode;
using UnityEngine;

public class ClientEnemyHealth : NetworkBehaviour
{
    [SerializeField] private EnemyManager enemyManager;
    [SerializeField] private SpriteRenderer healthBarRenderer;
    [SerializeField] private float tweenDuration = 0.3f;

    private ServerEnemyHealth _serverHealth;
    private MaterialPropertyBlock _propertyBlock;
    private float _currentDisplayHealth;

    // The bar eases from where it is to the new health. Driven by hand rather than by a DOTween per hit: hits
    // are the most frequent event in a match, and a tween plus two closures per hit is garbage on every one.
    // The hit flash on the body belongs to EnemyFeedbackPresenter.
    private float _easeFrom;
    private float _easeTo;
    private float _easeElapsed;

    private static readonly int HealthNormalized = Shader.PropertyToID("_HealthNormalized");

    /// <summary>Scaled by the summoner's card level, so it comes from the server, not the shared SO.</summary>
    private float MaxHealth => _serverHealth != null && _serverHealth.MaxHealth.Value > 0f
        ? _serverHealth.MaxHealth.Value
        : enemyManager.Data.MaxHealth;

    public override void OnNetworkSpawn()
    {
        if (IsServer && !IsClient)
        {
            enabled = false;
            return;
        }

        _propertyBlock ??= new MaterialPropertyBlock();
        _serverHealth = GetComponent<ServerEnemyHealth>();
        _serverHealth.CurrentHealth.OnValueChanged += OnHealthChanged;

        _currentDisplayHealth = Mathf.Clamp01(_serverHealth.CurrentHealth.Value / MaxHealth);
        SetHealthProperty(_currentDisplayHealth);
        enabled = false;
    }

    public override void OnNetworkDespawn()
    {
        if (_serverHealth != null)
            _serverHealth.CurrentHealth.OnValueChanged -= OnHealthChanged;

        enabled = false;
    }

    private void Update()
    {
        _easeElapsed += Time.deltaTime;
        float t = tweenDuration > 0f ? Mathf.Clamp01(_easeElapsed / tweenDuration) : 1f;

        // Ease.OutQuad, as before.
        float eased = 1f - (1f - t) * (1f - t);
        _currentDisplayHealth = Mathf.LerpUnclamped(_easeFrom, _easeTo, eased);
        SetHealthProperty(_currentDisplayHealth);

        if (t >= 1f) enabled = false;
    }

    private void OnHealthChanged(float previousValue, float newValue)
    {
        _easeFrom = _currentDisplayHealth;
        _easeTo = Mathf.Clamp01(newValue / MaxHealth);
        _easeElapsed = 0f;
        enabled = true;
    }

    private void SetHealthProperty(float normalized)
    {
        healthBarRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(HealthNormalized, normalized);
        healthBarRenderer.SetPropertyBlock(_propertyBlock);
    }
}
