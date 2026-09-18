using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Netcode;
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
    /// <summary>Unscaled seconds the script waits, after InMatch, for the players' intro to leave the
    /// screen. It normally takes about 1.5s; this only matters if it never does.</summary>
    private const float IntroductionTimeoutSeconds = 10f;

    /// <summary>World units within which a place result's position is matched to the tower standing on it.
    /// The two are the same point, so this only has to stay under the gap between two slots.</summary>
    private const float TowerMatchRadius = 0.5f;

    /// <summary>World-unit half-width of the hole cut around a sent troop. Wide enough to hold the whole
    /// column the card sends, since they spawn a fraction of a second apart and walk together.</summary>
    private const float SentTroopHighlightRadius = 1.5f;

    /// <summary>Unscaled seconds the board is left running after a spell lands, so the player sees it work.
    /// Long enough to read a troop surging ahead; short enough not to feel like a pause.</summary>
    private const float SpellWatchSeconds = 2.5f;

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

    /// <summary>The costliest card in the lent deck — the mana ceiling the script has to guarantee. Resolved
    /// once the match is live, when the card list is loaded.</summary>
    private float _deckManaCost;

    /// <summary>Which cards the player may play this frame. Null leaves the whole hand open; set by the tick
    /// of whichever step is asking for a card, and cleared at the top of every frame.</summary>
    private Func<AbstractCard, bool> _playableCards;

    // --- Observations. Set by the subscriptions below, read by the step predicates. ---
    private bool _towerPlaced;
    private bool _towerLevelledUp;
    private bool _troopSent;

    /// <summary>Cards the player has played this match. Tracked per card rather than per family because the
    /// two spell steps name the exact card they teach: the hand holds Ice and Fireball at the same time, and
    /// they are cast on opposite fields for opposite reasons.</summary>
    private readonly HashSet<CardType> _cardsPlayed = new();
    private Vector2 _placedTowerPosition;
    private CardType _placedTowerCard = CardType.None;
    private CameraSide _cameraSide = CameraSide.Local;

    /// <summary>The tower the player's latest place or upgrade landed on - what the two tower steps settle on.</summary>
    private TowerManager _lastBuiltTower;

    /// <summary>Rolled and banked when the outro is reached, so the outro can name and show it and the
    /// handover does not have to roll a second one.</summary>
    private Reward _reward;
    private bool _rewardGranted;

    /// <summary>Set once the hand-off to the menu has started, so a late Skip cannot start a second one.</summary>
    private bool _leaving;

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

    private void Update()
    {
        // Cleared before the tick, so the only thing that can narrow the hand is a step's own tick, and only
        // on the frames it is actually waiting. Settling, the menu half and a run that has stopped all leave
        // the hand open without any step having to remember to unlock it — the failure a lock/unlock pair
        // invites the moment a step times out, is skipped, or is left by a Skip.
        _playableCards = null;

        _sequence?.Tick();

        ApplyHandLock();
    }

    private IEnumerator RunWhenMatchStarts()
    {
        ServiceLocator.TryGet(out _save);
        ServiceLocator.TryGet(out _rewards);

        yield return new WaitUntil(() => ServiceLocator.TryGet(out _gameFlow) && _gameFlow != null);
        yield return new WaitUntil(() => _gameFlow.CurrentGameState.Value == GameState.InMatch);

        // InMatch is not the moment the player can see the board: the server gets there while both names
        // are still up (MatchReady holds 2s, the intro 3s plus its fade). Starting on it put the first line,
        // and the freeze that comes with it, on top of the loading screen.
        if (ServiceLocator.TryGet(out IMatchIntroduction intro))
        {
            // Bounded, and unscaled like every other tutorial timeout: an intro that never clears may delay
            // the tutorial, never cancel it.
            float giveUpAt = Time.unscaledTime + IntroductionTimeoutSeconds;
            yield return new WaitUntil(() => intro.IsFinished || Time.unscaledTime >= giveUpAt);

            if (!intro.IsFinished)
                GameLog.Warn($"[Tutorial] The match intro was still up after {IntroductionTimeoutSeconds:0.#}s; " +
                             "starting anyway so the tutorial cannot dead-end.");
        }

        // Resolved after the match is live: the hand and the deployers only exist from that point.
        ServiceLocator.TryGet(out _teamManager);
        ServiceLocator.TryGet(out _cardContainer);
        ServiceLocator.TryGet(out _towerDeployer);
        ServiceLocator.TryGet(out _deploymentBus);

        _localTeam = _teamManager != null ? _teamManager.GetLocalTeam() : TeamType.None;
        _deckManaCost = ResolveDeckManaCost();

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
    /// <remarks>The two tower steps also settle: the world runs until the tower has finished arriving, or
    /// the next step's freeze would hold it as a dot mid-spawn while the player is told to upgrade it.</remarks>
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
            .WhileWaiting(Asking(AnyOf(ExistingTypesOfCard.Tower)))
            .CompletesWhen(() => _towerPlaced)
            .SettlingUntil(IsLastBuiltTowerSettled)
            .Pointing(PointAtTowerPlacement)
            .GivingUpAfter(60f),

        new TutorialStep(TutorialStepId.LevelUpTower)
            .WhileWaiting(Asking(IsUpgradeCard))
            .CompletesWhen(() => _towerLevelledUp)
            .SettlingUntil(IsLastBuiltTowerSettled)
            .Pointing(PointAtTowerUpgrade)
            .GivingUpAfter(60f),

        // The opponent's field sits ABOVE ours, and CameraSlide moves the camera against the finger (the
        // board follows the drag), so reaching it is a swipe DOWN and coming home a swipe UP.
        new TutorialStep(TutorialStepId.SwapToEnemyMap)
            .CompletesWhen(() => _cameraSide == CameraSide.Enemy)
            .Pointing(() => TutorialHighlight.World(ScreenCentreWorld(), 2.5f, TutorialHintKind.SwipeDown))
            .GivingUpAfter(45f),

        new TutorialStep(TutorialStepId.SendTroop)
            .WhileWaiting(Asking(AnyOf(ExistingTypesOfCard.Enemy)))
            .CompletesWhen(() => _troopSent)
            .Pointing(() => PointAtCardDrop(ExistingTypesOfCard.Enemy, enemyFieldAnchor))
            .GivingUpAfter(60f),

        // The one lesson that cannot be taught frozen: what a troop does is where it walks. This runs live so
        // the player watches theirs climb the opponent's lane while that lane's own waves come down it — and
        // it is the only place the board shows that the traffic goes both ways.
        // It carries a timeout despite being a tap step: every other read-this beat is safe to leave open
        // because the world is stopped, and this one is not.
        new TutorialStep(TutorialStepId.TroopDirection)
            .OnlyWhen(() => _troopSent)
            .Tap()
            .Running()
            .Pointing(PointAtSentTroop)
            .GivingUpAfter(30f),

        // The category before the card: a spell's field is part of what it IS, and the player is about to
        // meet one of each. Told here rather than at the first cast because this is where both are in hand
        // and the offensive one is about to be used on the field it belongs to.
        new TutorialStep(TutorialStepId.SpellKinds)
            .Tap()
            .Pointing(() => PointAtCardInHand(CardType.SpellRage)),

        // Rage, on the troops in their lane, named rather than left as "a spell": it only speeds up what is
        // already attacking that field, so dropped on bare lane it buffs nothing. Live for the reason the
        // beat before it is — a frozen troop cannot be seen surging — and watched afterwards, because the
        // next step freezes and the effect would stop on the frame the cast landed.
        new TutorialStep(TutorialStepId.CastSpell)
            .WhileWaiting(Asking(Only(CardType.SpellRage)))
            .Running()
            .CompletesWhen(() => HasPlayed(CardType.SpellRage))
            .Watching(SpellWatchSeconds)
            .Pointing(PointAtRageTarget)
            .GivingUpAfter(60f),

        new TutorialStep(TutorialStepId.SwapBackHome)
            .CompletesWhen(() => _cameraSide == CameraSide.Local)
            .Pointing(() => TutorialHighlight.World(ScreenCentreWorld(), 2.5f, TutorialHintKind.SwipeUp))
            .GivingUpAfter(45f),

        // The defensive half, and the one step that CANNOT be taught frozen at all: a stopped clock never
        // walks an enemy into the player's lane, so there would be nothing to aim at. Fireball damages only
        // the enemies attacking the caster's own map, which is exactly why it is taught at home, on the wave
        // that is already coming — the spell pair is what teaches that a side is part of a card.
        new TutorialStep(TutorialStepId.CastDefensiveSpell)
            .WhileWaiting(Asking(Only(CardType.SpellFireball)))
            .Running()
            .CompletesWhen(() => HasPlayed(CardType.SpellFireball))
            .Pointing(PointAtFireballTarget)
            .GivingUpAfter(60f),

        // The one step that lets the world run again: the reward lands here, and the board moving behind it
        // is what makes the hand-off feel like the end of a match rather than the end of a slideshow. The
        // reward card is shown on entry and stays up through the hand-off (see LeaveToMenu).
        new TutorialStep(TutorialStepId.MatchOutro)
            .Tap()
            .Running()
            .Entering(GrantTutorialReward)
            .Formatting(() => new object[] { RewardCardName }),
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

    /// <summary>
    /// The troop the player just sent, framed while it marches. Re-resolved every frame like every other
    /// highlight, so the ring travels with it — which is the step: the troop entered the opponent's lane at
    /// the end their own waves finish at, and walks back up it.
    /// </summary>
    /// <remarks>One killed, or arrived, mid-step leaves the copy with no ring rather than a hole cut over
    /// nothing.</remarks>
    private TutorialHighlight PointAtSentTroop()
    {
        EnemyManager troop = FindSentTroop();

        return troop != null
            ? TutorialHighlight.World(troop.transform.position, SentTroopHighlightRadius, TutorialHintKind.None)
            : TutorialHighlight.None;
    }

    /// <summary>
    /// Our furthest-along troop on the opponent's lane — the leader, since the card sends a small column and
    /// they walk close enough together for one ring to hold them all.
    /// </summary>
    /// <remarks>
    /// "Ours" is read off the reversed flag, the very thing that makes it walk the other way: a wave enemy on
    /// that lane is never reversed, and anything the bot sends is walking OUR lane, so it carries our team
    /// rather than theirs. Reading the server movement directly is safe for the same reason the tower lookups
    /// are — the tutorial is always a local host.
    /// </remarks>
    private EnemyManager FindSentTroop()
    {
        EnemyManager leader = null;
        float furthest = -1f;

        foreach (EnemyManager enemy in EnemyRegistry.ActiveEnemies)
        {
            if (enemy == null || enemy.Team == null || enemy.ServerMovement == null) continue;

            TeamType attackedMap = enemy.Team.GetTeamType();
            if (attackedMap == _localTeam || attackedMap == TeamType.None) continue;
            if (!enemy.ServerMovement.Reversed.Value) continue;

            float progress = enemy.ServerMovement.Progress;
            if (progress <= furthest) continue;

            leader = enemy;
            furthest = progress;
        }

        return leader;
    }

    private TutorialHighlight PointAtCardDrop(ExistingTypesOfCard family, Transform anchor)
    {
        AbstractCard card = FindCardInHand(family);
        if (card == null || anchor == null) return TutorialHighlight.None;

        return TutorialHighlight.DragUiToWorld(card.Rect, anchor.position);
    }

    /// <summary>The same drag hint, for a step that names one exact card rather than a family.</summary>
    private TutorialHighlight PointAtCardDrop(CardType cardType, Vector3 target)
    {
        AbstractCard card = FindCardInHand(cardType);

        return card != null ? TutorialHighlight.DragUiToWorld(card.Rect, target) : TutorialHighlight.None;
    }

    /// <summary>A card in hand, ringed but not dragged anywhere — for a step that talks about it rather
    /// than asking for it.</summary>
    private TutorialHighlight PointAtCardInHand(CardType cardType)
    {
        AbstractCard card = FindCardInHand(cardType);

        return card != null
            ? TutorialHighlight.Ui(card.Rect, TutorialHintKind.None)
            : TutorialHighlight.None;
    }

    /// <summary>
    /// Rage -> the troops walking the opponent's lane, the only thing it can speed up. The player's own
    /// sent troop is preferred — it is the one they just watched march and the one the copy is about — and
    /// anything else attacking that field will do, since Rage buffs the lane rather than an allegiance.
    /// Falls back to the enemy field anchor when the lane is momentarily empty.
    /// </summary>
    private TutorialHighlight PointAtRageTarget()
    {
        EnemyManager troop = FindSentTroop() ?? FindAnyTroopOnEnemyLane();
        if (troop != null) return PointAtCardDrop(CardType.SpellRage, troop.transform.position);

        return enemyFieldAnchor != null
            ? PointAtCardDrop(CardType.SpellRage, enemyFieldAnchor.position)
            : TutorialHighlight.None;
    }

    /// <summary>
    /// Fireball -> the enemy furthest down the player's own lane. Falls back to the home anchor for the
    /// moment before the wave arrives; the step runs live, so one is on its way.
    /// </summary>
    private TutorialHighlight PointAtFireballTarget()
    {
        EnemyManager threat = FindWorstThreatOnOurLane();
        if (threat != null) return PointAtCardDrop(CardType.SpellFireball, threat.transform.position);

        return localFieldAnchor != null
            ? PointAtCardDrop(CardType.SpellFireball, localFieldAnchor.position)
            : TutorialHighlight.None;
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

    /// <summary>
    /// Anything walking the opponent's lane, ours or their own incoming wave. Rage buffs whatever is
    /// attacking that field, so either is a target the cast actually lands on — this is the fallback for
    /// the moment the player's own troops have already died or arrived.
    /// </summary>
    private EnemyManager FindAnyTroopOnEnemyLane()
    {
        foreach (EnemyManager enemy in EnemyRegistry.ActiveEnemies)
        {
            if (enemy == null || enemy.Team == null) continue;

            TeamType attackedMap = enemy.Team.GetTeamType();
            if (attackedMap != TeamType.None && attackedMap != _localTeam) return enemy;
        }

        return null;
    }

    /// <summary>
    /// The enemy furthest down the player's own lane. An enemy's team is the map it attacks, so this covers
    /// the waves and anything the bot has sent alike — everything a Fireball can hit, since its executor
    /// filters to the caster's own map.
    /// </summary>
    /// <remarks>Furthest along is both the enemy actually about to cost the player health and the one
    /// certain to be out of its spawn invincibility: <c>ServerEnemyHealth.TakeDamage</c> drops the hit
    /// outright while that is up, so aiming at a fresh spawn would teach a cast that does nothing.</remarks>
    private EnemyManager FindWorstThreatOnOurLane()
    {
        EnemyManager worst = null;
        float furthest = -1f;

        foreach (EnemyManager enemy in EnemyRegistry.ActiveEnemies)
        {
            if (enemy == null || enemy.Team == null || enemy.ServerMovement == null) continue;
            if (enemy.Team.GetTeamType() != _localTeam) continue;
            if (!enemy.ServerMovement.IsTargetable) continue;

            float progress = enemy.ServerMovement.Progress;
            if (progress <= furthest) continue;

            worst = enemy;
            furthest = progress;
        }

        return worst;
    }

    // ---- Observations ---------------------------------------------------------------------------

    private bool HasPlayed(CardType cardType) => _cardsPlayed.Contains(cardType);

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

        // Both outcomes start an animation and a setup window on that tower, and the step they complete
        // waits for both to finish before the next one freezes the world again.
        _lastBuiltTower = FindLocalTowerAt(result.Position);

        if (_lastBuiltTower == null)
            GameLog.Warn($"[Tutorial] No tower of ours at {result.Position}; the next step will not wait for it.");
    }

    /// <summary>
    /// The player's tower standing on <paramref name="position"/>. A place result carries server space, which
    /// on the tutorial's host is world space too: the host is the server, so its towers sit where it put them.
    /// </summary>
    private TowerManager FindLocalTowerAt(Vector2 position)
    {
        TowerManager closest = null;
        float closestSqr = TowerMatchRadius * TowerMatchRadius;

        foreach (TowerManager tower in TowerRegistry.ActiveTowers)
        {
            if (tower == null || tower.Team == null || tower.Team.GetTeamType() != _localTeam) continue;

            float sqr = ((Vector2)tower.transform.position - position).sqrMagnitude;
            if (sqr > closestSqr) continue;

            closest = tower;
            closestSqr = sqr;
        }

        return closest;
    }

    /// <summary>
    /// Whether the tower the player just built or upgraded has finished arriving: its setup window is over
    /// and its spawn or upgrade animation has played out. Both run on scaled time, so under a frozen step
    /// they stop where they are - a fresh tower stays a dot at 1% scale.
    /// </summary>
    /// <remarks>Reads the server combat directly, which only works because the tutorial is a local host.</remarks>
    private bool IsLastBuiltTowerSettled()
    {
        // Gone, or never found: there is nothing left to wait for.
        if (_lastBuiltTower == null) return true;

        if (_lastBuiltTower.ServerTowerCombat.IsSettingUp) return false;

        ClientTowerGFX gfx = _lastBuiltTower.GetComponentInChildren<ClientTowerGFX>();
        return gfx == null || !gfx.IsPlayingLevelFeedback;
    }

    private void HandleCardDeployed(CardDeployedEventArgs args)
    {
        // The bot plays through the same deployers, so a deploy only counts when it was the player's.
        if (args.TeamDeployed != _localTeam) return;

        CardDataSO card = settings != null && settings.CardDataList != null
            ? settings.CardDataList.GetCardDataByType(args.CardDeployed)
            : null;

        if (card == null) return;

        _cardsPlayed.Add(args.CardDeployed);

        if (card.ExistingType == ExistingTypesOfCard.Enemy) _troopSent = true;
    }

    private void HandleCameraSideChanged(CameraSide side) => _cameraSide = side;

    // ---- Reward ---------------------------------------------------------------------------------

    /// <summary>
    /// Rolls and banks the payout as the outro appears, so the outro can name the card and show its icon.
    /// Granted straight through <see cref="BaseRewardService"/>: the payout is authored rather than rolled
    /// for value, and the tutorial never reaches a real win condition to hang an end-of-match roll off.
    /// </summary>
    private void GrantTutorialReward()
    {
        if (_rewardGranted || _rewards == null || _save == null) return;

        _rewardGranted = true;
        _reward = new TutorialRewardRoller(settings, _save).Roll();

        if (_reward.IsEmpty) return;

        _rewards.Grant(_reward);

        if (overlay != null) overlay.ShowReward(_reward, RewardCardData);
    }

    private CardDataSO RewardCardData =>
        _reward.HasCard && settings != null && settings.CardDataList != null
            ? settings.CardDataList.GetCardDataByType(_reward.Card)
            : null;

    /// <summary>The card's name for the outro line. The payout is always a card unless the player's whole
    /// collection sits in their deck; the gold wording only keeps the sentence readable in that case.</summary>
    private string RewardCardName
    {
        get
        {
            CardDataSO card = RewardCardData;
            if (card != null) return card.CardName;

            return _reward.Gold > 0 ? $"{_reward.Gold} gold" : "a reward";
        }
    }

    /// <summary>
    /// Everything a step that asks for a card needs on every frame it waits: mana to afford the ask with,
    /// and a hand narrowed to the card being asked for.
    /// </summary>
    /// <remarks>The two travel together because they answer the same question — can the player do what they
    /// were just told to, and only that? A step with no card to ask for keeps <see cref="RefillMana"/>.</remarks>
    private Action Asking(Func<AbstractCard, bool> playable) => () =>
    {
        RefillMana();
        _playableCards = playable;
    };

    private static Func<AbstractCard, bool> AnyOf(ExistingTypesOfCard family) =>
        card => card.CardData.ExistingType == family;

    private static Func<AbstractCard, bool> Only(CardType cardType) =>
        card => card.CardData.CardType == cardType;

    /// <summary>
    /// The card that can upgrade the tower just built — only one of that tower's own type can, which is the
    /// rule the deployer enforces and the same one the upgrade hint follows. Falls back to any tower card
    /// when there is no tower to match (the place step timed out), rather than closing the whole hand.
    /// </summary>
    private bool IsUpgradeCard(AbstractCard card) =>
        _placedTowerCard == CardType.None
            ? card.CardData.ExistingType == ExistingTypesOfCard.Tower
            : card.CardData.CardType == _placedTowerCard;

    /// <summary>
    /// Closes every card the running step did not ask for, so a stray drop cannot spend the card that step
    /// is waiting on — or the mana it needs — and leave the player stuck on an instruction they can no
    /// longer carry out.
    /// </summary>
    /// <remarks>
    /// Applied every frame rather than on entry and exit: the hand re-deals under a step (a played card is
    /// replaced at once on a deck this size), and a card arriving on its slot opens itself.
    /// </remarks>
    private void ApplyHandLock()
    {
        if (_cardContainer == null) return;

        // A predicate nothing matches would close the whole hand and strand the player, which is never what
        // a step means — it is what a step whose card has not been dealt back yet, or whose premise did not
        // hold, would ask for by accident. The hand is left open instead.
        bool anyMatch = false;

        if (_playableCards != null)
        {
            foreach (AbstractCard card in _cardContainer.CardsInHand)
            {
                if (card == null || card.CardData == null || !_playableCards(card)) continue;

                anyMatch = true;
                break;
            }
        }

        foreach (AbstractCard card in _cardContainer.CardsInHand)
        {
            if (card == null || card.CardData == null) continue;

            card.SetInteractable(!anyMatch || _playableCards(card));
        }
    }

    /// <summary>
    /// Keeps the player topped up for as long as an action step is waiting. A frozen step regenerates no
    /// mana at all, so without this a player who spent down to nothing would sit on an instruction they can
    /// never carry out until its timeout fired.
    /// </summary>
    /// <remarks>Every frame rather than on entry: the deploy that completed the previous step spends on the
    /// server when its Rpc is processed, which can land after the next step has already entered and
    /// refilled - leaving the player short on a step that just told them it had filled their bar.</remarks>
    private void RefillMana()
    {
        if (_localTeam == TeamType.None) return;
        if (!ServiceLocator.TryGet(out BaseServerManaManager mana)) return;

        RaiseManaCapForDeck(mana);
        mana.GrantMana(_localTeam, mana.GetMaxMana(_localTeam));
    }

    /// <summary>
    /// Lifts the player's mana ceiling to cover the costliest card the tutorial lends them, if it does not
    /// already. The shared table drops the cap to 4 at wave 1 while the lent deck holds a 5-mana spell, so
    /// without this the step that asks for it points at a card the player can never afford and can only
    /// time out — the same failure <see cref="RefillMana"/> exists to prevent, one layer up.
    /// </summary>
    /// <remarks>
    /// Raised only, only for the player's own team, and re-applied every frame an action step waits, so a
    /// new wave's own cap can neither be lowered past what the deck needs nor stay lowered. The bot is left
    /// on the table's value: this buys the script its cards, not the tutorial an easier opponent.
    /// </remarks>
    private void RaiseManaCapForDeck(BaseServerManaManager mana)
    {
        if (_deckManaCost <= 0f) return;

        NetworkVariable<float> max = mana.GetMaxManaNetworkVariable(_localTeam);
        if (max != null && max.Value < _deckManaCost) max.Value = _deckManaCost;
    }

    /// <summary>
    /// The costliest card in the lent deck. Read from the deck rather than authored, so re-authoring the
    /// deck cannot leave a card in it that no step can ever pay for.
    /// </summary>
    private float ResolveDeckManaCost()
    {
        if (settings == null || settings.TutorialDeck == null || settings.CardDataList == null) return 0f;

        float highest = 0f;

        foreach (CardType cardType in settings.TutorialDeck)
        {
            CardDataSO card = settings.CardDataList.GetCardDataByType(cardType);
            if (card != null && card.Cost > highest) highest = card.Cost;
        }

        return highest;
    }

    // ---- Handover -------------------------------------------------------------------------------

    private void HandleSkipTapped()
    {
        if (_leaving) return;

        GameLog.Info("[Tutorial] Player skipped the tutorial match.");

        _sequence?.Stop();
        _tutorial?.Abandon();

        StartCoroutine(LeaveToMenu(completed: false));
    }

    private void HandleSequenceFinished() => StartCoroutine(LeaveToMenu(completed: true));

    /// <summary>
    /// Hands the player to the Main Menu: for the second half when the match was played through, straight
    /// to the menu when it was skipped.
    /// </summary>
    /// <remarks>
    /// The payout itself was already banked when the outro appeared (see <see cref="GrantTutorialReward"/>),
    /// so this only carries the card across. Rolling here instead would mean the outro could not name or
    /// show what the player had won.
    /// </remarks>
    private IEnumerator LeaveToMenu(bool completed)
    {
        if (_leaving) yield break;
        _leaving = true;

        Unsubscribe();

        // The match is over either way; a Skip tapped during the wait below would abandon a tutorial that
        // has just been finished.
        if (overlay != null)
        {
            overlay.OnSkipTapped -= HandleSkipTapped;
            overlay.SetSkipVisible(false);
        }

        if (completed)
        {
            ShowRewardAlone();

            if (settings != null && settings.OutroSeconds > 0f)
                yield return new WaitForSecondsRealtime(settings.OutroSeconds);

            // Arms the menu half before the scene change, so the menu director finds the phase already set
            // the moment it wakes.
            _tutorial?.BeginMenuPhase(_reward.HasCard ? _reward.Card : CardType.None);
        }

        // Fire-and-forget on purpose: LeaveMatchAsync owns the host teardown and the scene load, and a
        // coroutine cannot await a Task anyway (yielding one would just wait a single frame).
        if (ServiceLocator.TryGet(out BaseClientManager clientManager))
            _ = clientManager.LeaveMatchAsync();
        else
            Loader.Load(Loader.Scene.MainMenu);
    }

    /// <summary>
    /// Puts the reward back on screen, alone on the dim. The sequence clears the overlay as it finishes, and
    /// without this the player would watch a bare board — and then the host tearing it down — for the whole
    /// outro wait, instead of what they just won.
    /// </summary>
    private void ShowRewardAlone()
    {
        if (overlay == null || !_rewardGranted || _reward.IsEmpty) return;

        overlay.Show(string.Empty, showContinue: false);
        overlay.SetHighlight(TutorialHighlight.None);
        overlay.ShowReward(_reward, RewardCardData);
    }
}
