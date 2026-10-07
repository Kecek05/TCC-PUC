using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Kecek.Editor
{
    /// <summary>
    /// Builds the Spell Juice data: imports the effect sprites Codex painted (white, alpha from brightness) so each
    /// one's longest side is exactly one world unit, then creates or refills <see cref="SpellFxSettingsSO"/> in
    /// Resources with them, the Ice card's snowflake for the frozen badge, and the game's Fredoka font for the aim's
    /// tags. Re-runnable: drop a new <c>fx_*.png</c> in the folder and run it again.
    /// </summary>
    public static class SpellFxEditorTools
    {
        private const string SpriteFolder = "Assets/Sprites/Spells/FX";
        private const string AssetPath = "Assets/Resources/Juice/SpellFx.asset";
        private const string SnowflakePath = "Assets/Sprites/UI/Cards/snowflakes.png";

        [MenuItem("Kecek/Juice/Build Spell Fx (Sprites + Settings)")]
        public static void Build()
        {
            int imported = ImportSprites();

            SpellFxSettingsSO settings = AssetDatabase.LoadAssetAtPath<SpellFxSettingsSO>(AssetPath);
            if (settings == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath)!);
                settings = ScriptableObject.CreateInstance<SpellFxSettingsSO>();
                AssetDatabase.CreateAsset(settings, AssetPath);
            }

            SpellFxSettingsSO.SpriteSet s = settings.Sprites;
            s.Meteor = Load("fx_meteor");
            s.Flame = Load("fx_flame");
            s.Smoke = Load("fx_smoke");
            s.Scorch = Load("fx_scorch");
            s.IceShard = Load("fx_ice_shard");
            s.IceBlock = Load("fx_ice_block");
            s.FrostNova = Load("fx_frost_nova");
            s.Spear = Load("fx_spear");
            s.Crack = Load("fx_crack");
            s.HasteRune = Load("fx_haste_rune");
            s.Lightning = Load("fx_lightning");
            s.Wing = Load("fx_wing");
            s.Rift = Load("fx_rift");
            s.Corrosion = Load("fx_corrosion");
            s.ShieldBreak = Load("fx_shield_break");
            s.Flakes = Enumerable.Range(0, 4).Select(i => Load($"fx_flake_{i}")).Where(sprite => sprite != null).ToArray();
            s.Snowflake = AssetDatabase.LoadAssetAtPath<Sprite>(SnowflakePath);

            if (settings.Aim.TagFont == null)
            {
                settings.Aim.TagFont = AssetDatabase.FindAssets("t:TMP_FontAsset Fredoka")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>)
                    .Where(font => font != null)
                    .OrderByDescending(font => font.name.Contains("Bold"))
                    .FirstOrDefault();
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);

            int missing = new[] { s.Meteor, s.Flame, s.Smoke, s.Scorch, s.IceShard, s.IceBlock, s.FrostNova, s.Spear, s.Crack,
                s.HasteRune, s.Lightning, s.Wing, s.Rift, s.Corrosion, s.ShieldBreak }.Count(sprite => sprite == null);
            Debug.Log($"[Juice] Spell Fx: {imported} sprites imported from {SpriteFolder}, {missing} missing, " +
                      $"{s.Flakes.Length} flakes, font {(settings.Aim.TagFont != null ? settings.Aim.TagFont.name : "TMP default")}.");
        }

        /// <summary>
        /// Single sprites, alpha as transparency, no mipmaps, and pixels-per-unit set to the longest side so a sprite is
        /// one world unit long: a visual sizes an effect by scale alone, whatever the sprite's resolution.
        /// </summary>
        private static int ImportSprites()
        {
            if (!AssetDatabase.IsValidFolder(SpriteFolder)) return 0;

            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;

                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                float ppu = Mathf.Max(1, Mathf.Max(width, height));

                bool changed = importer.textureType != TextureImporterType.Sprite
                               || importer.spriteImportMode != SpriteImportMode.Single
                               || !Mathf.Approximately(importer.spritePixelsPerUnit, ppu)
                               || !importer.alphaIsTransparency
                               || importer.mipmapEnabled;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = ppu;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;

                if (changed) importer.SaveAndReimport();
                count++;
            }

            return count;
        }

        private static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/{name}.png");
    }
}
