namespace Final_Test_Hybrid.Models.Steps;

public enum TestStepStatus
{
    Running,
    Success,
    Error
}

public class TestSequenseData
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Module { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Range { get; set; } = string.Empty;
    public TestStepStatus StepStatus { get; set; } = TestStepStatus.Running;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}
