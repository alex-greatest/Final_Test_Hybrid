using Final_Test_Hybrid.Models.Errors;

namespace Final_Test_Hybrid.Services.Errors;

public interface IErrorService
{
    event Action? OnActiveErrorsChanged;
    event Action? OnHistoryChanged;
    IReadOnlyList<ActiveError> GetActiveErrors();
    IReadOnlyList<ErrorHistoryItem> GetHistory();
    void Raise(ErrorDefinition error, string? details = null);
    void RaiseInStep(ErrorDefinition error, string stepId, string stepName, string? details = null);
    void Clear(string errorCode);
    void ClearActiveApplicationErrors();
    void RaisePlc(ErrorDefinition error, string? stepId = null, string? stepName = null);
    void ClearPlc(string errorCode);
    void ClearAllActiveErrors();
    void ClearHistory();
    bool HasResettableErrors { get; }
    bool HasActiveErrors { get; }
    bool IsHistoryEnabled { get; set; }
}
