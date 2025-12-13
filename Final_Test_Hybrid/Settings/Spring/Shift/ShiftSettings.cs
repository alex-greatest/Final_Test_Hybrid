namespace Final_Test_Hybrid.Settings.Spring.Shift;

public class ShiftSettings
{
    public string Endpoint { get; } = "/api/shift/request";
    public int PollingIntervalMs { get; } = 15000;
}
