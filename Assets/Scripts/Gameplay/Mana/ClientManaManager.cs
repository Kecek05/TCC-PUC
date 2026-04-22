using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Client-only: predicted mana display with optimistic spend visuals.
/// Reads from <see cref="ServerManaManager"/>'s NetworkVariables.
/// </summary>
public class ClientManaManager : BaseClientManaManager
{

    [SerializeField] private ManaSettingsSO manaSettings;
    [SerializeField] private Image manaBarFill;
    [SerializeField] private TMP_Text manaText;

    private float _predictedMana;
    private float _serverMana;
    private float _pendingSpendTotal;
    private bool _initialized;
    private TeamType _localTeam;

    private BaseTeamManager _teamManager;
    private BaseServerManaManager _serverManaManager;

    private void Awake()
    {
        ServiceLocator.Register<BaseClientManaManager>(this);
    }

    private void Start()
    {
        StartCoroutine(WaitForInitialization());
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<BaseClientManaManager>();
        
        if (_serverManaManager == null) return;

        NetworkVariable<float> manaVar = _localTeam == TeamType.Blue
            ? _serverManaManager.BlueMana
            : _serverManaManager.RedMana;

        manaVar.OnValueChanged -= OnServerManaChanged;
    }

    private IEnumerator WaitForInitialization()
    {
        _teamManager = ServiceLocator.Get<BaseTeamManager>();
        _serverManaManager = ServiceLocator.Get<BaseServerManaManager>();

        yield return new WaitUntil(() =>
            _teamManager != null &&
            _teamManager.HasLocalTeamBeenAssigned() &&
            _serverManaManager != null);

        _localTeam = _teamManager.GetLocalTeam();

        NetworkVariable<float> manaVar = _localTeam == TeamType.Blue
            ? _serverManaManager.BlueMana
            : _serverManaManager.RedMana;

        _serverMana = manaVar.Value;
        _predictedMana = _serverMana;

        manaVar.OnValueChanged += OnServerManaChanged;
        _initialized = true;
    }

    private void OnServerManaChanged(float previousValue, float newValue)
    {
        _serverMana = newValue;

        if (_pendingSpendTotal > 0f)
        {
            // Server truth minus what it hasn't processed yet
            _predictedMana = Mathf.Max(0f, _serverMana - _pendingSpendTotal);
        }
        else
        {
            _predictedMana = _serverMana;
        }
    }

    private void Update()
    {
        if (!_initialized) return;

        // Local regen prediction for smooth bar between network ticks
        _predictedMana = Mathf.Min(
            _predictedMana + manaSettings.RegenPerSecond * Time.deltaTime,
            manaSettings.MaxMana
        );

        UpdateUI();
    }

    public override bool CanAffordLocally(int cost)
    {
        return Mathf.FloorToInt(_predictedMana) >= cost;
    }

    public override void PredictSpend(int cost)
    {
        _pendingSpendTotal += cost;
        _predictedMana -= cost;
    }

    /// <summary>
    /// Called from the client when received the result from the server, when predicted cost is true.
    /// </summary>
    public override void ConfirmSpend(int cost)
    {
        _pendingSpendTotal = Mathf.Max(0f, _pendingSpendTotal - cost);
        _predictedMana = Mathf.Max(0f, _serverMana - _pendingSpendTotal);
    }

    /// <summary>
    /// Called from the client when received the result from the server, when predicted cost is false.
    /// </summary>
    public override void RevertSpend(int cost)
    {
        _pendingSpendTotal = Mathf.Max(0f, _pendingSpendTotal - cost);
        _predictedMana = Mathf.Max(0f, _serverMana - _pendingSpendTotal);
    }

    private void UpdateUI()
    {
        if (manaBarFill != null)
            manaBarFill.fillAmount = _predictedMana / manaSettings.MaxMana;

        if (manaText != null)
            manaText.text = Mathf.FloorToInt(_predictedMana).ToString();
    }
}