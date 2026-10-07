using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pooled 2D sound playback for every <see cref="SfxId"/>. A fixed set of AudioSources (the budget-voices
/// token) is created once; a new sound takes a free one or steals the quietest, so a busy wave can never
/// allocate sources or pile up hundreds of voices.
/// </summary>
/// <remarks>
/// AudioSources ignore <see cref="Time.timeScale"/>, which is what the tutorial needs: its frozen steps still
/// click and chime. Buses are volume multipliers from <see cref="JuiceTokensSO.Audio"/> rather than an
/// AudioMixer: the game has no mixer asset, and four multipliers do everything the plan asks of one.
/// </remarks>
public class SfxService : BaseSfxService
{
    private readonly List<AudioSource> _voices = new();
    private readonly Dictionary<SfxId, float> _lastPlayed = new();

    private SfxLibrarySO _library;
    private BaseGameSettingsService _settings;
    private AudioSource _music;
    private float _duckUntil = -1f;

    public static SfxService Create(SfxLibrarySO library, BaseGameSettingsService settings)
    {
        GameObject host = new GameObject("[Juice] Sfx");
        DontDestroyOnLoad(host);

        SfxService service = host.AddComponent<SfxService>();
        service.Initialize(library, settings);
        return service;
    }

    private void Initialize(SfxLibrarySO library, BaseGameSettingsService settings)
    {
        _library = library;
        _settings = settings;

        int voices = Mathf.Max(1, JuiceTokensSO.Instance.Budgets.Voices);
        for (int i = 0; i < voices; i++) _voices.Add(CreateSource("Voice " + i));

        _music = CreateSource("Music");
        _music.loop = true;

        if (_library == null)
            GameLog.Warn("[Juice] No SfxLibrary in Resources/" + SfxLibrarySO.ResourcePath + "; the game will be silent.");
    }

    private AudioSource CreateSource(string sourceName)
    {
        GameObject child = new GameObject(sourceName);
        child.transform.SetParent(transform, false);

        AudioSource source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        return source;
    }

    public override void Play(SfxId id, int pitchStep = 0, float volume = 1f)
    {
        if (id == SfxId.None || _library == null) return;
        if (_settings != null && !_settings.SoundEnabled) return;

        SfxCue cue = _library.Get(id);
        if (cue == null || cue.Clips == null || cue.Clips.Length == 0) return;

        JuiceTokensSO tokens = JuiceTokensSO.Instance;
        float now = Time.unscaledTime;
        float cooldown = cue.Cooldown >= 0f ? cue.Cooldown : tokens.Audio.Cooldown;
        if (_lastPlayed.TryGetValue(id, out float last) && now - last < cooldown) return;
        _lastPlayed[id] = now;

        AudioClip clip = cue.Clips.Length == 1 ? cue.Clips[0] : cue.Clips[Random.Range(0, cue.Clips.Length)];
        if (clip == null) return;

        float pitch = pitchStep == 0 ? 1f : Mathf.Pow(tokens.Audio.PitchStep, pitchStep);
        if (cue.Jitter) pitch *= 1f + Random.Range(-tokens.Audio.PitchJitter, tokens.Audio.PitchJitter);

        AudioSource voice = TakeVoice();
        voice.clip = clip;
        voice.pitch = pitch;
        voice.volume = Mathf.Clamp01(cue.Volume * BusVolume(cue.Bus, tokens) * volume);
        voice.Play();
    }

    // A free voice if there is one; otherwise the quietest gives way.
    private AudioSource TakeVoice()
    {
        AudioSource quietest = null;
        for (int i = 0; i < _voices.Count; i++)
        {
            AudioSource voice = _voices[i];
            if (!voice.isPlaying) return voice;
            if (quietest == null || voice.volume < quietest.volume) quietest = voice;
        }

        quietest.Stop();
        return quietest;
    }

    private static float BusVolume(SfxBus bus, JuiceTokensSO tokens) => bus switch
    {
        SfxBus.Ui => tokens.Audio.VolumeUi,
        SfxBus.Game => tokens.Audio.VolumeGame,
        SfxBus.Impact => tokens.Audio.VolumeImpact,
        SfxBus.Music => tokens.Audio.VolumeMusic,
        _ => 1f
    };

    public override void PlayMusic(AudioClip clip)
    {
        if (_music.clip == clip && (_music.isPlaying || clip == null)) return;

        _music.Stop();
        _music.clip = clip;
        if (clip != null) _music.Play();
    }

    public override void DuckMusic(float seconds)
    {
        _duckUntil = Mathf.Max(_duckUntil, Time.unscaledTime + seconds);
    }

    // Music volume eases toward its target every frame: the Music setting, the bus token and any duck.
    private void Update()
    {
        if (_music == null || _music.clip == null) return;

        JuiceTokensSO tokens = JuiceTokensSO.Instance;
        bool enabled = _settings == null || (_settings.MusicEnabled && _settings.SoundEnabled);
        float target = enabled ? tokens.Audio.VolumeMusic : 0f;
        if (Time.unscaledTime < _duckUntil) target *= Mathf.Pow(10f, tokens.Audio.DuckMusicDb / 20f);

        _music.volume = Mathf.MoveTowards(_music.volume, target, Time.unscaledDeltaTime * 2f);
    }
}
