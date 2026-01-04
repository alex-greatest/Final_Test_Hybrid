namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.PreExecution;

public interface IPreExecutionStep
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    bool IsVisibleInStatusGrid { get; }

    Task<PreExecutionResult> ExecuteAsync(PreExecutionContext context, CancellationToken ct);
}
