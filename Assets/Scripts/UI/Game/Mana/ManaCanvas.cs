using System.Collections;
using Sirenix.OdinInspector;
using TMPro;
using UI.Game.Shapes.ImmediateComponents;
using Unity.Netcode;
using UnityEngine;

public class ManaCanvas : MonoBehaviour
{
    [Title("References")] 
    [SerializeField] private RectangleImmediateUI manaFill;
    [SerializeField] private RectangleImmediateUI manaBackgroundFill;
    [SerializeField] private TextMeshProUGUI manaText;

    private BaseTeamManager _teamManager;
    private BaseServerManaManager _serverManaManager;
    private NetworkVariable<float> _maxMana;
    private BaseClientManaManager _clientManaManager;
    
    //Bar
    private readonly float _barGap = 0.15f;
    private readonly float _iconWidth = 1f;
    
    private float _filled;
    private float _fraction;
    private float _fillPos;
    private float _fillTotal;
    private float _barFill;

    private bool _initialized = false;
    
    private void Start()
    {
        _teamManager = ServiceLocator.Get<BaseTeamManager>();
        _serverManaManager = ServiceLocator.Get<BaseServerManaManager>();
        _clientManaManager =  ServiceLocator.Get<BaseClientManaManager>();
        
        StartCoroutine(WaitForInitialization());
    }

    private void OnDestroy()
    {
        if (_maxMana != null)
            _maxMana.OnValueChanged -= OnServerMaxManaChanged;
    }

    private IEnumerator WaitForInitialization()
    {
        _teamManager = ServiceLocator.Get<BaseTeamManager>();
        _serverManaManager = ServiceLocator.Get<BaseServerManaManager>();

        yield return new WaitUntil(() =>
            _teamManager != null &&
            _teamManager.HasLocalTeamBeenAssigned() &&
            _serverManaManager != null);

        _maxMana = _serverManaManager.GetMaxManaNetworkVariable(_teamManager.GetLocalTeam());

        _maxMana.OnValueChanged += OnServerMaxManaChanged;

        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized) return;
        if (_clientManaManager == null) return;
        
        _filled = Mathf.Floor(_clientManaManager.PredictedMana);
        _fraction = _clientManaManager.PredictedMana - _filled;
        
        _fillPos = _filled * (_iconWidth + _barGap) + _fraction * _iconWidth;
        _fillTotal = _maxMana.Value * (_iconWidth + _barGap);
        _barFill = 1f - (_fillPos / _fillTotal);
        
        manaFill.SetDashFill(_barFill);
        manaText.text = $"{Mathf.FloorToInt(_clientManaManager.PredictedMana)}";
    }

    private void OnServerMaxManaChanged(float previousValue, float newValue)
    {
        manaBackgroundFill.SetDashCount(newValue);
    }
}
