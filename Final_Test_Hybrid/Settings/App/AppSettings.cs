namespace Final_Test_Hybrid.Settings.App;

public class AppSettings
{
    // Биндинг IConfiguration использует setter'ы через reflection.
    // ReSharper disable once UnusedAutoPropertyAccessor
    public bool UseMes { get; set; }
    // ReSharper disable once UnusedAutoPropertyAccessor
    public bool UseOperatorQrAuth { get; set; }
    // ReSharper disable once UnusedAutoPropertyAccessor
    public bool UseAdminQrAuth { get; set; }
    // ReSharper disable once UnusedAutoPropertyAccessor
    public bool ExportStepsToExcel { get; set; }
    // ReSharper disable once UnusedAutoPropertyAccessor
    public bool UseInterruptReason { get; set; }
    // ReSharper disable once UnusedAutoPropertyAccessor
    public bool UseFloatingErrorBadge { get; set; }
    public string EngineerPassword { get; set; } = string.Empty;
    public string NameStation { get; set; } = string.Empty;
    public string ExportPath { get; set; } = string.Empty;
    public FloatingErrorBadgeSettings FloatingErrorBadge { get; set; } = new();
}
