namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;

public interface IPreExecutionStep
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    bool IsVisibleInStatusGrid { get; }

    /// <summary>
    /// Можно ли пропустить этот шаг при ошибке.
    /// Если false — Skip сигнал полностью игнорируется.
    /// </summary>
    bool IsSkippable { get; }

    Task<PreExecutionResult> ExecuteAsync(PreExecutionContext context, CancellationToken ct);
}
