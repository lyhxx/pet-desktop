using System.IO;
using System.Text.Json;

namespace DesktopPet.Save;

/// <summary>本地 JSON 存档的读写。存档位于 %AppData%\DesktopPet\save.json。</summary>
public sealed class SaveManager
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _directory;
    private readonly string _filePath;

    public SaveManager()
    {
        _directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopPet");
        _filePath = Path.Combine(_directory, "save.json");
    }

    public string FilePath => _filePath;

    public PetSaveData Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new PetSaveData { LastSaved = DateTimeOffset.Now };

            var data = JsonSerializer.Deserialize<PetSaveData>(File.ReadAllText(_filePath), Options);
            if (data is null)
                return new PetSaveData { LastSaved = DateTimeOffset.Now };

            data.ApplyOffline(DateTimeOffset.Now);
            return data;
        }
        catch
        {
            return new PetSaveData { LastSaved = DateTimeOffset.Now };
        }
    }

    public void Save(PetSaveData data)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(data, Options));
        }
        catch
        {
            // 存档失败不影响运行。
        }
    }
}
