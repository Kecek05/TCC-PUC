# Music

Two looping tracks: **menu** (menus, and while the host waits for an opponent) and **match** (from the VS
intro to the end screen). `MusicDirector` picks between them and `SfxService` crossfades; nothing else in
the game plays music.

## 1. Generate in Suno

*Create* in **Custom** mode, **Instrumental** on (no lyrics), the newest model your plan has.

| Track | Title | Style |
| --- | --- | --- |
| Menu | `Minimalist Defense - Menu` | `minimal electronic, instrumental, soft synth plucks, gentle arpeggios, warm pads, light percussion, clean, calm, focused, puzzle strategy game, 95 bpm, no vocals` |
| Match | `Minimalist Defense - Partida` | `minimal electronic, instrumental, driving pulse, arpeggiated synth, tight kick, light hi-hats, focused, tense but steady, strategy game, 124 bpm, steady energy, no vocals` |

*Exclude styles*, if the field is there: `vocals, choir, orchestral, epic, cinematic, dubstep, drop, guitar, heavy bass`.

Generate each 4-5 times and listen **quietly, as background**. Keep the one whose first minute holds a steady
energy (no build-up, drop or breakdown), whose melody is not so catchy it grates on the tenth repeat, and that
sounds like the same game as the other track. A strange intro or ending does not matter: only a stretch from
the middle is used.

Download as **WAV** (MP3 if the plan has no WAV) to `Art/Music/Suno/menu.wav` and `Art/Music/Suno/partida.wav`.
That folder is outside `Assets/`, so Unity never imports the raw exports.

## 2. Cut the loops

```
python -I Art/Music/make_loop.py analyze Art/Music/Suno/partida.wav
python -I Art/Music/make_loop.py cut Art/Music/Suno/partida.wav Assets/Audio/Music/music_match.wav --bpm 124 --start <downbeat> --bars 16 --preview
```

`analyze` estimates the tempo and the first downbeat and lists the loudness of every 4-bar stretch: pick a
start in a run of steady levels away from the intro and the ending. `cut` takes a whole number of bars,
crossfades what follows the cut into its start (so the seam is the song carrying on), and levels it to -18 dBFS
RMS so the two tracks sit at the same loudness. Listen to the `_preview3x.wav` it writes beside the loop for
the seam, then delete it. Do the same for the menu as `Assets/Audio/Music/music_menu.wav`.

## 3. Hook up in Unity

Run **Kecek/Juice/Assign Music**. A final `music_menu` / `music_match` under `Assets/Audio/Music` replaces the
placeholder in the Sfx library; a track picked by hand is never overwritten. Anything imported under
`Assets/Audio/Music` is set to stream, Vorbis 70%, load in background (`MusicImportSettings`), on first import only.

`placeholder_music.py` writes synth stand-ins for when a track is missing; they were deleted once the Suno
tracks arrived.

## The loops in the game (2026-11-25)

| Loop | Source | Tempo | Cut | Why there |
| --- | --- | --- | --- | --- |
| `music_menu.wav` | `Suno/Menu.wav` | 96.002 BPM | 20.102 s, 32 bars (80.0 s) | Sections A + B: starts where the bass enters, ends on B's one-bar break, so the seam is the song going round |
| `music_match.wav` | `Suno/Match.wav` | 125.000 BPM | 34.661 s, 32 bars (61.4 s) | From the bright section's entry to the same entry 32 bars later: the seam repeats the song's own dark-to-bright change |

The tempos are fine-fitted (0.002 BPM steps against the onsets): an error of 0.05 BPM over 80 s is already a
40 ms stumble at the seam. The match track's kick is four-on-the-floor, so its downbeat came from where the
section changes land, not from the kick.

## Trailer

The trailer's cuts fall on whole seconds (`Recordings/Trailer/edit-timeline.json`), so its music is **120 BPM**:
a beat is 0.5 s and a bar 2 s, which puts every cut on a beat and the even-second ones on a downbeat.

Suno does not follow a timed structure (it reads section tags as hints), so the track is generated as a song
with the right ingredients and **re-arranged by bars**:

```
python -I Art/Music/trailer_music.py --check                 # Suno/Trailer.wav -> Recordings/Trailer/music/trailer_music.wav
python -I Assets/_Capture/Montage~/trailer_montage.py        # re-renders the 60 s, 30 s and slides cuts with it
```

`trailer_music.py` holds the bar map (`ARRANGEMENT`): the hit at 0:00, near silence at 0:02, the riser into the
full beat at 0:06, the brighter part as the lift at 0:26, the light breakdown at 0:30, full again at 0:34, then
from 0:44 the track's own closing run uncut: the brighter part under "Última wave", the music thinning and
rising under the final blow (0:48), its two strongest bars landing with VICTORY (0:52), and the ending
resolving under the logo from 0:56 to the last frame. (A first cut went silent at 0:48 for a slow-motion beat;
the game has no slow motion yet, so the trailer does not pretend it does.)

Suno/Trailer.wav runs ~121.6 BPM and wanders by tens of ms within a section, so every piece is stretched
(rubberband) from its exact downbeats to exact 2 s bars. Those downbeats are **measured, in `DOWNBEATS`**:
automatic beat fitting failed both ways on this track (the hi-hats between the beats pull a full-band fit half
a beat late in bars 28-29; the bass pumping between the kicks pulls a kick-band fit off after bar 30). Bars 28-34
were read off a plot of the kick envelope. A new track needs its own `DOWNBEATS`; a bar missing there falls back
to fitting.

The montage mixes it under the game's sound (-6 dB, ducking under the game by sidechain), brings the whole to
-14 LUFS (two-pass loudnorm) and keeps the true peak under 0 dBTP after AAC. Without the score file it renders
the game's sound alone, as before.

## Mixing rules

- Music plays on the Music bus at `vol-music` (0.35) and obeys the Music and Sound settings.
- A change of track is an equal-power crossfade over `music-fade` (1.5 s).
- Cues flagged **Ducks Music** in the Sfx library (Fanfare, Defeat, Horn, Drum) dip it by `duck-music`
  (-6 dB) for as long as they sound.
