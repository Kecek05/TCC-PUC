using System.Collections.Generic;

public static class SpellExecutorFactory
{
    private static readonly Dictionary<SpellType, ISpellExecutor> _executors = new()
    {
        { SpellType.Fireball, new FireballExecutor() },
    };

    public static ISpellExecutor GetExecutor(SpellType spellType)
    {
        return _executors.TryGetValue(spellType, out var executor) ? executor : null;
    }
}
