namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;

internal class TimingState
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string? Name { get; private set; }
    public string? Description { get; private set; }
    public DateTime StartTime { get; private set; }
    public bool IsRunning { get; private set; }
    public TimeSpan AccumulatedDuration { get; private set; }

    public bool IsActive => Name != null;

    public TimeSpan CalculateDuration()
    {
        return IsRunning
            ? AccumulatedDuration + (DateTime.Now - StartTime)
            : AccumulatedDuration;
    }

    public void Start(string name, string description)
    {
        Id = Guid.NewGuid();
        Name = name;
        Description = description;
        StartTime = DateTime.Now;
        IsRunning = true;
        AccumulatedDuration = TimeSpan.Zero;
    }

    public void Pause()
    {
        if (!IsRunning)
        {
            return;
        }
        AccumulatedDuration += DateTime.Now - StartTime;
        IsRunning = false;
    }

    public void Resume()
    {
        if (IsRunning || !IsActive)
        {
            return;
        }
        StartTime = DateTime.Now;
        IsRunning = true;
    }

    public void Stop()
    {
        IsRunning = false;
    }

    public void Reset()
    {
        StartTime = DateTime.Now;
        IsRunning = true;
        AccumulatedDuration = TimeSpan.Zero;
    }

    public void Clear()
    {
        Name = null;
        Description = null;
        IsRunning = false;
        AccumulatedDuration = TimeSpan.Zero;
    }
}
