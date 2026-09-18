using System;
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
/// <para>
/// Unlike the match, nothing here freezes the clock: there is no wave to hold back, and the menu animates
/// on scaled time. Steps that change page settle on the strip instead, so the next one is only shown once
/// the page it talks about has finished sliding in.
/// </para>
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

    /// <summary>The reward card's level when the script started. "The player upgraded it" is a rise from
    /// here, not "above 1": a replayed tutorial can pay out a card the save has already levelled.</summary>
    private int _rewardLevelAtStart;

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
        _rewardLevelAtStart = RewardLevel;
        _deckCountAtStart = deckUIController.EquippedCount;

        if (ServiceLocator.TryGet(out _infoPanel)) _infoPanel.OnInfoPanelShow += HandleInfoPanelShown;
        if (battleButton != null) battleButton.onClick.AddListener(HandleBattlePressed);

        if (overlay != null)
        {
            overlay.OnSkipTapped += HandleSkipTapped;
            overlay.SetSkipVisible(settings != null && settings.AllowSkip);
        }

        _sequence = new TutorialSequence(BuildSteps(), overlay, settings != null ? settings.Copy : null,
            freezesWorld: false);
        _sequence.OnFinished += HandleSequenceFinished;
        _sequence.Start();
    }

    // ---- The script -----------------------------------------------------------------------------

    private List<TutorialStep> BuildSteps() => new()
    {
        new TutorialStep(TutorialStepId.MenuWelcome)
            .Tap(),

        // The tap switches CurrentPageIndex at once, but the page is still sliding in, and the next step
        // points at cards on it - so the step settles on the strip before the next one is shown.
        new TutorialStep(TutorialStepId.OpenDeckPage)
            .CompletesWhen(() => CurrentPage == deckPageIndex)
            .SettlingUntil(IsPageStripSettled)
            .Pointing(() => TutorialHighlight.Ui(deckNavButtonRect))
            .GivingUpAfter(45f),

        // The deck is full, so the new card has nowhere to go until one comes out. Taught as its own beat
        // rather than folded into the next one, because "tap a card you own to manage it" is the gesture
        // the whole page is built on. A deck with room (a replayed save) has nothing to make room in.
        new TutorialStep(TutorialStepId.RemoveCard)
            .OnlyWhen(() => _save != null && _save.IsActiveDeckFull)
            .CompletesWhen(() => deckUIController.EquippedCount < _deckCountAtStart)
            .Pointing(OnPage(deckPageIndex, deckNavButtonRect, PointAtDeckCard))
            .GivingUpAfter(60f),

        new TutorialStep(TutorialStepId.EquipRewardCard)
            .CompletesWhen(() => HasReward && deckUIController.IsCardEquipped(_rewardCard))
            .Pointing(OnPage(deckPageIndex, deckNavButtonRect, () => PointAtCard(_rewardCard)))
            .GivingUpAfter(60f),

        new TutorialStep(TutorialStepId.OpenCardDetails)
            .CompletesWhen(() => _infoPanelOpened)
            .Pointing(OnPage(deckPageIndex, deckNavButtonRect, PointAtDetailsButton))
            .GivingUpAfter(60f),

        new TutorialStep(TutorialStepId.UpgradeCard)
            .OnlyWhen(IsRewardUpgradeAhead)
            .CompletesWhen(() => RewardLevel > _rewardLevelAtStart)
            .Pointing(OnPage(deckPageIndex, deckNavButtonRect, PointAtUpgradeButton))
            .GivingUpAfter(90f),

        // The panel is modal and covers the nav bar, so the next step would point at a button the player
        // cannot reach. Skipped when the panel is already gone — the player may have closed it themselves,
        // and nothing above forces it open (UpgradeCard passes over a maxed card without ever showing).
        new TutorialStep(TutorialStepId.CloseCardDetails)
            .OnlyWhen(IsInfoPanelVisible)
            .CompletesWhen(() => !IsInfoPanelVisible())
            .Pointing(PointAtCloseButton)
            .GivingUpAfter(45f),

        new TutorialStep(TutorialStepId.OpenBattlePage)
            .CompletesWhen(() => CurrentPage == battlePageIndex)
            .SettlingUntil(IsPageStripSettled)
            .Pointing(() => TutorialHighlight.Ui(battleNavButtonRect))
            .GivingUpAfter(45f),

        new TutorialStep(TutorialStepId.PressBattle)
            .CompletesWhen(() => _battlePressed)
            .Pointing(OnPage(battlePageIndex, battleNavButtonRect,
                () => TutorialHighlight.Ui(battleButton != null ? (RectTransform)battleButton.transform : null)))
            .GivingUpAfter(90f),
    };

    // ---- Conditions -----------------------------------------------------------------------------

    private bool HasReward => _rewardCard != CardType.None;

    private int RewardLevel => HasReward && _save != null ? _save.GetCardLevel(_rewardCard) : 0;

    private int CurrentPage => pageStrip != null ? pageStrip.CurrentPageIndex : -1;

    /// <summary>An unwired strip never moves, so there is nothing to wait for.</summary>
    private bool IsPageStripSettled() => pageStrip == null || pageStrip.IsSettled;

    private bool IsInfoPanelVisible() => _infoPanel != null && _infoPanel.IsVisible;

    /// <summary>
    /// Whether there is still an upgrade to teach: the player has not bought it on the way here, and can
    /// afford it now. The payout covers the next level whenever the card has one, so this only fails for a
    /// replay whose reward was already maxed — and a step asking for an impossible purchase would only sit
    /// there until it timed out.
    /// </summary>
    private bool IsRewardUpgradeAhead() =>
        HasReward && _save != null && RewardLevel == _rewardLevelAtStart && _save.CanUpgradeCard(_rewardCard);

    // ---- Highlight resolvers --------------------------------------------------------------------

    /// <summary>
    /// A highlight that only exists on one page. Anywhere else — the player swiped away mid-step — it points
    /// at that page's nav button instead, so the ring never frames something that is off screen.
    /// </summary>
    private Func<TutorialHighlight> OnPage(int page, RectTransform navButton, Func<TutorialHighlight> highlight) =>
        () => pageStrip == null || CurrentPage == page ? highlight() : TutorialHighlight.Ui(navButton);

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

    /// <summary>
    /// The Upgrade button the player can actually see: the info panel's, which the previous step opened.
    /// Framing it matters beyond the ring — with no target the text box parks low, exactly over that button.
    /// Failing that the popup's own button, and failing that the card, whose popup leads to one.
    /// </summary>
    private TutorialHighlight PointAtUpgradeButton()
    {
        if (_infoPanel != null && _infoPanel.IsVisible && _infoPanel.UpgradeButtonRect != null)
            return TutorialHighlight.Ui(_infoPanel.UpgradeButtonRect);

        ActionFrame frame = deckUIController.ActionFrame;
        if (frame != null && frame.IsVisible && frame.ShownCard != null && frame.ShownCard.CardType == _rewardCard &&
            frame.UpgradeButtonRect != null)
            return TutorialHighlight.Ui(frame.UpgradeButtonRect);

        return PointAtCard(_rewardCard);
    }

    /// <summary>
    /// The panel's own Close button. No fallback to the card: the panel is modal, so while it is up there is
    /// nothing else worth framing — and the step is over the moment the panel goes away anyway.
    /// </summary>
    private TutorialHighlight PointAtCloseButton() =>
        IsInfoPanelVisible() ? TutorialHighlight.Ui(_infoPanel.CloseButtonRect) : TutorialHighlight.None;

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
