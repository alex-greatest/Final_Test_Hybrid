namespace Final_Test_Hybrid.Services.Steps.Infrastructure;

internal static class TemperatureRiseResultMessageFormatter
{
    public static string Format(float temperature, float delta)
    {
        return $"Температура: {temperature:F3}, Разница: {delta:F3}";
    }

    public static string FormatWithFlow(float temperature, float delta, float flow)
    {
        return $"Температура: {temperature:F3}, Разница: {delta:F3}, Расход: {flow:F3}";
    }
}
