using System;
using System.Collections;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class ManaCanvas : MonoBehaviour
{
    [Title("References")] 
    [SerializeField] private Image manaFill;
    [SerializeField] private Image manaBackgroundFill;

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
        
        manaFill.fillAmount = _clientManaManager.PredictedMana / 10;
    }

    private void OnServerMaxManaChanged(float previousValue, float newValue)
    {
        float normalizedValue = newValue / 10;
        manaBackgroundFill.fillAmount = normalizedValue;
    }
}
