namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;

/// <summary>
/// Форматирование длительности для runtime-контрактов.
/// </summary>
public static class DurationFormatter
{
    /// <summary>
    /// Форматирует длительность в канонический формат HH:mm:ss.
    /// Часы не ограничены сверху.
    /// </summary>
    public static string ToHoursMinutesSeconds(TimeSpan duration)
    {
        var totalHours = (long)duration.TotalHours;
        return $"{totalHours:D2}:{duration.Minutes:D2}:{duration.Seconds:D2}";
    }
}
