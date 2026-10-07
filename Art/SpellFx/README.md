# Spell FX sprites and sounds

The effect sprites under `Assets/Sprites/Spells/FX/` were painted by Codex and the `sfx_spell*.wav` placeholders under
`Assets/Audio/Sfx/Placeholder/` were synthesized; this folder holds both recipes so either can be redone.

## Sprites

1. `prompts.tsv` holds one `name<TAB>subject` line per sprite. `run_one.sh <name> "<subject>"` asks Codex for that one
   image (white shapes on pure black) and copies it to `raw/<name>.png`. To run every line, four at a time:
   ```
   while IFS=$'\t' read -r name subject; do printf '%s\0%s\0' "$name" "$subject"; done < prompts.tsv | xargs -0 -n 2 -P 4 bash run_one.sh
   ```
   The Codex path in `run_one.sh` names an app version; find the current one with
   `(Get-AppxPackage OpenAI.Codex).InstallLocation`.
2. `python process.py` turns `raw/*.png` into `final/*.png`: white sprites whose alpha is the render's brightness,
   cropped (radial art kept centred), at most 512 px, and `fx_flakes` cut into four.
3. Copy `final/*.png` into `Assets/Sprites/Spells/FX/` and run **Kecek/Juice/Build Spell Fx (Sprites + Settings)**.
   It imports them (pixels per unit = longest side, so each sprite is one world unit long) and refills
   `Assets/Resources/Juice/SpellFx.asset`.

Codex returns RGB with no alpha, which is why the prompts ask for white on black: brightness becomes opacity.

## Sounds

`python synth.py <folder>` writes the ten `sfx_spell*.wav` cues (pure Python, no numpy). Drop them in
`Assets/Audio/Sfx/Placeholder/` and run **Kecek/Juice/Rebuild Sfx Library**. A real recording replaces one by name.
