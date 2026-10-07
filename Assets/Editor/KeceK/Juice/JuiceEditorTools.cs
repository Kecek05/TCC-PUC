using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kecek.Editor
{
    /// <summary>
    /// Editor side of the Juice &amp; Feedback System: keeps <see cref="JuiceTokensSO"/> in step with the design
    /// system's tokens.json, and builds <see cref="SfxLibrarySO"/> from the clips on disk.
    /// </summary>
    public static class JuiceEditorTools
    {
        private const string TokensAssetPath = "Assets/Resources/Juice/JuiceTokens.asset";
        private const string LibraryAssetPath = "Assets/Resources/Juice/SfxLibrary.asset";
        private const string SfxFolder = "Assets/Audio/Sfx";

        // ---------- tokens ----------

        [Serializable] private class TokenFile
        {
            public Family color, duration, easing, scale, distance, angle, shake, haptic, audio, opacity, budget;
        }

        [Serializable] private class Family { public Token[] tokens; }
        [Serializable] private class Token { public string name; public string value; }

        /// <summary>
        /// Copies every token the game reads from the design system's tokens.json (the TextAsset on the tokens
        /// asset) into <see cref="JuiceTokensSO"/>. Easing curves are not imported: the design system writes them
        /// as CSS curves, and each one is already named for the DOTween ease the asset holds.
        /// </summary>
        [MenuItem("Kecek/Juice/Import Tokens From Design System")]
        public static void ImportTokens()
        {
            JuiceTokensSO tokens = AssetDatabase.LoadAssetAtPath<JuiceTokensSO>(TokensAssetPath);
            if (tokens == null) { Debug.LogError("[Juice] No tokens asset at " + TokensAssetPath); return; }
            if (tokens.TokensJson == null) { Debug.LogError("[Juice] The tokens asset has no TokensJson to import from."); return; }

            TokenFile file = JsonUtility.FromJson<TokenFile>(tokens.TokensJson.text);
            Dictionary<string, string> values = new();
            foreach (Family family in new[] { file.color, file.duration, file.easing, file.scale, file.distance, file.angle,
                         file.shake, file.haptic, file.audio, file.opacity, file.budget })
            {
                if (family?.tokens == null) continue;
                foreach (Token t in family.tokens)
                    if (!string.IsNullOrEmpty(t?.name)) values[t.name] = t.value;
            }

            Undo.RecordObject(tokens, "Import Juice tokens");
            int applied = 0;
            List<string> missing = new();

            void Seconds(string n, Action<float> set) { if (TryNumber(values, n, out float v, out string unit)) { set(unit == "ms" ? v / 1000f : v); applied++; } else missing.Add(n); }
            void Number(string n, Action<float> set) { if (TryNumber(values, n, out float v, out _)) { set(v); applied++; } else missing.Add(n); }
            void Percent(string n, Action<float> set) { if (TryNumber(values, n, out float v, out string unit)) { set(unit == "%" ? v / 100f : v); applied++; } else missing.Add(n); }
            void Whole(string n, Action<int> set) { if (TryNumber(values, n, out float v, out _)) { set(Mathf.RoundToInt(v)); applied++; } else missing.Add(n); }
            void Preset(string n, Action<Lofelt.NiceVibrations.HapticPatterns.PresetType> set)
            {
                if (values.TryGetValue(n, out string s) && Enum.TryParse(s, out Lofelt.NiceVibrations.HapticPatterns.PresetType p)) { set(p); applied++; }
                else missing.Add(n);
            }
            void Colour(string n, Action<Color> set)
            {
                if (TryColour(values, n, 0, out Color c)) { set(c); applied++; } else missing.Add(n);
            }

            JuiceTokensSO.DurationTokens d = tokens.Durations;
            Seconds("dur-press", v => d.Press = v); Seconds("dur-select", v => d.Select = v); Seconds("dur-release", v => d.Release = v);
            Seconds("dur-flash", v => d.Flash = v); Seconds("dur-hitstop", v => d.HitStop = v); Seconds("dur-fall", v => d.Fall = v);
            Seconds("dur-punch", v => d.Punch = v); Seconds("dur-pop", v => d.Pop = v); Seconds("dur-settle", v => d.Settle = v);
            Seconds("dur-deny", v => d.Deny = v); Seconds("dur-panel", v => d.Panel = v); Seconds("dur-travel", v => d.Travel = v);
            Seconds("dur-count", v => d.Count = v); Seconds("dur-spawn", v => d.Spawn = v); Seconds("dur-levelup", v => d.LevelUp = v);
            Seconds("dur-celebrate", v => d.Celebrate = v); Seconds("dur-toast", v => d.Toast = v); Seconds("dur-banner", v => d.Banner = v);
            Seconds("dur-shake", v => d.Shake = v); Seconds("dur-shake-long", v => d.ShakeLong = v);
            Seconds("stagger", v => d.Stagger = v); Seconds("stagger-reveal", v => d.StaggerReveal = v);

            JuiceTokensSO.ScaleTokens sc = tokens.Scales;
            Number("scale-press", v => sc.Press = v); Number("scale-select", v => sc.Select = v); Number("scale-drag", v => sc.Drag = v);
            Number("scale-nav", v => sc.Nav = v); Number("scale-panel-from", v => sc.PanelFrom = v); Number("scale-pop-peak", v => sc.PopPeak = v);
            Number("scale-commit", v => sc.Commit = v); Number("punch-soft", v => sc.PunchSoft = v); Number("punch-hard", v => sc.PunchHard = v);
            Number("hit-squash", v => sc.HitSquash = v); Number("hit-rebound", v => sc.HitRebound = v);
            Number("land-x", v => sc.LandX = v); Number("land-y", v => sc.LandY = v); Number("breathe", v => sc.Breathe = v);

            JuiceTokensSO.DistanceTokens di = tokens.Distances;
            Number("lift-card", v => di.LiftCard = v); Number("shake-card", v => di.ShakeCard = v); Number("shake-bar", v => di.ShakeBar = v);
            Number("rise-banner", v => di.RiseBanner = v); Number("rise-toast", v => di.RiseToast = v);
            Number("tilt-drag", v => di.TiltDrag = v); Number("tilt-deny", v => di.TiltDeny = v);

            JuiceTokensSO.ShakeTokens sh = tokens.Shake;
            Percent("shake-micro", v => sh.Micro = v); Percent("shake-small", v => sh.Small = v); Percent("shake-medium", v => sh.Medium = v);
            Percent("shake-large", v => sh.Large = v); Percent("zoom-kick", v => sh.ZoomKick = v); Number("shake-freq", v => sh.Frequency = v);

            JuiceTokensSO.HapticTokens h = tokens.Haptics;
            Preset("haptic-select", v => h.Select = v); Preset("haptic-light", v => h.Light = v); Preset("haptic-medium", v => h.Medium = v);
            Preset("haptic-heavy", v => h.Heavy = v); Preset("haptic-success", v => h.Success = v); Preset("haptic-warning", v => h.Warning = v);
            Preset("haptic-failure", v => h.Failure = v);

            JuiceTokensSO.AudioTokens a = tokens.Audio;
            Number("vol-ui", v => a.VolumeUi = v); Number("vol-game", v => a.VolumeGame = v); Number("vol-impact", v => a.VolumeImpact = v);
            Number("vol-music", v => a.VolumeMusic = v); Number("duck-music", v => a.DuckMusicDb = v); Percent("pitch-jitter", v => a.PitchJitter = v);
            Number("pitch-step", v => a.PitchStep = v); Seconds("sfx-cooldown", v => a.Cooldown = v);

            JuiceTokensSO.OpacityTokens o = tokens.Opacity;
            Number("disabled", v => o.Disabled = v); Number("flash-peak", v => o.FlashPeak = v); Number("ghost-blocked", v => o.GhostBlocked = v);

            JuiceTokensSO.BudgetTokens b = tokens.Budgets;
            Whole("budget-shake", v => b.ShakesPerSecond = v); Whole("budget-flash", v => b.FlashesPerSecond = v);
            Whole("budget-numbers", v => b.Numbers = v); Whole("budget-voices", v => b.Voices = v); Seconds("budget-haptic", v => b.HapticGap = v);

            JuiceTokensSO.ColorTokens c = tokens.Colors;
            Colour("ink", v => c.Ink = v); Colour("ink-muted", v => c.InkMuted = v); Colour("outline", v => c.Outline = v);
            Colour("sheet", v => c.Sheet = v); Colour("scrim", v => c.Scrim = v); Colour("deny", v => c.Deny = v);
            Colour("card-flash", v => c.CardFlash = v); Colour("hit-flash", v => c.HitFlash = v); Colour("dmg", v => c.Damage = v);
            Colour("dmg-heavy", v => c.DamageHeavy = v); Colour("dmg-resist", v => c.DamageResist = v); Colour("gain", v => c.Gain = v);
            Colour("mana", v => c.Mana = v); Colour("mana-gain", v => c.ManaGain = v); Colour("mana-short", v => c.ManaShort = v);
            Colour("attention", v => c.Attention = v); Colour("place-ok", v => c.PlaceOk = v); Colour("ghost", v => c.Ghost = v);
            Colour("range", v => c.Range = v); Colour("range-next", v => c.RangeNext = v); Colour("sparkle", v => c.Sparkle = v);
            Colour("dust", v => c.Dust = v); Colour("blast", v => c.Blast = v); Colour("ice", v => c.Ice = v);
            Colour("hasted", v => c.Hasted = v); Colour("gold", v => c.Gold = v); Colour("hp-you", v => c.HpYou = v);
            Colour("hp-foe", v => c.HpFoe = v); Colour("name-you", v => c.NameYou = v); Colour("name-foe", v => c.NameFoe = v);
            Colour("cta", v => c.Cta = v); Colour("upgrade", v => c.Upgrade = v);
            Colour("panel", v => c.Panel = v); Colour("stat", v => c.Stat = v); Colour("tile", v => c.Tile = v);
            Colour("nav", v => c.Nav = v); Colour("button", v => c.ButtonFace = v); Colour("rarity-legendary", v => c.RarityLegendary = v);
            Colour("zone-haste", v => c.ZoneHaste = v); Colour("zone-rage", v => c.ZoneRage = v);

            EditorUtility.SetDirty(tokens);
            AssetDatabase.SaveAssetIfDirty(tokens);
            Debug.Log($"[Juice] Imported {applied} tokens from {tokens.TokensJson.name}." +
                      (missing.Count > 0 ? " Missing: " + string.Join(", ", missing) : ""));
        }

        private static bool TryNumber(Dictionary<string, string> values, string name, out float value, out string unit)
        {
            value = 0f;
            unit = string.Empty;
            if (!values.TryGetValue(name, out string raw) || string.IsNullOrEmpty(raw)) return false;

            string s = raw.Trim();
            int end = 0;
            while (end < s.Length && (char.IsDigit(s[end]) || s[end] == '.' || s[end] == '-' || s[end] == '+')) end++;
            if (end == 0) return false;

            unit = s.Substring(end).Trim().ToLowerInvariant();
            return float.TryParse(s.Substring(0, end), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        // Follows "{alias}" values to the colour they name.
        private static bool TryColour(Dictionary<string, string> values, string name, int depth, out Color colour)
        {
            colour = default;
            if (depth > 8 || !values.TryGetValue(name, out string raw) || string.IsNullOrEmpty(raw)) return false;

            raw = raw.Trim();
            if (raw.StartsWith("{") && raw.EndsWith("}")) return TryColour(values, raw.Substring(1, raw.Length - 2), depth + 1, out colour);

            colour = JuiceTokensSO.Hex(raw);
            return colour != Color.magenta;
        }

        /// <summary>Appends the default copy for every <see cref="RefusalReason"/> the asset does not have yet
        /// (reasons are append-only, so new ones arrive after the asset was created).</summary>
        [MenuItem("Kecek/Juice/Add Missing Refusal Copy")]
        public static void AddMissingRefusalCopy()
        {
            RefusalCopySO copy = AssetDatabase.LoadAssetAtPath<RefusalCopySO>("Assets/Resources/Juice/RefusalCopy.asset");
            if (copy == null) { Debug.LogError("[Juice] No refusal copy asset."); return; }

            Undo.RecordObject(copy, "Add missing refusal copy");
            int added = 0;
            foreach (RefusalCopyEntry entry in RefusalCopySO.DefaultEntries())
            {
                if (copy.Entries.Any(e => e.Reason == entry.Reason)) continue;
                copy.Entries.Add(entry);
                added++;
            }

            EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssetIfDirty(copy);
            Debug.Log($"[Juice] Refusal copy: added {added} entries.");
        }

        // ---------- sounds ----------

        /// <summary>
        /// Fills <see cref="SfxLibrarySO"/> from the clips under Assets/Audio/Sfx named <c>sfx_&lt;cue&gt;</c>
        /// (<c>sfx_&lt;cue&gt;_2</c> for variants). A cue that already has clips is left alone, so a final clip
        /// assigned by hand is never overwritten by a placeholder; every cue gets its bus by default.
        /// </summary>
        [MenuItem("Kecek/Juice/Rebuild Sfx Library")]
        public static void RebuildSfxLibrary()
        {
            SfxLibrarySO library = AssetDatabase.LoadAssetAtPath<SfxLibrarySO>(LibraryAssetPath);
            if (library == null) { Debug.LogError("[Juice] No Sfx library at " + LibraryAssetPath); return; }

            Dictionary<SfxId, List<AudioClip>> found = new();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { SfxFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!file.StartsWith("sfx_", StringComparison.OrdinalIgnoreCase)) continue;

                string cueName = file.Substring(4);
                int variant = cueName.LastIndexOf('_');
                if (variant > 0 && int.TryParse(cueName.Substring(variant + 1), out _)) cueName = cueName.Substring(0, variant);
                if (!Enum.TryParse(cueName, true, out SfxId id) || id == SfxId.None) continue;

                if (!found.TryGetValue(id, out List<AudioClip> clips)) found[id] = clips = new List<AudioClip>();
                clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
            }

            Undo.RecordObject(library, "Rebuild Sfx library");
            int filled = 0;
            foreach (SfxId id in Enum.GetValues(typeof(SfxId)).Cast<SfxId>().Where(i => i != SfxId.None))
            {
                SfxCue cue = library.Cues.FirstOrDefault(c => c.Id == id);
                if (cue == null)
                {
                    cue = new SfxCue { Id = id, Bus = DefaultBus(id), Volume = DefaultVolume(id) };
                    library.Cues.Add(cue);
                }

                if ((cue.Clips == null || cue.Clips.Length == 0 || cue.Clips.All(c => c == null)) && found.TryGetValue(id, out List<AudioClip> clips))
                {
                    cue.Clips = clips.OrderBy(c => c.name).ToArray();
                    filled++;
                }
            }

            library.Cues = library.Cues.OrderBy(c => (int)c.Id).ToList();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssetIfDirty(library);
            Debug.Log($"[Juice] Sfx library: {library.Cues.Count} cues, {filled} filled from {SfxFolder}.");
        }

        // The placeholders all peak at the same level; the frequent ones are trimmed so a busy wave never drowns the rest.
        private static float DefaultVolume(SfxId id) => id switch
        {
            SfxId.Hit => 0.45f,
            SfxId.Coin or SfxId.Mana or SfxId.Clink or SfxId.Death => 0.6f,
            SfxId.Tick or SfxId.Spawn => 0.7f,
            SfxId.Pick or SfxId.Flip => 0.8f,
            _ => 1f
        };

        private static SfxBus DefaultBus(SfxId id) => id switch
        {
            SfxId.Tick or SfxId.Press or SfxId.Tap or SfxId.Confirm or SfxId.Deny or SfxId.Flip or SfxId.Pick => SfxBus.Ui,
            SfxId.Impact or SfxId.Boom or SfxId.BaseHit or SfxId.Upgrade or SfxId.Fanfare or SfxId.Defeat
                or SfxId.LevelUp or SfxId.Stamp => SfxBus.Impact,
            _ => SfxBus.Game
        };
    }
}
