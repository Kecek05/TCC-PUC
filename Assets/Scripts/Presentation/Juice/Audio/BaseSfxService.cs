using UnityEngine;

/// <summary>
/// Plays the game's sounds by cue. Callers say what happened (<see cref="SfxId"/>); the service owns cooldowns,
/// pitch jitter, semitone ladders, the voice budget, the buses and the Sound and Music settings.
/// </summary>
/// <remarks>
/// Registered in <see cref="ServiceLocator"/> by <c>JuiceServices</c> at boot and kept across scenes. Most code
/// goes through <see cref="Juice.Sfx"/>, which tolerates the service being absent.
/// </remarks>
public abstract class BaseSfxService : MonoBehaviour
{
    /// <param name="pitchStep">Semitones up (or down, negative) from the cue's pitch: ladders climb one per item.</param>
    /// <param name="volume">Multiplies the cue's own volume and its bus.</param>
    public abstract void Play(SfxId id, int pitchStep = 0, float volume = 1f);

    /// <summary>
    /// Loops a music clip on the Music bus, crossfading from whatever was playing over the music-fade token.
    /// Null fades the music out. <see cref="MusicDirector"/> is the one caller: it decides which track fits.
    /// </summary>
    public abstract void PlayMusic(AudioClip clip);

    /// <summary>The track playing (or fading in) now; null when there is no music.</summary>
    public abstract AudioClip CurrentMusic { get; }

    /// <summary>Dips the music by the duck-music token for a while, under a celebration or a banner.</summary>
    public abstract void DuckMusic(float seconds);
}
