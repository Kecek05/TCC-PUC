using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>One sound cue: which clips may play for it, on which bus, how loud.</summary>
[Serializable]
public class SfxCue
{
    public SfxId Id;
    public SfxBus Bus = SfxBus.Game;

    [Tooltip("One is picked at random per play. Empty means the cue is silent.")]
    public AudioClip[] Clips = Array.Empty<AudioClip>();

    [Range(0f, 1f)] public float Volume = 1f;

    [Tooltip("Seconds before the same cue may play again. Negative uses the sfx-cooldown token.")]
    public float Cooldown = -1f;

    [Tooltip("Shift the pitch by up to the pitch-jitter token on every play, so a repeat never sounds identical.")]
    public bool Jitter = true;

    [Tooltip("Dip the music by duck-music for as long as this clip plays. For the T4 beats: victory, defeat, a banner's horn.")]
    public bool DucksMusic;
}

/// <summary>
/// Every sound in the game, by <see cref="SfxId"/>. Swapping a placeholder for a final clip is an edit to this
/// asset only: no presenter names a clip.
/// </summary>
/// <remarks>Lives in Resources so the sound service can load it in any scene, the boot flow or not.</remarks>
[CreateAssetMenu(fileName = "SfxLibrary", menuName = "Scriptable Objects/Presentation/Sfx Library")]
public class SfxLibrarySO : ScriptableObject
{
    public const string ResourcePath = "Juice/SfxLibrary";

    [TableList(ShowIndexLabels = false, AlwaysExpanded = true)]
    public List<SfxCue> Cues = new();

    [Title("Music")]
    [Tooltip("Loops in the menus, and while the host waits for an opponent. Empty: no music there, and the Music " +
             "setting is hidden unless the match has a track. Kecek/Juice/Assign Music fills it from " +
             "Assets/Audio/Music/music_menu.")]
    public AudioClip MenuMusic;

    [Tooltip("Loops from the moment a match starts (MatchReady) to its end screen. Empty: no music in matches. " +
             "Kecek/Juice/Assign Music fills it from Assets/Audio/Music/music_match.")]
    public AudioClip MatchMusic;

    private Dictionary<SfxId, SfxCue> _byId;

    public bool HasMusic => MenuMusic != null || MatchMusic != null;

    public SfxCue Get(SfxId id)
    {
        if (_byId == null)
        {
            _byId = new Dictionary<SfxId, SfxCue>(Cues.Count);
            foreach (SfxCue cue in Cues)
                if (cue != null && !_byId.ContainsKey(cue.Id)) _byId.Add(cue.Id, cue);
        }

        return _byId.TryGetValue(id, out SfxCue found) ? found : null;
    }

    private void OnValidate() => _byId = null;
}
