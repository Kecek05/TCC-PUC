# _Capture: trailer and gameplay video tooling

Editor/dev-only (`#if UNITY_EDITOR || DEVELOPMENT_BUILD`); nothing here ships in a release build and nothing
here changes gameplay, balance, production prefabs or scenes.

## Pieces

- **`CaptureDirector`**: runs a list of `ICaptureAction` steps and nothing else. A new kind of step is a new
  class; the director never changes.
- **Programs**: `ShotDefinition` (one trailer shot: deck, off-tape setup, filmed beats, edit cut and caption)
  and `MatchScript` (a whole take, recording switched on and off inside it). Both are ScriptableObjects
  generated from code by `TrailerShotLibrary` / `GameplayScriptLibrary` (menu `Kecek/Capture/Build ...`), so
  the whole script reads top to bottom in one file.
- **The finger**: `VirtualFinger` drives the real UI through `ExecuteEvents` (pointer down, drag, end drag,
  click): a card leaves the hand and the game's own drop check and server Rpc decide what happens. The
  `TouchIndicator` (capture-only overlay, never raycast) follows it.
- **Recorder**: `UnityRecorderCapture` (Editor) behind `ICaptureRecorder`: Movie Recorder, H.264 MP4 at 40
  Mbps, constant 60 fps (the Recorder drives `Time.captureDeltaTime`), game audio, Game View 1080x1920.
- **Montage~** (ignored by Unity): `trailer_montage.py` and `gameplay_montage.py`, ffmpeg edits.

## Staging vs real play

Trailer shots are staged around real mechanics through seams the game already has: health floors and lane
holds (nobody loses mid-shot), a raised mana ceiling (the tutorial's seam), `SpawnEnemy` crowds, the bot's own
deploy cores for rival towers and troops, an off-tape fast-forward. The player's plays always go through the
hand UI. The only simulated effect is the final blow's slow motion (`TimeScaleRamp`).

The gameplay video stages nothing: a real match against the real bot, the player's side played by scripted
beats plus `AutoPlay` (a human-paced policy) through the hand UI.

## Running

1. Back up the save (`%USERPROFILE%/AppData/LocalLow/JoaoKeceK/MinimalistDefense/player_save.json`): menu shots
   and match rewards write to it.
2. Play from `StartScene`, wait for the Main Menu.
3. Select a shot or match script and use `Kecek/Capture/Play Selected Shot Or Script`, or from the Unity CLI:
   `KeceK.Capture.EditorTools.CaptureMenu.PlayAsset("Assets/_Capture/...asset")`; poll `CaptureMenu.Status()`.
4. Raw takes land in `Recordings/` (git-ignored). Edit with `python -I Assets/_Capture/Montage~/trailer_montage.py`
   (the final cuts live in its `SHOTS` list; the finale's come from `Recordings/Trailer/finale.json`, the take's
   marks) and `python -I Assets/_Capture/Montage~/gameplay_montage.py Recordings/Gameplay/marks.json`.
   A `ShotDefinition`'s CutIn/CutSeconds are the first guess; the montage script is the edit.
