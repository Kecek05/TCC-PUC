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

        SlideNamesIn();
    }

    /// <summary>
    /// MatchSearch's VS beat on the intro: the two names slide in from opposite edges (dur-travel, ease-out)
    /// with a whoosh and land on a stamp. Unscaled like the fade, and it never touches the canvas group, so
    /// <see cref="IsFinished"/> keeps meaning exactly what the tutorial waits on.
    /// </summary>
    private void SlideNamesIn()
    {
        if (!isActiveAndEnabled || canvasGroup.alpha <= 0f) return;

        JuiceTokensSO t = Juice.Tokens;

        // Each name rides a band in its own colour (name-you, name-foe), and the two bands come in from opposite
        // sides, so the screen reads as two players meeting rather than two lines of text.
        RectTransform local = Band(localPlayerLabel, t.Colors.NameYou);
        RectTransform enemy = Band(enemyPlayerLabel, t.Colors.NameFoe);
        Vector2 localRest = local.anchoredPosition;
        Vector2 enemyRest = enemy.anchoredPosition;
        float offset = 1200f;

        local.anchoredPosition = localRest + Vector2.left * offset;
        enemy.anchoredPosition = enemyRest + Vector2.right * offset;

        // VS waits off screen-size, then stamps down once both names are in.
        Transform versus = FindVersusLabel();
        CanvasGroup versusGroup = versus != null ? GroupOf(versus) : null;
        if (versusGroup != null)
        {
            versusGroup.alpha = 0f;
            versus.localScale = Vector3.one * 2.2f;
        }

        Juice.Sfx(SfxId.Whoosh);
        Sequence s = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
            .Join(local.DOAnchorPos(localRest, t.Durations.Travel).SetEase(t.Eases.Out))
            .Join(enemy.DOAnchorPos(enemyRest, t.Durations.Travel).SetEase(t.Eases.Out));

        if (versusGroup != null)
        {
            s.Append(versusGroup.DOFade(1f, t.Durations.Select));
            s.Join(versus.DOScale(1f, t.Durations.Pop).SetEase(t.Eases.Pop));
        }

        s.AppendCallback(() =>
            {
                Juice.Sfx(SfxId.Stamp);
                Juice.Haptic(HapticRole.Medium);
                JuiceCamera.Shake(t.Shake.Micro, t.Durations.Shake);
                if (versus != null) JuiceFx.Punch(versus, t.Scales.PunchHard);
            })
            .OnKill(() =>
            {
                if (local != null) local.anchoredPosition = localRest;
                if (enemy != null) enemy.anchoredPosition = enemyRest;
                if (versusGroup != null) versusGroup.alpha = 1f;
                if (versus != null) versus.localScale = Vector3.one;
            });
    }

    /// <summary>The row a name sits in, given a soft band in the player's colour behind it (built once).</summary>
    private static RectTransform Band(TMP_Text label, Color color)
    {
        RectTransform row = label.rectTransform.parent as RectTransform;
        if (row == null) return label.rectTransform;
        if (row.Find("NameBand") != null) return row;

        GameObject band = new GameObject("NameBand", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        RectTransform rect = (RectTransform)band.transform;
        rect.SetParent(row, false);
        rect.SetAsFirstSibling();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(0f, label.rectTransform.rect.height * 1.7f);

        UnityEngine.UI.Image image = band.GetComponent<UnityEngine.UI.Image>();
        image.sprite = JuiceSprites.Glow;
        image.raycastTarget = false;
        color.a = 0.3f;
        image.color = color;
        return row;
    }

    private Transform FindVersusLabel()
    {
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            if (text.name.Contains("Versus") || text.text == "VS") return text.transform;
        return null;
    }

    private static CanvasGroup GroupOf(Component c)
    {
        CanvasGroup g = c.GetComponent<CanvasGroup>();
        return g != null ? g : c.gameObject.AddComponent<CanvasGroup>();
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
