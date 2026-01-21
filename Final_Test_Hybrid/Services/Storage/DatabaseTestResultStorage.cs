using System.Data.Common;
using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Database;
using Final_Test_Hybrid.Services.Database.Config;
using Final_Test_Hybrid.Services.Storage.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Final_Test_Hybrid.Services.Storage;

/// <summary>
/// Координатор сохранения результатов теста в базу данных PostgreSQL.
/// Объединяет работу всех storage-сервисов в единую транзакцию.
/// </summary>
public class DatabaseTestResultStorage(
    IDbContextFactory<AppDbContext> contextFactory,
    BoilerState boilerState,
    IOperationStorageService operationStorage,
    IResultStorageService resultStorage,
    IErrorStorageService errorStorage,
    IStepTimeStorageService stepTimeStorage,
    SuccessCountService successCountService,
    DualLogger<DatabaseTestResultStorage> logger) : ITestResultStorage
{
    /// <summary>
    /// Сохраняет результаты теста в базу данных.
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

        var serialNumber = boilerState.LastSerialNumber;
        if (string.IsNullOrEmpty(serialNumber))
        {
            logger.LogWarning("SerialNumber не найден в BoilerState");
            return SaveResult.Fail("Серийный номер котла не найден");
        }

        logger.LogInformation(
            "Начало сохранения результатов теста для котла {SerialNumber}, результат={TestResult}",
            serialNumber,
            testResult);

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(ct);

            var operation = await operationStorage.UpdateOperationAsync(context, serialNumber, testResult, ct);
            if (operation == null)
            {
                return SaveResult.Fail($"Operation для котла {serialNumber} не найдена");
            }

            var results = await resultStorage.CreateResultsAsync(context, operation, ct);
            if (results.Count > 0)
            {
                context.Results.AddRange(results);
            }

            var errors = await errorStorage.CreateErrorsAsync(context, operation, ct);
            if (errors.Count > 0)
            {
                context.Errors.AddRange(errors);
            }

            var stepTimes = await stepTimeStorage.CreateStepTimesAsync(context, operation, ct);
            if (stepTimes.Count > 0)
            {
                context.StepTimes.AddRange(stepTimes);
            }

            await context.SaveChangesAsync(ct);

            // Инкремент счётчика успешных тестов
            if (testResult == 1)
            {
                var successCount = await context.SuccessCounts.FirstOrDefaultAsync(ct);
                if (successCount == null)
                {
                    successCount = new SuccessCount { Count = 1 };
                    context.SuccessCounts.Add(successCount);
                }
                else
                {
                    successCount.Count++;
                }
                await context.SaveChangesAsync(ct);
                successCountService.NotifyCountChanged();
            }

            logger.LogInformation(
                "Результаты теста сохранены: Operation={OperationId}, Results={ResultCount}, Errors={ErrorCount}, StepTimes={StepTimeCount}",
                operation.Id,
                results.Count,
                errors.Count,
                stepTimes.Count);

            return SaveResult.Success();
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Ошибка при сохранении в базу данных");
            return SaveResult.Fail($"Ошибка базы данных: {ex.InnerException?.Message ?? ex.Message}");
        }
        catch (DbException ex)
        {
            logger.LogError(ex, "Ошибка подключения к базе данных");
            return SaveResult.Fail($"Ошибка подключения: {ex.Message}");
        }
        catch (TimeoutException ex)
        {
            logger.LogError(ex, "Таймаут при сохранении в базу данных");
            return SaveResult.Fail($"Таймаут: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Сохранение отменено");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Неожиданная ошибка при сохранении результатов теста");
            return SaveResult.Fail($"Ошибка: {ex.Message}");
        }
    }
}
