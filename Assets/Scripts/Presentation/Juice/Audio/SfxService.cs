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
    private float _duckUntil = -1f;

    // Music is two sources so a change of track is a crossfade: the new one comes in on _music while the old
    // one fades out on _musicOut. Each fade is a 0..1 gain shaped to equal power, applied over _musicLevel
    // (the Music setting, the bus token and any duck), which eases on its own.
    private AudioSource _music;
    private AudioSource _musicOut;
    private float _musicGain;
    private float _musicOutGain;
    private float _musicLevel;

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

        _music = CreateSource("Music A");
        _music.loop = true;
        _musicOut = CreateSource("Music B");
        _musicOut.loop = true;

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

        // A T4 beat (victory, defeat, a banner's horn) gets the music out of its way for as long as it sounds.
        if (cue.DucksMusic) DuckMusic(clip.length / Mathf.Max(0.1f, pitch));
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

    public override AudioClip CurrentMusic => _music.clip;

    public override void PlayMusic(AudioClip clip)
    {
        if (_music.clip == clip && (clip == null || _music.isPlaying)) return;

        // Asked back for the track that is still fading out (menu, match, menu in quick succession): turn it
        // round where it is rather than restarting it from the top.
        if (clip != null && _musicOut.clip == clip && _musicOut.isPlaying)
        {
            (_music, _musicOut) = (_musicOut, _music);
            (_musicGain, _musicOutGain) = (_musicOutGain, _musicGain);
            return;
        }

        // The track playing now fades out; whatever was still fading out before it is cut, it is near silent.
        if (_music.clip != null && _music.isPlaying)
        {
            _musicOut.Stop();
            (_music, _musicOut) = (_musicOut, _music);
            _musicOutGain = _musicGain;
        }
        else
        {
            _music.Stop();
        }

        _music.clip = clip;
        _musicGain = 0f;
        if (clip != null) _music.Play();
    }

    public override void DuckMusic(float seconds)
    {
        _duckUntil = Mathf.Max(_duckUntil, Time.unscaledTime + seconds);
    }

    // Every frame: the level eases toward the Music setting, the bus token and any duck; the incoming track
    // fades in and the outgoing one out over music-fade; a source that has faded out lets go of its clip.
    private void Update()
    {
        if (_music == null) return;

        JuiceTokensSO tokens = JuiceTokensSO.Instance;
        float dt = Time.unscaledDeltaTime;

        bool enabled = _settings == null || (_settings.MusicEnabled && _settings.SoundEnabled);
        float target = enabled ? tokens.Audio.VolumeMusic : 0f;
        if (Time.unscaledTime < _duckUntil) target *= Mathf.Pow(10f, tokens.Audio.DuckMusicDb / 20f);
        _musicLevel = Mathf.MoveTowards(_musicLevel, target, dt * 2f);

        float fadeStep = dt / Mathf.Max(0.01f, tokens.Audio.MusicFade);
        _musicGain = Mathf.MoveTowards(_musicGain, 1f, fadeStep);
        _musicOutGain = Mathf.MoveTowards(_musicOutGain, 0f, fadeStep);

        if (_musicOut.clip != null && _musicOutGain <= 0f)
        {
            _musicOut.Stop();
            _musicOut.clip = null;
        }

        _music.volume = _musicLevel * EqualPower(_musicGain);
        _musicOut.volume = _musicLevel * EqualPower(_musicOutGain);
    }

    // A linear crossfade dips in the middle; a quarter sine keeps the sum of the two tracks' power constant.
    private static float EqualPower(float gain) => Mathf.Sin(Mathf.Clamp01(gain) * Mathf.PI * 0.5f);
}
