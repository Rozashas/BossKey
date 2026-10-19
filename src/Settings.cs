using System.Text.Json;

namespace BossKey;

internal sealed class Hotkey
{
    public int Key { get; set; } = (int)Keys.Oemtilde;
    public List<int> Modifiers { get; set; } = [0xA4];

    internal bool IsValid() => Key >= 8 && Key <= 254 && !HotkeyManager.ModifierKeys.Contains(Key)
        && Modifiers.Count > 0 && Modifiers.Distinct().Count() == Modifiers.Count
        && Modifiers.All(HotkeyManager.ModifierKeys.Contains);

    public override string ToString()
    {
        Dictionary<int, string> names = new()
        {
            [0xA0] = "좌 Shift", [0xA1] = "우 Shift", [0xA2] = "좌 Ctrl", [0xA3] = "우 Ctrl",
            [0xA4] = "좌 Alt", [0xA5] = "우 Alt", [0x5B] = "좌 Win", [0x5C] = "우 Win"
        };
        string keyName = Key == (int)Keys.Oemtilde ? "`" : ((Keys)Key).ToString();
        return string.Join(" + ", Modifiers.Select(key => names.GetValueOrDefault(key, "?" )).Append(keyName));
    }
}

internal sealed class Settings
{
    public Hotkey Hotkey { get; set; } = new();
    public List<string> Programs { get; set; } = [];
    internal static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BossKey");
    internal static string FilePath => Path.Combine(DirectoryPath, "settings.json");

    internal static Settings Load(out string warning)
    {
        warning = "";
        if (!File.Exists(FilePath)) return new();
        try
        {
            Settings result = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? throw new InvalidDataException();
            if (result.Hotkey == null || result.Hotkey.Modifiers == null || !result.Hotkey.IsValid() || result.Programs == null)
                throw new InvalidDataException();
            result.Programs = result.Programs.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return result;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            warning = "설정을 읽을 수 없어 기본값 적용. 기존 설정 파일은 다음 저장 전까지 유지.";
            return new();
        }
    }

    internal void Save()
    {
        Directory.CreateDirectory(DirectoryPath);
        string temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, FilePath, true);
    }
}
