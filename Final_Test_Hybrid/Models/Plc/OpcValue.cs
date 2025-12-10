namespace Final_Test_Hybrid.Models.Plc;

public record OpcValue(
    object? Value,
    DateTime SourceTimestamp,
    bool IsGood
);
