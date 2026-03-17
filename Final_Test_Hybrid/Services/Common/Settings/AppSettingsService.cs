using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Final_Test_Hybrid.Settings.App;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Common.Settings;

public class AppSettingsService(IOptions<AppSettings> options)
{
    private readonly string _settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
    public bool UseMes { get; private set; } = options.Value.UseMes;
    public bool UseOperatorQrAuth { get; private set; } = options.Value.UseOperatorQrAuth;
    public bool UseAdminQrAuth { get; private set; } = options.Value.UseAdminQrAuth;
    public bool ExportStepsToExcel { get; private set; } = options.Value.ExportStepsToExcel;
    public bool UseInterruptReason { get; private set; } = options.Value.UseInterruptReason;
    public bool UseFloatingErrorBadge { get; } = options.Value.UseFloatingErrorBadge;
    public FloatingErrorBadgeSettings FloatingErrorBadge { get; } =
        SanitizeFloatingErrorBadgeSettings(options.Value.FloatingErrorBadge);
    public string EngineerPassword { get; } = options.Value.EngineerPassword;
    public string NameStation { get; } = options.Value.NameStation;
    public string ExportPath { get; } = options.Value.ExportPath;
    public event Action<bool>? UseMesChanged;
    public event Action<bool>? UseInterruptReasonChanged;

    public void SaveUseMes(bool value)
    {
        UseMes = value;
        SaveSettingToFile(nameof(UseMes), value);
        UseMesChanged?.Invoke(value);
    }

    public void SaveUseOperatorQrAuth(bool value)
    {
        UseOperatorQrAuth = value;
        SaveSettingToFile(nameof(UseOperatorQrAuth), value);
    }

    public void SaveUseAdminQrAuth(bool value)
    {
        UseAdminQrAuth = value;
        SaveSettingToFile(nameof(UseAdminQrAuth), value);
    }

    public void SaveExportStepsToExcel(bool value)
    {
        ExportStepsToExcel = value;
        SaveSettingToFile(nameof(ExportStepsToExcel), value);
    }

    public void SaveUseInterruptReason(bool value)
    {
        UseInterruptReason = value;
        SaveSettingToFile(nameof(UseInterruptReason), value);
        UseInterruptReasonChanged?.Invoke(value);
    }

    private void SaveSettingToFile(string settingName, bool value)
    {
        var json = File.ReadAllText(_settingsPath, Encoding.UTF8);
        var jsonNode = JsonNode.Parse(json)!;
        jsonNode["Settings"]![settingName] = value;
        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(_settingsPath, jsonNode.ToJsonString(opts), new UTF8Encoding(true));
    }

    private static FloatingErrorBadgeSettings SanitizeFloatingErrorBadgeSettings(FloatingErrorBadgeSettings? settings)
    {
        settings ??= new FloatingErrorBadgeSettings();
        return new FloatingErrorBadgeSettings
        {
            WidthPx = SanitizePixelValue(settings.WidthPx, 84),
            HeightPx = SanitizePixelValue(settings.HeightPx, 84),
            IconSizePx = SanitizePixelValue(settings.IconSizePx, 78),
            CounterMinWidthPx = SanitizePixelValue(settings.CounterMinWidthPx, 24),
            CounterHeightPx = SanitizePixelValue(settings.CounterHeightPx, 24),
            CounterFontSizePx = SanitizePixelValue(settings.CounterFontSizePx, 13)
        };
    }

    private static int SanitizePixelValue(int value, int fallback)
    {
        if (value <= 0)
        {
            return fallback;
        }

        return Math.Clamp(value, 8, 512);
    }
}
