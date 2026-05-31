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
    [SerializeField] private float reducer = 0.8f;
    [SerializeField] private TextMeshProUGUI manaText;

    private BaseTeamManager _teamManager;
    private BaseServerManaManager _serverManaManager;
    private NetworkVariable<float> _maxMana;
    private BaseClientManaManager _clientManaManager;
    
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
    }

    private void Update()
    {
        if (_clientManaManager == null) return;
        GameLog.Info($"Updating mana UI. Predicted mana: {_clientManaManager.PredictedMana} - {1 - ((_clientManaManager.PredictedMana / _maxMana.Value) * reducer)}");
        manaFill.SetDashFill(1 - ((_clientManaManager.PredictedMana / _maxMana.Value) * reducer));
        manaText.text = $"{Mathf.FloorToInt(_clientManaManager.PredictedMana)}";
    }

    private void OnServerMaxManaChanged(float previousValue, float newValue)
    {
        manaBackgroundFill.SetDashCount(newValue);
    }
}
