using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Services;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Diagnostic;

/// <summary>
/// Читает последнюю ошибку и журнал ошибок котла.
/// </summary>
public class DiagReadBoilerErrorsStep(
    BoilerDeviceInfoService deviceInfo,
    DualLogger<DiagReadBoilerErrorsStep> logger) : ITestStep
{
    public string Id => "diag-read-boiler-errors";
    public string Name => "DiagReadBoilerErrors";
    public string Description => "Чтение ошибок котла";

    /// <summary>
    /// Выполняет чтение последней ошибки и журнала ошибок котла.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("▶ Чтение ошибок котла");

        var lastErrorResult = await ReadLastErrorAsync(ct);
        var errorLogResult = await ReadErrorLogAsync(ct);

        logger.LogInformation("◼ Чтение ошибок завершено");

        return TestStepResult.Pass($"LastError: {lastErrorResult}, Log: {errorLogResult}");
    }

    /// <summary>
    /// Читает последнюю ошибку котла.
    /// </summary>
    private async Task<string> ReadLastErrorAsync(CancellationToken ct)
    {
        var lastError = await deviceInfo.ReadLastErrorAsync(ct);

        if (lastError.Success)
        {
            var err = lastError.Value!;
            logger.LogInformation("Последняя ошибка: [{Code}] {Description}", err.DisplayCode, err.Description);
            return $"[{err.DisplayCode}] {err.Description}";
        }

        logger.LogWarning("Не удалось прочитать последнюю ошибку: {Error}", lastError.Error);
        return $"Error: {lastError.Error}";
    }

    /// <summary>
    /// Читает журнал ошибок котла.
    /// </summary>
    private async Task<string> ReadErrorLogAsync(CancellationToken ct)
    {
        var errorLog = await deviceInfo.ReadErrorLogAsync(ct);

        if (errorLog.Success)
        {
            var errors = errorLog.Value!.Where(e => e.Id != 0).ToArray();
            logger.LogInformation("Журнал ошибок: {Count} записей", errors.Length);

            foreach (var err in errors)
            {
                logger.LogInformation("  [{Code}] {Description}", err.DisplayCode, err.Description);
            }

            return $"{errors.Length} записей";
        }

        logger.LogWarning("Не удалось прочитать журнал ошибок: {Error}", errorLog.Error);
        return $"Error: {errorLog.Error}";
    }
}
