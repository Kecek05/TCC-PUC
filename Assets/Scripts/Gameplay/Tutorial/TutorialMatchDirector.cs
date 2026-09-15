using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Runs the scripted half of the first-time experience: the match in TutorialScene. Owns the step list,
/// the subscriptions that complete those steps, and the handover to the Main Menu once they are done.
/// </summary>
/// <remarks>
/// A plain MonoBehaviour, not a NetworkBehaviour. The tutorial is always a local host, so the client and
/// the server are the same process and every signal it needs — deploy results, the camera side, the save —
/// is readable without a single Rpc. That is what lets the whole script live in one readable file.
/// </remarks>
public class TutorialMatchDirector : MonoBehaviour
{
    [Title("References")]
    [SerializeField, Required] private TutorialSettingsSO settings;

    [Tooltip("Optional. Without one the steps still run and complete — they just say and show nothing, " +
             "which is what makes the sequence drivable from a test.")]
    [SerializeField] private BaseTutorialOverlay overlay;

    [Title("Highlight Targets")]
    [InfoBox("Scene anchors the script points at. World anchors are Transforms so they can be dragged " +
             "into place rather than guessed as coordinates.")]
    [SerializeField] private RectTransform manaBarRect;
    [SerializeField] private RectTransform handRect;

    [Tooltip("A point on the player's own field, used to aim the swipe-home hint.")]
    [SerializeField] private Transform localFieldAnchor;

    [Tooltip("A point on the opponent's field — where troops and offensive spells are aimed.")]
    [SerializeField] private Transform enemyFieldAnchor;

    private BaseTutorialService _tutorial;
    private BasePlayerSaveManager _save;
    private BaseRewardService _rewards;
    private BaseTeamManager _teamManager;
    private BaseCardContainer _cardContainer;
    private BaseGameFlowManager _gameFlow;
    private BaseCardTowerDeployer _towerDeployer;
    private CardDeploymentBus _deploymentBus;

    private TutorialSequence _sequence;
    private TeamType _localTeam = TeamType.None;

    // --- Observations. Set by the subscriptions below, read by the step predicates. ---
    private bool _towerPlaced;
    private bool _towerLevelledUp;
    private bool _troopSent;
    private bool _spellCast;
    private Vector2 _placedTowerPosition;
    private CardType _placedTowerCard = CardType.None;
    private CameraSide _cameraSide = CameraSide.Local;

    private void Awake()
    {
        // Only the first-time run is scripted. Entering TutorialScene any other way (a debug load) leaves
        // the director inert rather than hijacking the match.
        if (!ServiceLocator.TryGet(out _tutorial) || _tutorial.Phase != TutorialPhase.Match)
        {
            enabled = false;
            return;
        }
    }

    private void OnEnable() => CameraSlide.SideChanged += HandleCameraSideChanged;

    private void OnDisable() => CameraSlide.SideChanged -= HandleCameraSideChanged;

    private void Start()
    {
        if (!enabled) return;
        StartCoroutine(RunWhenMatchStarts());
    }

    private void OnDestroy()
    {
        Unsubscribe();
        _sequence?.Stop();

        if (overlay != null) overlay.OnSkipTapped -= HandleSkipTapped;
    }

    private void Update() => _sequence?.Tick();

    private IEnumerator RunWhenMatchStarts()
    {
        ServiceLocator.TryGet(out _save);
        ServiceLocator.TryGet(out _rewards);

        yield return new WaitUntil(() => ServiceLocator.TryGet(out _gameFlow) && _gameFlow != null);
        yield return new WaitUntil(() => _gameFlow.CurrentGameState.Value == GameState.InMatch);

        // Resolved after the match is live: the hand and the deployers only exist from that point.
        ServiceLocator.TryGet(out _teamManager);
        ServiceLocator.TryGet(out _cardContainer);
        ServiceLocator.TryGet(out _towerDeployer);
        ServiceLocator.TryGet(out _deploymentBus);

        _localTeam = _teamManager != null ? _teamManager.GetLocalTeam() : TeamType.None;

        Subscribe();

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

    /// <summary>
    /// The tutorial, in order. Every step that asks for an action carries a timeout: a player who cannot
    /// make one happen — no matching card drawn, no mana, a slot the bot took — is moved along rather than
    /// left stuck, which is the one failure a first-time experience must not have.
    /// </summary>
    private List<TutorialStep> BuildSteps() => new()
    {
        new TutorialStep(TutorialStepId.Welcome)
            .Tap(),

        new TutorialStep(TutorialStepId.Mana)
            .Tap()
            .Pointing(() => TutorialHighlight.Ui(manaBarRect, TutorialHintKind.None)),

        new TutorialStep(TutorialStepId.Hand)
            .Tap()
            .Pointing(() => TutorialHighlight.Ui(handRect, TutorialHintKind.None)),

        new TutorialStep(TutorialStepId.PlaceTower)
            .CompletesWhen(() => _towerPlaced)
            .Pointing(PointAtTowerPlacement)
            .GivingUpAfter(60f),

        new TutorialStep(TutorialStepId.LevelUpTower)
            .CompletesWhen(() => _towerLevelledUp)
            .Pointing(PointAtTowerUpgrade)
            .GivingUpAfter(60f),

        new TutorialStep(TutorialStepId.SwapToEnemyMap)
            .CompletesWhen(() => _cameraSide == CameraSide.Enemy)
            .Pointing(() => TutorialHighlight.World(ScreenCentreWorld(), 2.5f, TutorialHintKind.SwipeUp))
            .GivingUpAfter(45f),

        new TutorialStep(TutorialStepId.SendTroop)
            .CompletesWhen(() => _troopSent)
            .Pointing(() => PointAtCardDrop(ExistingTypesOfCard.Enemy, enemyFieldAnchor))
            .GivingUpAfter(60f),

        new TutorialStep(TutorialStepId.CastSpell)
            .CompletesWhen(() => _spellCast)
            .Pointing(() => PointAtCardDrop(ExistingTypesOfCard.Spell, enemyFieldAnchor))
            .GivingUpAfter(60f),

        new TutorialStep(TutorialStepId.SwapBackHome)
            .CompletesWhen(() => _cameraSide == CameraSide.Local)
            .Pointing(() => TutorialHighlight.World(ScreenCentreWorld(), 2.5f, TutorialHintKind.SwipeDown))
            .GivingUpAfter(45f),

        new TutorialStep(TutorialStepId.MatchOutro)
            .Tap(),
    };

    // ---- Highlight resolvers --------------------------------------------------------------------

    /// <summary>
    /// Card in hand -> a free slot on the player's own lane. Resolved every frame, so a hand that has no
    /// tower card yet simply shows the dim and no ring until one is drawn, instead of pointing at nothing.
    /// </summary>
    private TutorialHighlight PointAtTowerPlacement()
    {
        AbstractCard card = FindCardInHand(ExistingTypesOfCard.Tower);
        AbstractPlaceable slot = FindFreePlaceable();

        if (card == null || slot == null) return TutorialHighlight.None;

        return TutorialHighlight.DragUiToWorld(card.Rect, slot.PlaceablePoint.position);
    }

    /// <summary>
    /// The upgrade is the same gesture onto the tower already standing, and only a card of that tower's
    /// own type can do it — which is exactly the rule the deployer enforces, so the hint has to match it
    /// or the player would be told to do something the server refuses.
    /// </summary>
    private TutorialHighlight PointAtTowerUpgrade()
    {
        if (_placedTowerCard == CardType.None) return TutorialHighlight.None;

        AbstractCard card = FindCardInHand(_placedTowerCard);
        if (card == null) return TutorialHighlight.None;

        return TutorialHighlight.DragUiToWorld(card.Rect, _placedTowerPosition);
    }

    private TutorialHighlight PointAtCardDrop(ExistingTypesOfCard family, Transform anchor)
    {
        AbstractCard card = FindCardInHand(family);
        if (card == null || anchor == null) return TutorialHighlight.None;

        return TutorialHighlight.DragUiToWorld(card.Rect, anchor.position);
    }

    private Vector3 ScreenCentreWorld()
    {
        Camera camera = Camera.main;
        return camera != null ? camera.transform.position + Vector3.forward * 10f : Vector3.zero;
    }

    // ---- Scene lookups --------------------------------------------------------------------------

    private AbstractCard FindCardInHand(ExistingTypesOfCard family)
    {
        if (_cardContainer == null) return null;

        foreach (AbstractCard card in _cardContainer.CardsInHand)
        {
            if (card == null || card.CardData == null) continue;
            if (card.CardData.ExistingType == family) return card;
        }

        return null;
    }

    private AbstractCard FindCardInHand(CardType cardType)
    {
        if (_cardContainer == null) return null;

        foreach (AbstractCard card in _cardContainer.CardsInHand)
        {
            if (card == null || card.CardData == null) continue;
            if (card.CardData.CardType == cardType) return card;
        }

        return null;
    }

    /// <summary>
    /// A free slot on the player's own lane. Walked fresh each frame rather than cached: the bot cannot
    /// take these, but a tower lost to a wave frees one back up mid-step.
    /// </summary>
    private AbstractPlaceable FindFreePlaceable()
    {
        AbstractPlaceable[] all = FindObjectsByType<AbstractPlaceable>(FindObjectsSortMode.None);

        foreach (AbstractPlaceable placeable in all)
        {
            if (placeable == null || placeable.IsOccupied()) continue;

            TeamIdentifier team = placeable.GetComponentInParent<TeamIdentifier>();
            if (team != null && team.TeamType == _localTeam) return placeable;
        }

        return null;
    }

    // ---- Observations ---------------------------------------------------------------------------

    private void Subscribe()
    {
        if (_towerDeployer != null) _towerDeployer.OnPlaceResult += HandleTowerPlaceResult;
        if (_deploymentBus != null) _deploymentBus.OnAnyCardDeployed += HandleCardDeployed;
    }

    private void Unsubscribe()
    {
        if (_towerDeployer != null) _towerDeployer.OnPlaceResult -= HandleTowerPlaceResult;
        if (_deploymentBus != null) _deploymentBus.OnAnyCardDeployed -= HandleCardDeployed;
    }

    private void HandleTowerPlaceResult(TowerPlaceResult result)
    {
        if (!result.Validation.IsValid) return;

        switch (result.Validation.Reason)
        {
            case TowerReason.Success:
                _towerPlaced = true;

                // Remembered so the upgrade step can point back at this exact tower with a card that can
                // actually upgrade it. Position is where the card landed, which is the placeable's point.
                _placedTowerPosition = result.Position;
                _placedTowerCard = result.CardType;
                break;

            case TowerReason.LevelUp:
                _towerLevelledUp = true;
                break;
        }
    }

    private void HandleCardDeployed(CardDeployedEventArgs args)
    {
        // The bot plays through the same deployers, so a deploy only counts when it was the player's.
        if (args.TeamDeployed != _localTeam) return;

        CardDataSO card = settings != null && settings.CardDataList != null
            ? settings.CardDataList.GetCardDataByType(args.CardDeployed)
            : null;

        if (card == null) return;

        if (card.ExistingType == ExistingTypesOfCard.Enemy) _troopSent = true;
        if (card.ExistingType == ExistingTypesOfCard.Spell) _spellCast = true;
    }

    private void HandleCameraSideChanged(CameraSide side) => _cameraSide = side;

    // ---- Handover -------------------------------------------------------------------------------

    private void HandleSkipTapped()
    {
        GameLog.Info("[Tutorial] Player skipped the tutorial match.");

        _sequence?.Stop();
        _tutorial?.Abandon();

        StartCoroutine(LeaveToMenu(grantReward: false, immediate: true));
    }

    private void HandleSequenceFinished() => StartCoroutine(LeaveToMenu(grantReward: true, immediate: false));

    /// <summary>
    /// Pays the tutorial out and hands the player to the Main Menu for the second half.
    /// </summary>
    /// <remarks>
    /// The reward is granted straight through <see cref="BaseRewardService"/> rather than by ending the
    /// match and letting <c>ServerEndGameManager</c> roll one. Two reasons: the payout is authored, not
    /// rolled — the menu steps that follow depend on its exact shape — and the tutorial never reaches a
    /// real win condition, so there is no end-of-match to hang it off.
    /// </remarks>
    private IEnumerator LeaveToMenu(bool grantReward, bool immediate)
    {
        Unsubscribe();

        CardType rewardCard = CardType.None;

        if (grantReward && _rewards != null && _save != null)
        {
            Reward reward = new TutorialRewardRoller(settings, _save).Roll();
            if (!reward.IsEmpty)
            {
                _rewards.Grant(reward);
                rewardCard = reward.Card;
            }
        }

        if (!immediate && settings != null && settings.OutroSeconds > 0f)
            yield return new WaitForSecondsRealtime(settings.OutroSeconds);

        // Arms the menu half before the scene change, so the menu director finds the phase already set
        // the moment it wakes.
        if (grantReward) _tutorial?.BeginMenuPhase(rewardCard);

        // Fire-and-forget on purpose: LeaveMatchAsync owns the host teardown and the scene load, and a
        // coroutine cannot await a Task anyway (yielding one would just wait a single frame).
        if (ServiceLocator.TryGet(out BaseClientManager clientManager))
            _ = clientManager.LeaveMatchAsync();
        else
            Loader.Load(Loader.Scene.MainMenu);
    }
}
