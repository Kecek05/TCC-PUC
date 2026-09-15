using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Default <see cref="BaseTutorialService"/>. Holds the phase, lends the player the authored tutorial deck
/// for the duration of the scripted match, and writes the one persistent bit when it is over.
/// </summary>
public class TutorialService : BaseTutorialService
{
    private readonly BasePlayerSaveManager _save;
    private readonly TutorialSettingsSO _settings;
    private readonly UserData _userData;

    /// <summary>The player's own deck, parked while the tutorial deck is on loan. Never touches the save —
    /// only <see cref="UserData"/>, which is the connection payload and is rebuilt from the save anyway.</summary>
    private List<CardType> _borrowedDeck;
    private List<int> _borrowedLevels;

    private TutorialPhase _phase = TutorialPhase.None;
    private CardType _rewardCard = CardType.None;

    public TutorialService(BasePlayerSaveManager save, TutorialSettingsSO settings, UserData userData)
    {
        _save = save;
        _settings = settings;
        _userData = userData;

        if (_save == null) GameLog.Error($"[{nameof(TutorialService)}] Created without a player save.");
        if (_settings == null) GameLog.Error($"[{nameof(TutorialService)}] Created without TutorialSettingsSO; the tutorial is disabled.");
    }

    public override bool ShouldRunOnBoot => _settings != null && _save != null && !_save.TutorialCompleted;

    public override TutorialPhase Phase => _phase;

    public override CardType RewardCard => _rewardCard;

    public override async Task<bool> StartMatchPhaseAsync()
    {
        if (_settings == null) return false;

        if (!ServiceLocator.TryGet(out BaseHostManager hostManager))
        {
            GameLog.Error($"[{nameof(TutorialService)}] No host manager; cannot start the tutorial match.");
            return false;
        }

        LendTutorialDeck();
        SetPhase(TutorialPhase.Match);

        // Local host: no relay, no lobby, and the bot seats without waiting - see StartLocalHostAsync.
        bool started = await hostManager.StartLocalHostAsync(Loader.Scene.TutorialScene);

        if (!started)
        {
            GameLog.Error($"[{nameof(TutorialService)}] Failed to host the tutorial match; falling back to the menu.");
            ReturnBorrowedDeck();
            SetPhase(TutorialPhase.None);
            Loader.Load(Loader.Scene.MainMenu);
        }

        return started;
    }

    public override void BeginMenuPhase(CardType rewardCard)
    {
        _rewardCard = rewardCard;

        // The scripted match is over, so the loan ends here rather than when the menu scene loads: the
        // connection payload is rebuilt from UserData on the next match, and that must be the real deck.
        ReturnBorrowedDeck();
        SetPhase(TutorialPhase.Menu);
    }

    public override void Complete()
    {
        _save?.SetTutorialCompleted(true);
        _rewardCard = CardType.None;
        SetPhase(TutorialPhase.None);

        GameLog.Info($"[{nameof(TutorialService)}] Tutorial completed.");
    }

    public override void Abandon()
    {
        ReturnBorrowedDeck();

        // Written as completed on purpose: opting out is a decision, and re-running the tutorial on the
        // next launch would override it. The debug menu is what puts a player back into it.
        _save?.SetTutorialCompleted(true);
        _rewardCard = CardType.None;
        SetPhase(TutorialPhase.None);

        GameLog.Info($"[{nameof(TutorialService)}] Tutorial abandoned; marked completed so it will not run again.");
    }

    /// <summary>
    /// Swaps the authored tutorial deck into the connection payload. Scripting "place a tower" is only
    /// safe when a tower is guaranteed to be in the deck, and the player's own deck is theirs.
    /// </summary>
    private void LendTutorialDeck()
    {
        if (_userData == null || _settings.TutorialDeck == null || _settings.TutorialDeck.Count == 0) return;
        if (_borrowedDeck != null) return; // already on loan

        _borrowedDeck = new List<CardType>(_userData.DeckCards);
        _borrowedLevels = new List<int>(_userData.DeckCardLevels);

        List<CardType> deck = new(_settings.TutorialDeck);
        List<int> levels = new(deck.Count);
        for (int i = 0; i < deck.Count; i++) levels.Add(_settings.DeckCardLevel);

        _userData.SetDeckCards(deck);
        _userData.SetDeckCardLevels(levels);
    }

    private void ReturnBorrowedDeck()
    {
        if (_borrowedDeck == null) return;

        _userData?.SetDeckCards(_borrowedDeck);
        _userData?.SetDeckCardLevels(_borrowedLevels);

        _borrowedDeck = null;
        _borrowedLevels = null;
    }

    private void SetPhase(TutorialPhase phase)
    {
        if (_phase == phase) return;

        _phase = phase;
        RaisePhaseChanged(phase);
    }
}
