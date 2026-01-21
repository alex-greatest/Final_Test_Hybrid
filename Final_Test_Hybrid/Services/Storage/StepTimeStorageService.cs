using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Database.Config;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;
using Final_Test_Hybrid.Services.Storage.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Final_Test_Hybrid.Services.Storage;

/// <summary>
/// Сервис для сохранения StepTime (времен выполнения шагов) в базу данных.
/// </summary>
public class StepTimeStorageService(
    IStepTimingService stepTimingService,
    DualLogger<StepTimeStorageService> logger) : IStepTimeStorageService
{
    /// <summary>
    /// Создает список StepTime для batch insert.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="operation">Operation, к которой привязываются времена шагов.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Список StepTime для добавления в БД.</returns>
    public async Task<List<StepTime>> CreateStepTimesAsync(
        AppDbContext context,
        Operation operation,
        CancellationToken ct)
    {
        var timingRecords = stepTimingService.GetAll();
        if (timingRecords.Count == 0)
        {
            logger.LogInformation("Нет времен шагов для сохранения");
            return [];
        }

        var settingsDict = await LoadSettingsAsync(context, ct);
        var stepTimes = CreateStepTimeEntities(timingRecords, operation, settingsDict);

        logger.LogInformation(
            "Подготовлено {Count} времен шагов для сохранения",
            stepTimes.Count);

        return stepTimes;
    }

    /// <summary>
    /// Загружает все активные настройки шагов в словарь.
    /// </summary>
    private static async Task<Dictionary<string, StepFinalTestHistory>> LoadSettingsAsync(
        AppDbContext context,
        CancellationToken ct)
    {
        var settings = await context.StepFinalTestHistories
            .Where(h => h.IsActive)
            .ToListAsync(ct);

        return settings.ToDictionary(h => h.Name);
    }

    /// <summary>
    /// Создает StepTime entities из записей времени.
    /// </summary>
    private List<StepTime> CreateStepTimeEntities(
        IReadOnlyList<StepTimingRecord> timingRecords,
        Operation operation,
        Dictionary<string, StepFinalTestHistory> settingsDict)
    {
        var stepTimes = new List<StepTime>(timingRecords.Count);

        foreach (var item in timingRecords)
        {
            if (!settingsDict.TryGetValue(item.Name, out var stepHistory))
            {
                logger.LogWarning(
                    "StepFinalTestHistory не найдена для шага {StepName}",
                    item.Name);
                continue;
            }

            stepTimes.Add(new StepTime
            {
                OperationId = operation.Id,
                StepFinalTestHistoryId = stepHistory.Id,
                Duration = item.Duration
            });
        }

        return stepTimes;
    }
}
