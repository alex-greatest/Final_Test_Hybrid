namespace Final_Test_Hybrid.Models.Errors;

public record ErrorDefinition(
    string Code,
    string Description,
    string? PlcTag = null,
    ErrorSeverity Severity = ErrorSeverity.Warning,
    bool ActivatesResetButton = false,
    string[]? PossibleStepIds = null,
    string? RelatedStepId = null,
    string? RelatedStepName = null)
{
    public bool IsPlcBound => PlcTag is not null;
    public bool IsGlobal => PossibleStepIds is null && RelatedStepId is null;
}

public enum ErrorSeverity { Info, Warning, Critical }

public enum ErrorSource { Application, Plc }
