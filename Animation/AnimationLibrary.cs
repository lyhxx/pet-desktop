using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopPet.Animation;

/// <summary>
/// 动画资源库。从 Assets/Pet/animations.json 读取网格、图集路径和动画表，
/// 图集按需懒加载。资源缺失时安静降级（不抛异常），由视图回退到占位形象。
/// </summary>
public sealed class AnimationLibrary
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly Dictionary<string, AnimationClip> _clips = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SpriteSheet> _sheets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _sheetPaths = new(StringComparer.OrdinalIgnoreCase);

    private int _columns = 8;
    private int _rows = 8;
    private readonly string _baseDir;

    private AnimationLibrary(string baseDir) => _baseDir = baseDir;

    public static AnimationLibrary LoadDefault()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "Pet", "animations.json");
        return Load(path);
    }

    public static AnimationLibrary Load(string configPath)
    {
        string baseDir = Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? AppContext.BaseDirectory;
        var library = new AnimationLibrary(baseDir);

        try
        {
            if (!File.Exists(configPath)) return library;

            var data = JsonSerializer.Deserialize<AnimationsFile>(File.ReadAllText(configPath), JsonOptions);
            if (data is null) return library;

            library._columns = data.Grid?.Columns ?? 8;
            library._rows = data.Grid?.Rows ?? 8;

            foreach (var kv in data.Sheets)
                library._sheetPaths[kv.Key] = Path.Combine(baseDir, kv.Value);

            foreach (var kv in data.Animations)
            {
                var clip = kv.Value;
                library._clips[kv.Key] = new AnimationClip(
                    kv.Key, clip.Sheet, clip.Frames, clip.Fps, clip.Loop);
            }
        }
        catch
        {
            // 资源未就绪时保持空库，交给占位形象。
        }

        return library;
    }

    public bool TryGetClip(string name, out AnimationClip clip)
    {
        if (_clips.TryGetValue(name, out var found) &&
            _sheetPaths.TryGetValue(found.SheetKey, out var sheetPath) &&
            File.Exists(sheetPath))
        {
            clip = found;
            return true;
        }

        clip = null!;
        return false;
    }

    public SpriteSheet GetSheet(string key)
    {
        if (_sheets.TryGetValue(key, out var cached)) return cached;

        if (!_sheetPaths.TryGetValue(key, out var path) || !File.Exists(path))
            throw new FileNotFoundException($"找不到图集：{key} -> {path}");

        var sheet = new SpriteSheet(path, _columns, _rows);
        _sheets[key] = sheet;
        return sheet;
    }

    private sealed class AnimationsFile
    {
        [JsonPropertyName("grid")] public GridDef? Grid { get; set; }
        [JsonPropertyName("sheets")] public Dictionary<string, string> Sheets { get; set; } = new();
        [JsonPropertyName("animations")] public Dictionary<string, ClipDef> Animations { get; set; } = new();
    }

    private sealed class GridDef
    {
        [JsonPropertyName("columns")] public int Columns { get; set; } = 8;
        [JsonPropertyName("rows")] public int Rows { get; set; } = 8;
        [JsonPropertyName("cellWidth")] public int CellWidth { get; set; } = 256;
        [JsonPropertyName("cellHeight")] public int CellHeight { get; set; } = 256;
        [JsonPropertyName("anchorX")] public int AnchorX { get; set; } = 128;
        [JsonPropertyName("anchorY")] public int AnchorY { get; set; } = 236;
    }

    private sealed class ClipDef
    {
        [JsonPropertyName("sheet")] public string Sheet { get; set; } = "";
        [JsonPropertyName("frames")] public int[] Frames { get; set; } = Array.Empty<int>();
        [JsonPropertyName("fps")] public double Fps { get; set; } = 10;
        [JsonPropertyName("loop")] public bool Loop { get; set; } = true;
    }
}
