namespace Final_Test_Hybrid.Services.Errors;

public sealed class ErrorScope(IErrorService errorService)
{
    private readonly List<string> _raisedCodes = [];

    public void Raise(List<Models.Errors.ErrorDefinition>? errors, string stepId, string stepName)
    {
        if (errors is not { Count: > 0 })
        {
            return;
        }

        foreach (var error in errors)
        {
            errorService.RaiseInStep(error, stepId, stepName);
            _raisedCodes.Add(error.Code);
        }
    }

    public void Clear()
    {
        foreach (var code in _raisedCodes)
        {
            errorService.Clear(code);
        }

        _raisedCodes.Clear();
    }
}
