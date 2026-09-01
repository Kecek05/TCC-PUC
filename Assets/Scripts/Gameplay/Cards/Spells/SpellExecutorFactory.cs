using System.Collections.Generic;

public static class SpellExecutorFactory
{
    private static readonly Dictionary<SpellType, ISpellExecutor> _executors = new()
    {
        { SpellType.Fireball, new FireballExecutor() },
        { SpellType.Ice, new IceExecutor() },
        { SpellType.Haste, new HasteExecutor() },
        { SpellType.Rage, new RageExecutor() },
        { SpellType.Rift, new RiftExecutor() },
        { SpellType.Lance, new LanceExecutor() },
        { SpellType.Ferrugem, new FerrugemExecutor() },
    };

    public static ISpellExecutor GetExecutor(SpellType spellType)
    {
        return _executors.TryGetValue(spellType, out var executor) ? executor : null;
    }
}
