namespace Final_Test_Hybrid.Settings.App;

public class AppSettings
{
    public bool UseMes { get; set; }
    public bool UseOperatorQrAuth { get; set; }
    public bool UseAdminQrAuth { get; set; }
    public string EngineerPassword { get; set; } = string.Empty;
    public string NameStation { get; set; } = string.Empty;
}
