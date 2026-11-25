using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Decides which music track loops, and is the only caller of <see cref="BaseSfxService.PlayMusic"/>: the menu
/// track in the Main Menu, the match track from the moment a match is actually starting until its end screen
/// is left. Every other scene keeps whatever is playing, so the loading screen never cuts the music.
/// </summary>
/// <remarks>
/// <para>The host waits for an opponent INSIDE the match scene (the match-search pill), so the scene alone is
/// not the cue: the menu track carries on through <see cref="GameState.WaitingForPlayers"/> and the match track
/// comes in at <see cref="GameState.MatchReady"/>, under the VS intro. A joining client arrives later in the
/// flow and gets the same answer from the same state.</para>
/// <para>It polls rather than subscribing to <c>CurrentGameState.OnValueChanged</c>: a client never gets that
/// callback for the value it is first synchronised with, and polling one enum a frame costs nothing.</para>
/// </remarks>
public class MusicDirector : MonoBehaviour
{
    public enum Track { Keep, Menu, Match }

    private const float FlowLookupInterval = 0.5f;

    private SfxLibrarySO _library;
    private BaseSfxService _sfx;
    private BaseGameFlowManager _flow;
    private float _nextFlowLookup;
    private Track _requested = Track.Keep;

    public static MusicDirector Create(SfxLibrarySO library, BaseSfxService sfx)
    {
        GameObject host = new GameObject("[Juice] Music");
        DontDestroyOnLoad(host);

        MusicDirector director = host.AddComponent<MusicDirector>();
        director._library = library;
        director._sfx = sfx;
        return director;
    }

    /// <summary>
    /// Which track a scene and a match state call for. <see cref="Track.Keep"/> leaves the current one playing.
    /// </summary>
    public static Track Resolve(string sceneName, GameState state)
    {
        if (sceneName == Loader.Scene.MainMenu.ToString()) return Track.Menu;
        if (!Loader.IsGameplayScene(sceneName)) return Track.Keep;

        return state is GameState.MatchReady or GameState.DrawingCards or GameState.InMatch or GameState.EndMatch
            ? Track.Match
            : Track.Keep;
    }

    private void OnEnable() => SceneManager.activeSceneChanged += OnActiveSceneChanged;
    private void OnDisable() => SceneManager.activeSceneChanged -= OnActiveSceneChanged;

    // The flow manager belongs to the scene that just left; look again in the new one straight away.
    private void OnActiveSceneChanged(Scene previous, Scene next)
    {
        _flow = null;
        _nextFlowLookup = 0f;
    }

    private void Update()
    {
        if (_library == null || _sfx == null) return;

        string sceneName = SceneManager.GetActiveScene().name;
        GameState state = GameState.None;
        if (Loader.IsGameplayScene(sceneName))
        {
            if (_flow == null && Time.unscaledTime >= _nextFlowLookup)
            {
                _flow = FindFirstObjectByType<BaseGameFlowManager>();
                _nextFlowLookup = Time.unscaledTime + FlowLookupInterval;
            }

            if (_flow != null) state = _flow.CurrentGameState.Value;
        }

        Track wanted = Resolve(sceneName, state);
        if (wanted == Track.Keep || wanted == _requested) return;

        _requested = wanted;
        _sfx.PlayMusic(wanted == Track.Menu ? _library.MenuMusic : _library.MatchMusic);
    }
}
