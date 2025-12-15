namespace Final_Test_Hybrid.Settings.Database;

public class DatabaseSettings
{
    public string ConnectionString { get; init; } = string.Empty;
    public int HealthCheckIntervalMs { get; init; } = 10000;
    public int ConnectionTimeoutSeconds { get; init; } = 5;
}
