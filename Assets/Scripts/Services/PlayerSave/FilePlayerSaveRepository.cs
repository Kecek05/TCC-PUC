using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Stores the player save as JSON under <see cref="Application.persistentDataPath"/>.
/// Writes are write-then-swap, so a crash mid-write can only lose the scratch file, never the live save.
/// </summary>
public class FilePlayerSaveRepository : IPlayerSaveRepository
{
    private readonly string _filePath;
    private readonly string _tempPath;

    /// <summary>Absolute path of the save file, for logging and the editor debug tools.</summary>
    public string FilePath => _filePath;

    public FilePlayerSaveRepository(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) fileName = "player_save.json";

        _filePath = Path.Combine(Application.persistentDataPath, fileName);
        _tempPath = _filePath + ".tmp";
    }

    public bool TryLoad(out PlayerSaveData data)
    {
        data = null;

        try
        {
            if (!File.Exists(_filePath)) return false;

            string json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json)) return false;

            data = JsonUtility.FromJson<PlayerSaveData>(json);
        }
        catch (Exception e)
        {
            // A corrupt save must degrade to a fresh default, never throw into ClientManager.Awake.
            GameLog.Exception(e);
            data = null;
        }

        return data != null;
    }

    public void Save(PlayerSaveData data)
    {
        if (data == null) return;

        try
        {
            File.WriteAllText(_tempPath, JsonUtility.ToJson(data, prettyPrint: true));

            if (File.Exists(_filePath)) File.Delete(_filePath);
            File.Move(_tempPath, _filePath);
        }
        catch (Exception e)
        {
            GameLog.Exception(e);
        }
    }

    public void Delete()
    {
        try
        {
            if (File.Exists(_filePath)) File.Delete(_filePath);
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
        }
        catch (Exception e)
        {
            GameLog.Exception(e);
        }
    }
}
