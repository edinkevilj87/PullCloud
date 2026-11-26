using System.Text.Json;

namespace PullCloud;

public sealed class AppSettings
{
    public string MyLocationName { get; set; } = "Sarajevo, Bosnia and Herzegovina";
    public string MyTzId { get; set; } = "Europe/Sarajevo";
    /// <summary>If the time left until pull is at most this many minutes -> WAIT, otherwise NOT YET.</summary>
    public int WaitThresholdMinutes { get; set; } = 60;
    public string? LastState { get; set; }

    private static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PullCloud");
    private static string FilePath => Path.Combine(Dir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
