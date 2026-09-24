using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class ConnectionManagerUI : MonoBehaviour
{
    [Header("General")]
    [SerializeField] private CardHandSettingsSO cardHandSettingsSO;

    [Header("Battle")]
    [Tooltip("Quick match. Joins whoever is already waiting, or starts waiting itself - no code either way.")]
    [FormerlySerializedAs("createRelayButton")]
    [SerializeField] private Button battleButton;
    [SerializeField] private Button createDedicatedServerButton;

    [Header("Join by code (direct, for playing with a specific person)")]
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button joinDedicatedServerButton;

    private BaseClientManager _clientManager;
    private BaseMatchmaker _matchmaker;
    private ScreenWarning _screenWarning;

    private void Start()
    {
        _clientManager = ServiceLocator.Get<BaseClientManager>();
        _matchmaker = ServiceLocator.Get<BaseMatchmaker>();
        _screenWarning = ServiceLocator.Get<ScreenWarning>();

        battleButton.onClick.AddListener(QuickPlay);
        joinButton.onClick.AddListener(JoinRelay);

        createDedicatedServerButton.onClick.AddListener(CreateDedicatedServer);
        joinDedicatedServerButton.onClick.AddListener(JoinDedicatedServer);
    }

    /// <summary>
    /// Battle. One press finds a match: it joins an open one if there is one and hosts a new one if there
    /// is not, so the player never sees a room code. Both outcomes are a success and both start a scene
    /// load, which is why only the failure path puts the button back.
    /// </summary>
    private async void QuickPlay()
    {
        if (!CanPlay()) return;

        battleButton.interactable = false;

        MatchmakingOutcome outcome = MatchmakingOutcome.Failed;
        try
        {
            outcome = await _matchmaker.FindOrCreateMatchAsync();
        }
        catch (System.Exception e)
        {
            GameLog.Exception(e);
        }

        if (outcome != MatchmakingOutcome.Failed) return;

        _screenWarning.ShowWarning(WarningMessages.MatchmakingFailed);
        battleButton.interactable = true;
    }

    private async void JoinRelay()
    {
        if (!CanPlay()) return;
        
        string code = joinCodeInput.text.Trim();
        if (string.IsNullOrEmpty(code)) return;

        joinButton.interactable = false;

        try
        {
            if (await _clientManager.JoinHost(code)) return;
        }
        catch (Exception e)
        {
            GameLog.Error($"Failed to join relay: {e.Message}");
        }
        
        joinButton.interactable = true;
    }

    private async void CreateDedicatedServer()
    {
        
    }

    private async void JoinDedicatedServer()
    {
        
    }

    private bool CanPlay()
    {
        if (_clientManager.UserData.DeckCards.Count == cardHandSettingsSO.DeckSize)
            return true;
        
        _screenWarning.ShowWarning(WarningMessages.DeckNotFull);
        return false;
    }
}