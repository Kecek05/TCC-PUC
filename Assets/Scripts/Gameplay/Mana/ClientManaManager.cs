using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Client-only: predicted mana display with optimistic spend visuals.
/// Reads from ServerManaManager's NetworkVariables, owns no network state.
/// </summary>
public class ClientManaManager : MonoBehaviour
{
    public static ClientManaManager Instance { get; private set; }

    [SerializeField] private ManaSettingsSO manaSettings;
    [SerializeField] private Image manaBarFill;
    [SerializeField] private TMP_Text manaText;

    private float _predictedMana;
    private float _serverMana;
    private float _pendingSpendTotal;
    private bool _initialized;
    private TeamType _localTeam;

    public float PredictedMana => _predictedMana;

    private void Awake() => Instance = this;

    private void Start()
    {
        StartCoroutine(WaitForInitialization());
    }

    private IEnumerator WaitForInitialization()
    {
        yield return new WaitUntil(() =>
            TeamManager.Instance != null &&
            TeamManager.Instance.HasLocalTeamBeenAssigned() &&
            ServerManaManager.Instance != null);

        _localTeam = TeamManager.Instance.GetLocalTeam();
        if (_localTeam == TeamType.None)
        {
            Debug.LogError("ClientManaManager: Local team not assigned. Mana display will not function.");
            yield break;
        }
        
        Debug.Log($"ClientManaManager: Local team is {_localTeam}");
        
        var manaVar = _localTeam == TeamType.Blue
            ? ServerManaManager.Instance.BlueMana
            : ServerManaManager.Instance.RedMana;

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
            Debug.Log($"ClientManaManager: Server mana updated to {_serverMana}. Pending spend total is {_pendingSpendTotal}. Predicted:  {Mathf.Max(0f, _serverMana - _pendingSpendTotal)}");
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

    public bool CanAffordLocally(int cost)
    {
        Debug.Log($"ClientManaManager: Can afford local cost {cost} : {Mathf.FloorToInt(_predictedMana) >= cost} (server: {Mathf.FloorToInt(_serverMana)}, pending: {_pendingSpendTotal})");
        return Mathf.FloorToInt(_predictedMana) >= cost;
    }

    public void PredictSpend(int cost)
    {
        _pendingSpendTotal += cost;
        _predictedMana -= cost;
    }

    /// <summary>
    /// Called when server confirms the spend was processed.
    /// No need to adjust _predictedMana — the next OnValueChanged will
    /// recompute it as _serverMana - remainingPending automatically.
    /// </summary>
    public void ConfirmSpend(int cost)
    {
        _pendingSpendTotal = Mathf.Max(0f, _pendingSpendTotal - cost);
    }

    public void RevertSpend(int cost)
    {
        _pendingSpendTotal = Mathf.Max(0f, _pendingSpendTotal - cost);
        _predictedMana += cost;
    }

    private void UpdateUI()
    {
        if (manaBarFill != null)
            manaBarFill.fillAmount = _predictedMana / manaSettings.MaxMana;

        if (manaText != null)
            manaText.text = Mathf.FloorToInt(_predictedMana).ToString();
    }
}