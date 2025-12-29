namespace Final_Test_Hybrid.Models;

public class TestSequenseData
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Module { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Range { get; set; } = string.Empty;
    public bool IsError { get; set; }
    public bool IsSuccess { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}
