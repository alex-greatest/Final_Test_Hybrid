using System.Text.Json;
using System.Text.Json.Nodes;
using Final_Test_Hybrid.Settings.App;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Common.Settings;

public class AppSettingsService(IOptions<AppSettings> options)
{
    private readonly string _settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
    public bool UseMes { get; private set; } = options.Value.UseMes;
    public string EngineerPassword { get; } = options.Value.EngineerPassword;
    public string NameStation { get; } = options.Value.NameStation;
    public event Action<bool>? UseMesChanged;

    public void SaveUseMes(bool value)
    {
        UseMes = value;
        SaveToFile();
        UseMesChanged?.Invoke(value);
    }

    private void SaveToFile()
    {
        var json = File.ReadAllText(_settingsPath);
        var jsonNode = JsonNode.Parse(json)!;
        jsonNode["Settings"]![nameof(UseMes)] = UseMes;
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(_settingsPath, jsonNode.ToJsonString(options));
    }
}
