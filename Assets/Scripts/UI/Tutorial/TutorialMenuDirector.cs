using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The second half of the first-time experience, in the Main Menu: what the match paid out, how to put it
/// in a deck, how to spend the payout on levelling it, and back into a real match.
/// </summary>
/// <remarks>
/// Lives in MainMenu and arms itself only when <see cref="BaseTutorialService.Phase"/> is
/// <see cref="TutorialPhase.Menu"/>, so a returning player never sees it. The steps read the page's own
/// state through <c>DeckUIController</c> rather than driving it — the tutorial points, the player acts.
/// That is also why the deck is full when this starts and the script has to teach <i>remove then add</i>:
/// the starter deck is exactly <c>DeckSize</c> cards and <c>TryEquipCard</c> refuses a full one.
/// </remarks>
public class TutorialMenuDirector : MonoBehaviour
{
    [Title("References")]
    [SerializeField, Required] private TutorialSettingsSO settings;

    [Tooltip("Optional. Without one the steps still run and complete, silently.")]
    [SerializeField] private BaseTutorialOverlay overlay;

    [SerializeField, Required] private DeckUIController deckUIController;

    [Title("Navigation")]
    [InfoBox("The page strip and the two nav buttons the script sends the player through. Highlighted and " +
             "watched, never pressed for them.")]
    [SerializeField] private HorizontalPageStrip pageStrip;
    [SerializeField, MinValue(0)] private int deckPageIndex = 1;
    [SerializeField, MinValue(0)] private int battlePageIndex = 0;
    [SerializeField] private RectTransform deckNavButtonRect;
    [SerializeField] private RectTransform battleNavButtonRect;

    [Tooltip("The Battle button itself. Tapping it is the last beat of the tutorial.")]
    [SerializeField] private Button battleButton;

    private BaseTutorialService _tutorial;
    private BasePlayerSaveManager _save;
    private BaseInfoPanelService _infoPanel;

    private TutorialSequence _sequence;
    private CardType _rewardCard = CardType.None;

    /// <summary>Deck size at the moment the script started, so "a card came out" is a comparison rather
    /// than an assumption about what a full deck is.</summary>
    private int _deckCountAtStart;

    private bool _infoPanelOpened;
    private bool _battlePressed;

    private void Awake()
    {
        if (!ServiceLocator.TryGet(out _tutorial) || _tutorial.Phase != TutorialPhase.Menu)
        {
            enabled = false;
            return;
        }
    }

    private void Start()
    {
        if (!enabled) return;
        StartCoroutine(RunWhenPageReady());
    }

    private void OnDestroy()
    {
        if (_infoPanel != null) _infoPanel.OnInfoPanelShow -= HandleInfoPanelShown;
        if (battleButton != null) battleButton.onClick.RemoveListener(HandleBattlePressed);
        if (overlay != null) overlay.OnSkipTapped -= HandleSkipTapped;

        _sequence?.Stop();
    }

    private void Update() => _sequence?.Tick();

    private IEnumerator RunWhenPageReady()
    {
        ServiceLocator.TryGet(out _save);

        // DeckUIController builds its widgets in Start; pointing at one before that finds nothing.
        yield return null;
        yield return new WaitUntil(() => deckUIController != null && deckUIController.ActionFrame != null);

        _rewardCard = _tutorial.RewardCard;
        _deckCountAtStart = deckUIController.EquippedCount;

        if (ServiceLocator.TryGet(out _infoPanel)) _infoPanel.OnInfoPanelShow += HandleInfoPanelShown;
        if (battleButton != null) battleButton.onClick.AddListener(HandleBattlePressed);

        if (overlay != null)
        {
            overlay.OnSkipTapped += HandleSkipTapped;
            overlay.SetSkipVisible(settings != null && settings.AllowSkip);
        }

        _sequence = new TutorialSequence(BuildSteps(), overlay, settings != null ? settings.Copy : null);
        _sequence.OnFinished += HandleSequenceFinished;
        _sequence.Start();
    }

    // ---- The script -----------------------------------------------------------------------------

    private List<TutorialStep> BuildSteps() => new()
    {
        new TutorialStep(TutorialStepId.MenuWelcome)
            .Tap(),

        new TutorialStep(TutorialStepId.OpenDeckPage)
            .CompletesWhen(() => CurrentPage == deckPageIndex)
            .Pointing(() => TutorialHighlight.Ui(deckNavButtonRect))
            .GivingUpAfter(45f),

        // The deck is full, so the new card has nowhere to go until one comes out. Taught as its own beat
        // rather than folded into the next one, because "tap a card you own to manage it" is the gesture
        // the whole page is built on.
        new TutorialStep(TutorialStepId.RemoveCard)
            .CompletesWhen(() => deckUIController.EquippedCount < _deckCountAtStart)
            .Pointing(PointAtDeckCard)
            .GivingUpAfter(60f),

        new TutorialStep(TutorialStepId.EquipRewardCard)
            .CompletesWhen(() => HasReward && deckUIController.IsCardEquipped(_rewardCard))
            .Pointing(() => PointAtCard(_rewardCard))
            .GivingUpAfter(60f),

        new TutorialStep(TutorialStepId.OpenCardDetails)
            .CompletesWhen(() => _infoPanelOpened)
            .Pointing(PointAtDetailsButton)
            .GivingUpAfter(60f),

        new TutorialStep(TutorialStepId.UpgradeCard)
            .CompletesWhen(() => HasReward && _save != null && _save.GetCardLevel(_rewardCard) > 1)
            .Pointing(PointAtUpgradeButton)
            .GivingUpAfter(90f),

        new TutorialStep(TutorialStepId.OpenBattlePage)
            .CompletesWhen(() => CurrentPage == battlePageIndex)
            .Pointing(() => TutorialHighlight.Ui(battleNavButtonRect))
            .GivingUpAfter(45f),

        new TutorialStep(TutorialStepId.PressBattle)
            .CompletesWhen(() => _battlePressed)
            .Pointing(() => TutorialHighlight.Ui(battleButton != null ? (RectTransform)battleButton.transform : null))
            .GivingUpAfter(90f),
    };

    // ---- Highlight resolvers --------------------------------------------------------------------

    private bool HasReward => _rewardCard != CardType.None;

    private int CurrentPage => pageStrip != null ? pageStrip.CurrentPageIndex : -1;

    /// <summary>
    /// Any card currently in the deck — whichever one the player removes is fine, so the script points at
    /// the first it finds rather than insisting on a particular card the player may like.
    /// </summary>
    private TutorialHighlight PointAtDeckCard()
    {
        if (_save?.ActiveDeck == null) return TutorialHighlight.None;

        foreach (CardType cardType in _save.ActiveDeck.Cards)
        {
            if (cardType == _rewardCard) continue;
            if (deckUIController.TryGetCardWidget(cardType, out SingleCardInDeck widget))
                return TutorialHighlight.Ui((RectTransform)widget.transform);
        }

        return TutorialHighlight.None;
    }

    private TutorialHighlight PointAtCard(CardType cardType)
    {
        if (cardType == CardType.None) return TutorialHighlight.None;

        return deckUIController.TryGetCardWidget(cardType, out SingleCardInDeck widget)
            ? TutorialHighlight.Ui((RectTransform)widget.transform)
            : TutorialHighlight.None;
    }

    /// <summary>
    /// The Details button, but only once the popup is open on the right card — before that the button is
    /// off screen, and framing a hidden rect would cut a hole over nothing.
    /// </summary>
    private TutorialHighlight PointAtDetailsButton()
    {
        ActionFrame frame = deckUIController.ActionFrame;
        if (frame == null) return TutorialHighlight.None;

        if (!frame.IsVisible || frame.ShownCard == null || frame.ShownCard.CardType != _rewardCard)
            return PointAtCard(_rewardCard);

        return TutorialHighlight.Ui(frame.InfoButtonRect);
    }

    private TutorialHighlight PointAtUpgradeButton()
    {
        ActionFrame frame = deckUIController.ActionFrame;
        if (frame != null && frame.IsVisible && frame.UpgradeButtonRect != null)
            return TutorialHighlight.Ui(frame.UpgradeButtonRect);

        // The info panel has an Upgrade button of its own, and it is the one the player is looking at
        // after the previous step. Nothing to resolve it to a rect from here, so dim only and let the copy
        // carry the instruction — the step still completes off the saved level either way.
        return TutorialHighlight.None;
    }

    // ---- Observations ---------------------------------------------------------------------------

    private void HandleInfoPanelShown() => _infoPanelOpened = true;

    private void HandleBattlePressed() => _battlePressed = true;

    // ---- Handover -------------------------------------------------------------------------------

    private void HandleSkipTapped()
    {
        GameLog.Info("[Tutorial] Player skipped the menu tutorial.");

        _sequence?.Stop();
        _tutorial?.Abandon();
    }

    /// <summary>
    /// The last step is the player pressing Battle, so by the time this runs the match is already being
    /// hosted. Writing the save here rather than on the match's first frame keeps the whole tutorial's
    /// bookkeeping in the two directors that own it.
    /// </summary>
    private void HandleSequenceFinished() => _tutorial?.Complete();
}
