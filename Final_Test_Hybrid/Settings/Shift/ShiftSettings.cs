namespace Final_Test_Hybrid.Settings.Shift;

public class ShiftSettings
{
    public string Endpoint { get; set; } = "/api/shift/request";
    public int PollingIntervalMs { get; set; } = 15000;
}
