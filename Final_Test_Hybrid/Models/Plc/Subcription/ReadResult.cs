namespace Final_Test_Hybrid.Models.Plc.Subcription;

public record ReadResult<T>(string NodeId, T? Value, string? Error)
{
    public bool Success => Error == null;
}
