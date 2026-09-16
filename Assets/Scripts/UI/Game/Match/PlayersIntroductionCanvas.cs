using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

public class PlayersIntroductionCanvas : MonoBehaviour, IMatchIntroduction
{
    [Title("References")]
    [SerializeField] private TextMeshProUGUI localPlayerLabel;
    [SerializeField] private TextMeshProUGUI enemyPlayerLabel;
    [SerializeField] private CanvasGroup canvasGroup;

    [Title("Hide Settings")]
    [SerializeField] private float delayBeforeHideSeconds = 2f;
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private Ease fadeEase = Ease.InOutSine;

    private BaseGameFlowManager _gameFlowManager;
    private BaseTeamManager _teamManager;

    private bool _namesPopulated;
    private bool _hideStarted;
    private Tween _fadeTween;

    public bool IsFinished { get; private set; }

    private void Awake()
    {
        canvasGroup.alpha = 1f;
        ServiceLocator.Register<IMatchIntroduction>(this);
    }

    private IEnumerator Start()
    {
        yield return new WaitUntil(() =>
            ServiceLocator.Get<BaseGameFlowManager>() != null &&
            ServiceLocator.Get<BaseTeamManager>() != null);

        _gameFlowManager = ServiceLocator.Get<BaseGameFlowManager>();
        _teamManager = ServiceLocator.Get<BaseTeamManager>();

        // This canvas has no NetworkObject of its own, so it can't use OnNetworkSpawn.
        // Wait for the networked GameFlowManager to spawn (so CurrentGameState holds the
        // synced value), then use the same approach as WaitingPlayersCanvas: react to
        // changes AND handle the current value immediately (covers an already-advanced
        // state, e.g. a late join).
        yield return new WaitUntil(() => _gameFlowManager.IsSpawned);

        _gameFlowManager.CurrentGameState.OnValueChanged += OnGameStateChanged;
        HandleGameState(_gameFlowManager.CurrentGameState.Value);
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<IMatchIntroduction>();

        if (_gameFlowManager != null)
            _gameFlowManager.CurrentGameState.OnValueChanged -= OnGameStateChanged;

        _fadeTween?.Kill();
    }

    private void OnGameStateChanged(GameState previousState, GameState newState)
    {
        HandleGameState(newState);
    }

    private void HandleGameState(GameState state)
    {
        TryPopulatePlayerNames();

        if (!_hideStarted && HasReachedMatchReady(state))
        {
            _hideStarted = true;
            HideAfterDelay();
        }
    }

    // Names ride inside TeamManager's synced PlayerTeamPair. On the host the local team
    // is assigned before the client even connects, so committing on the local team alone
    // leaves the enemy label empty forever. Wait until BOTH names are present, retrying
    // on each state change (the host's enemy name arrives with the WaitingForPlayers ->
    // LoadingMatch transition).
    private void TryPopulatePlayerNames()
    {
        if (_namesPopulated || _teamManager == null) return;
        if (!_teamManager.HasLocalTeamBeenAssigned()) return;

        string localName = _teamManager.GetPlayerName(_teamManager.GetLocalTeam());
        string enemyName = _teamManager.GetPlayerName(_teamManager.GetEnemyTeam());

        if (string.IsNullOrEmpty(localName) || string.IsNullOrEmpty(enemyName)) return;

        localPlayerLabel.text = localName;
        enemyPlayerLabel.text = enemyName;
        _namesPopulated = true;
    }

    // Unscaled: this is a loading screen, not gameplay, so nothing that stops the clock may hold it up.
    // The tutorial freezes Time.timeScale, and a scaled fade caught by that freeze left both names over
    // the board for the entire scripted match.
    private void HideAfterDelay()
    {
        _fadeTween = canvasGroup
            .DOFade(0f, fadeDuration)
            .SetDelay(delayBeforeHideSeconds)
            .SetEase(fadeEase)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                IsFinished = true;
            });
    }

    // Any state from MatchReady on, not MatchReady alone. The server passes through it in two seconds, and a
    // canvas that first looks after that would otherwise never hide - which the tutorial, now waiting on
    // IsFinished, would turn into a dead end.
    private static bool HasReachedMatchReady(GameState state) =>
        state is GameState.MatchReady or GameState.DrawingCards or GameState.InMatch or GameState.EndMatch;
}
