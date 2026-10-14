using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

/// <summary>
/// What every spell looks like, in one asset: its hue (the Spell Juice canvas's sp-* tokens), the effect sprites
/// Codex painted for it, and the few timings its visual reads. Spell visuals, the aim and the unit statuses all read
/// it through <see cref="Instance"/>, so retinting a spell is an edit to this asset and never to code.
/// </summary>
/// <remarks>
/// Lives in Resources like <see cref="JuiceTokensSO"/>: the visuals are built in code on every client, nothing in a
/// scene points at them. Built and refilled by <c>Kecek/Juice/Build Spell Fx</c>, which also imports the sprites.
/// Every sprite falls back to a <see cref="JuiceSprites"/> shape when missing, so a stripped asset degrades to
/// plainer effects rather than to exceptions.
/// </remarks>
[CreateAssetMenu(fileName = "SpellFx", menuName = "Scriptable Objects/Juice/Spell Fx")]
public class SpellFxSettingsSO : ScriptableObject
{
    public const string ResourcePath = "Juice/SpellFx";

    private static SpellFxSettingsSO _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _instance = null;

    /// <summary>The asset in Resources, or a default-valued one built in memory so callers never see null.</summary>
    public static SpellFxSettingsSO Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = Resources.Load<SpellFxSettingsSO>(ResourcePath);
            if (_instance == null)
            {
                _instance = CreateInstance<SpellFxSettingsSO>();
                _instance.hideFlags = HideFlags.DontSave;
            }
            return _instance;
        }
    }

    [Serializable]
    public class HueTokens
    {
        [Tooltip("sp-fire: Fireball's flame.")] public Color Fire = Hex("#ff9b3d");
        [Tooltip("sp-fire-core: white-hot.")] public Color FireCore = Hex("#fff1c4");
        [Tooltip("sp-ember: embers, specks on the scorch.")] public Color Ember = Hex("#ff6a2b");
        [Tooltip("sp-smoke: smoke and the scorch.")] public Color Smoke = Hex("#241d33");
        [Tooltip("sp-light: Lance's spear.")] public Color Light = Hex("#ffffff");
        [Tooltip("sp-light-edge: the spear's glint and the scar.")] public Color LightEdge = Hex("#ffe9a8");
        [Tooltip("ice: Ice.")] public Color Ice = Hex("#00c0ff");
        [Tooltip("ice-soft: frost, the seal.")] public Color IceSoft = Hex("#c4f1ff");
        [Tooltip("zone-haste: Haste.")] public Color Haste = Hex("#ffd94d");
        [Tooltip("haste-soft: zaps, tick lights.")] public Color HasteSoft = Hex("#fff1a6");
        [Tooltip("sp-rage: Rage. Replaces zone-rage #4cffe1, which sat beside the map's mint.")] public Color Rage = Hex("#e85cff");
        [Tooltip("sp-rage-soft: wings, feathers.")] public Color RageSoft = Hex("#f6b8ff");
        [Tooltip("sp-rift: Rift's rim.")] public Color Rift = Hex("#b9a2ff");
        [Tooltip("sp-rift-soft: echoes.")] public Color RiftSoft = Hex("#ddd1ff");
        [Tooltip("sp-rift-void: the fissure itself.")] public Color RiftVoid = Hex("#120d22");
        [Tooltip("sp-acid: Ferrugem.")] public Color Acid = Hex("#c9f25a");
        [Tooltip("sp-rust: rust flakes.")] public Color Rust = Hex("#c4622d");
        [Tooltip("tint-orange: orange armour.")] public Color ArmorOrange = Hex("#ff8242");
        [Tooltip("tint-pink: pink armour.")] public Color ArmorPink = Hex("#ff4280");
        [Tooltip("tint-purple: purple armour.")] public Color ArmorPurple = Hex("#7242ff");
        [Tooltip("The held status (Ancora): the steel of its chain and anchor.")] public Color Hold = Hex("#d6e0f0");
    }

    [Serializable]
    public class SpriteSet
    {
        public Sprite Meteor;
        public Sprite Flame;
        public Sprite Smoke;
        public Sprite Scorch;
        public Sprite IceShard;
        public Sprite IceBlock;
        public Sprite FrostNova;
        public Sprite Spear;
        public Sprite Crack;
        public Sprite HasteRune;
        public Sprite Lightning;
        public Sprite Wing;
        public Sprite Rift;
        public Sprite Corrosion;
        public Sprite ShieldBreak;
        public Sprite[] Flakes = Array.Empty<Sprite>();
        [Tooltip("The frozen badge: Ice's card art.")] public Sprite Snowflake;
        [Tooltip("The held status: a shackle of chain links closed round the enemy.")] public Sprite ChainRing;
        [Tooltip("The held status's badge.")] public Sprite Anchor;
    }

    [Serializable]
    public class AimSettings
    {
        [Tooltip("The aim disc at its rim, as alpha over the spell's hue; it thins to about a quarter of this in the middle.")] [Range(0f, 1f)] public float DiscAlpha = 0.3f;
        [Tooltip("Font for the aim's tags (KO, -200, +25%...). Empty: TMP's default font.")] public TMP_FontAsset TagFont;
        [Tooltip("Font size of a tag's words (the board's 11px of a 380px-wide phone). The pill is sized from it.")] public float TagSize = 2f;
        [Tooltip("How far above the top of a target, in world units, the middle of its tag sits.")] public float TagLift = 0.34f;
    }

    [FoldoutGroup("Hues"), HideLabel, InlineProperty] public HueTokens Hues = new();
    [FoldoutGroup("Sprites"), HideLabel, InlineProperty] public SpriteSet Sprites = new();
    [FoldoutGroup("Aim"), HideLabel, InlineProperty] public AimSettings Aim = new();

    /// <summary>The identity hue of a spell: its aim, its zone, its rings.</summary>
    public Color HueFor(SpellType spell) => spell switch
    {
        SpellType.Fireball => Hues.Fire,
        SpellType.Ice => Hues.Ice,
        SpellType.Haste => Hues.Haste,
        SpellType.Rage => Hues.Rage,
        SpellType.Rift => Hues.Rift,
        SpellType.Lance => Hues.Light,
        SpellType.Ferrugem => Hues.Acid,
        _ => Color.white
    };

    /// <summary>The secondary hue: tags, sparks, the soft half of a spell.</summary>
    public Color SoftHueFor(SpellType spell) => spell switch
    {
        SpellType.Fireball => Hues.FireCore,
        SpellType.Ice => Hues.IceSoft,
        SpellType.Haste => Hues.HasteSoft,
        SpellType.Rage => Hues.RageSoft,
        SpellType.Rift => Hues.RiftSoft,
        SpellType.Lance => Hues.LightEdge,
        SpellType.Ferrugem => Hues.Acid,
        _ => Color.white
    };

    /// <summary>An armour colour as it is painted on the board (the tint-* tokens).</summary>
    public Color ArmorTint(ArmorColor armor) => armor switch
    {
        ArmorColor.Orange => Hues.ArmorOrange,
        ArmorColor.Pink => Hues.ArmorPink,
        ArmorColor.Purple => Hues.ArmorPurple,
        _ => Color.white
    };

    /// <summary>A sprite, or the soft disc when it was never imported.</summary>
    public static Sprite Or(Sprite sprite, Sprite fallback) => sprite != null ? sprite : fallback;

    private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;
}
