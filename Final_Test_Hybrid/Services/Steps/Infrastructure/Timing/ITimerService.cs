namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;

public interface ITimerService
{
    event Action? OnChanged;
    void Start(string key);
    TimeSpan? Stop(string key);
    TimeSpan? GetElapsed(string key);
    bool IsRunning(string key);
    IReadOnlyDictionary<string, TimeSpan> GetAllActive();
    void StopAll();
    void Clear();
}
