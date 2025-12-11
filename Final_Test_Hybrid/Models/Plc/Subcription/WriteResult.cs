namespace Final_Test_Hybrid.Models.Plc.Subcription;

public record WriteResult(string NodeId, string? Error)
{
    public bool Success => Error == null;
}
