using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.SpringBoot.Operation.Finish;

namespace Final_Test_Hybrid.Services.Storage;

/// <summary>
/// Реализация ITestResultStorage для отправки результатов теста в MES.
/// Все данные берутся из runtime-сервисов, без обращения к БД.
/// </summary>
public class MesTestResultStorage(
    BoilerState boilerState,
    FinalTestResultsSnapshotBuilder snapshotBuilder,
    OperationFinishService finishService,
    DualLogger<MesTestResultStorage> logger) : ITestResultStorage
{
    /// <summary>
    /// Отправляет результаты теста в MES.
    /// </summary>
    /// <param name="testResult">Результат теста (1 = Ok, 2 = Nok).</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат сохранения.</returns>
    public async Task<SaveResult> SaveAsync(int testResult, CancellationToken ct)
    {
        if (testResult is not (1 or 2))
        {
            logger.LogError("Некорректное значение testResult={TestResult}, ожидается 1 (Ok) или 2 (Nok)", testResult);
            return SaveResult.Fail($"Некорректный результат теста: {testResult}");
        }

        if (!snapshotBuilder.TryBuild(testResult, out var request, out var errorMessage))
        {
            return SaveResult.Fail(errorMessage);
        }

        logger.LogInformation(
            "Подготовка результатов теста для MES: SerialNumber={SerialNumber}, Result={TestResult}",
            boilerState.SerialNumber,
            testResult);

        return await finishService.FinishOperationAsync(request, ct);
    }
}
