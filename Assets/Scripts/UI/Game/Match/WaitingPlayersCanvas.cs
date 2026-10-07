using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// What a host sees while it waits in the match scene for an opponent: the menu's searching pill, still counting
/// from the press of Battle (MatchSearch). When the opponent arrives the pill says so (the found punch, flash,
/// chime and buzz) and the screen fades away onto the players' introduction.
/// </summary>
public class WaitingPlayersCanvas : NetworkBehaviour
{
    [Title("References")]
    [SerializeField] private GameObject waitingCanvas;
    [SerializeField] private TextMeshProUGUI  joinCodeText;

    [Title("Search Pill")]
    [Tooltip("The pill's size on this screen.")]
    [SerializeField] private Vector2 pillSize = new(560f, 120f);
    [Tooltip("How long \"found\" holds before the screen fades onto the introduction.")]
    [SerializeField, Min(0f)] private float foundHoldSeconds = 0.6f;

    private BaseGameFlowManager _gameFlowManager;
    private MatchSearchView _search;
    private CanvasGroup _group;
    private bool _waiting;
    private Sequence _leave;

    private void Awake()
    {
        waitingCanvas.SetActive(true);
    }

    public override void OnNetworkSpawn()
    {
        if (IsHost)
        {
            var hostManager = ServiceLocator.Get<BaseHostManager>();
            joinCodeText.text = hostManager.CurrentHostConnectionData.JoinCode;
        }

        _gameFlowManager = ServiceLocator.Get<BaseGameFlowManager>();
        _gameFlowManager.CurrentGameState.OnValueChanged += OnGameStateChanged;
        UpdateCanvasVisibility(_gameFlowManager.CurrentGameState.Value);
    }

    public override void OnNetworkDespawn()
    {
        if (_gameFlowManager != null)
            _gameFlowManager.CurrentGameState.OnValueChanged -= OnGameStateChanged;
    }

    public override void OnDestroy()
    {
        _leave?.Kill();
        base.OnDestroy();
    }

    private void OnGameStateChanged(GameState oldState, GameState newState)
    {
        UpdateCanvasVisibility(newState);
    }

    private void UpdateCanvasVisibility(GameState newState)
    {
        if (newState == GameState.WaitingForPlayers)
        {
            ShowSearching();
            return;
        }

        if (newState != GameState.LoadingMatch) return;

        // Someone joined: a host that was waiting is told so before the screen goes; anyone else simply never
        // saw a wait.
        if (_waiting) Found();
        else waitingCanvas.SetActive(false);
    }

    /// <summary>The searching pill, picking up the menu's clock (a search that hosts carries straight on here).</summary>
    private void ShowSearching()
    {
        if (_waiting) return;
        _waiting = true;

        if (_search == null)
        {
            RectTransform screen = (RectTransform)waitingCanvas.transform;
            TMP_Text style = waitingCanvas.GetComponentInChildren<TMP_Text>(true);

            // The old static "Waiting..." gives way to the pill; its font is the one the pill borrows.
            foreach (TMP_Text label in waitingCanvas.GetComponentsInChildren<TMP_Text>(true))
                label.gameObject.SetActive(false);

            _search = MatchSearchView.CreateIn(screen, style, pillSize);
            _group = waitingCanvas.GetComponent<CanvasGroup>();
            if (_group == null) _group = waitingCanvas.AddComponent<CanvasGroup>();
        }

        float started = MatchSearchView.SearchStartedAt;
        _search.ShowSearching(started >= 0f && Time.unscaledTime - started < 600f ? started : null);
        _search.ShowHosting();
    }

    /// <summary>The opponent arrived: the found beat, a short hold, then this screen fades onto the introduction.</summary>
    private void Found()
    {
        _waiting = false;
        _search.ShowFound();

        JuiceTokensSO t = Juice.Tokens;
        _leave?.Kill();
        _leave = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .AppendInterval(foundHoldSeconds)
            .Append(_group.DOFade(0f, t.Durations.Panel).SetEase(t.Eases.In))
            .OnComplete(() => waitingCanvas.SetActive(false))
            .OnKill(() =>
            {
                if (_group != null) _group.alpha = 1f;
            });
    }
}
