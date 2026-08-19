/// <summary>
/// Storage seam for the player save: the only thing that knows <i>where</i> the save lives. A Unity Cloud
/// Save backend can replace <see cref="FilePlayerSaveRepository"/> without touching
/// <see cref="BasePlayerSaveManager"/> or the deck UI.
/// </summary>
public interface IPlayerSaveRepository
{
    /// <summary>Reads the save. Returns false (with <paramref name="data"/> null) when there is nothing
    /// readable — a missing file, an empty file or a corrupt one all mean "start fresh", never an exception.</summary>
    bool TryLoad(out PlayerSaveData data);

    void Save(PlayerSaveData data);

    void Delete();
}
