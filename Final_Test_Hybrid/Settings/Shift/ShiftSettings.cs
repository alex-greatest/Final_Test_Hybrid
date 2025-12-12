namespace Final_Test_Hybrid.Settings.Shift;

public class ShiftSettings
{
    public string Endpoint { get; set; } = "/api/operation/last/results";
    public string NameStation { get; set; } = "FT 68";
    public int PollingIntervalMs { get; set; } = 15000;
}
