using QFSW.QC;
using UnityEngine.SceneManagement;

/// <summary>
/// Quantum Console command for the local save as a whole. <see cref="ProgressionDebugCommands"/> and
/// <see cref="RewardDebugCommands"/> edit a save; this one starts it over, which is what testing anything a
/// brand-new player sees needs — the tutorial above all. "Replay Tutorial On Next Launch" keeps the
/// collection, so the tutorial then runs against a player who already owns every card.
/// </summary>
/// <remarks>
/// Goes through <see cref="BasePlayerSaveManager.ResetToDefault"/> rather than deleting the file: the save
/// lives in memory for the whole session, and its next write would put the old one straight back. Unlike
/// the editor's "Delete Player Save", it also works in a build.
/// </remarks>
public static class SaveDebugCommands
{
    [Command("reset-save",
        "Wipes the local save back to a brand-new player's - starter deck, starting gold, only the starter " +
        "cards owned, tutorial pending - then routes the way a first launch does: into the tutorial. " +
        "Not available mid-match.")]
    private static string ResetSave(
        [CommandParameterDescription("True marks the tutorial as done, so the reset lands in the Main Menu " +
                                     "instead of replaying the tutorial.")]
        bool skipTutorial = false)
    {
        if (!ServiceLocator.TryGet(out BasePlayerSaveManager save))
            return "No BasePlayerSaveManager registered - enter play from StartScene so ClientManager creates it.";

        // The match has already dealt from the old deck, and the tutorial's deck loan would hand the old deck
        // back to the connection payload on the way out, so a reset from here would not stick.
        if (Loader.IsGameplayScene(SceneManager.GetActiveScene().name))
            return "Leave the match first (in the tutorial: Skip) - a reset mid-match would not stick.";

        ServiceLocator.TryGet(out BaseTutorialService tutorial);

        // A menu half still in progress would otherwise resume on the reloaded menu, pointing at a reward
        // card the new save does not own. Abandon's write to the save is replaced by the reset below.
        if (tutorial != null && tutorial.Phase != TutorialPhase.None) tutorial.Abandon();

        save.ResetToDefault();
        if (skipTutorial) save.SetTutorialCompleted(true);

        // The same route a finished boot takes (ClientManager.RouteAfterAuth). Reloading the menu rather
        // than leaving it up matters too: every page re-reads the save when it loads.
        if (tutorial != null && tutorial.ShouldRunOnBoot)
        {
            _ = tutorial.StartMatchPhaseAsync();
            return "Save reset to a new player's. Starting the tutorial...";
        }

        Loader.Load(Loader.Scene.MainMenu);
        return skipTutorial
            ? "Save reset to a new player's, tutorial marked done. Reloading the Main Menu..."
            : "Save reset to a new player's. Reloading the Main Menu...";
    }
}
