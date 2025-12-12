namespace Final_Test_Hybrid.Services.SpringBoot.Settings;

public class SpringBootSettings
{
    public string BaseUrl { get; set; } = "http://localhost:8080";
    public int HealthCheckIntervalMs { get; set; } = 15000;
    public int TimeoutMs { get; set; } = 5000;
}
