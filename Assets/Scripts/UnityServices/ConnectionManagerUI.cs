using System;
using System.Threading;
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
    private MatchSearchView _searchView;
    private CancellationTokenSource _search;

    private void Start()
    {
        _clientManager = ServiceLocator.Get<BaseClientManager>();
        _matchmaker = ServiceLocator.Get<BaseMatchmaker>();

        _searchView = MatchSearchView.Create(battleButton);
        _searchView.OnCancelRequested += CancelSearch;
        _matchmaker.HostingStarted += _searchView.ShowHosting;

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
        _search?.Dispose();
        _search = new CancellationTokenSource();
        _searchView.ShowSearching();

        MatchmakingOutcome outcome = MatchmakingOutcome.Failed;
        try
        {
            outcome = await _matchmaker.FindOrCreateMatchAsync(_search.Token);
        }
        catch (System.Exception e)
        {
            GameLog.Exception(e);
        }

        if (this == null) return; // the scene went while we waited

        switch (outcome)
        {
            case MatchmakingOutcome.Joined:
                // Too late to cancel or not, the load is under way: say so before it takes the screen.
                _searchView.ShowFound();
                return;

            case MatchmakingOutcome.Hosting:
                // Nobody was found yet: the wait for an opponent goes on in the match scene.
                return;

            case MatchmakingOutcome.Cancelled:
                _searchView.ShowIdle();
                battleButton.interactable = true;
                return;

            default:
                _searchView.ShowIdle(shake: true);
                Juice.Refuse(RefusalReason.MatchmakingFailed);
                battleButton.interactable = true;
                return;
        }
    }

    /// <summary>
    /// The cross on the searching pill. The search stops at its next clean exit (before it joins or hosts);
    /// the pill shows "Cancelling" until it does, and Battle comes back when it has.
    /// </summary>
    private void CancelSearch()
    {
        if (_search == null || _search.IsCancellationRequested) return;
        _search.Cancel();
        _searchView.ShowCancelling();
    }

    private void OnDestroy()
    {
        if (_searchView != null) _searchView.OnCancelRequested -= CancelSearch;

        // The matchmaker outlives this menu; a scene that is gone must not be told about the next search.
        if (_matchmaker != null && _searchView != null) _matchmaker.HostingStarted -= _searchView.ShowHosting;
        _search?.Dispose();
        _search = null;
    }

    private async void JoinRelay()
    {
        if (!CanPlay()) return;
        
        string code = joinCodeInput.text.Trim();
        if (string.IsNullOrEmpty(code))
        {
            Juice.Toast("Enter a room code");
            return;
        }

        joinButton.interactable = false;

        try
        {
            if (await _clientManager.JoinHost(code)) return;
        }
        catch (Exception e)
        {
            GameLog.Error($"Failed to join relay: {e.Message}");
        }

        // A failed join used to be silent; the player now hears why.
        Juice.Refuse(RefusalReason.JoinFailed);
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
        
        int missing = cardHandSettingsSO.DeckSize - _clientManager.UserData.DeckCards.Count;
        Juice.Refuse(RefusalReason.DeckNotFull, missing > 0 ? $"Add {missing} more" : null);
        return false;
    }
}