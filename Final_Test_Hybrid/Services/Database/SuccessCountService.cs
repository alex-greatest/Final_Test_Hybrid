using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Database.Config;
using Microsoft.EntityFrameworkCore;

namespace Final_Test_Hybrid.Services.Database;

/// <summary>
/// Сервис для управления счётчиком успешных тестов.
/// Предоставляет единую точку доступа к счётчику и событие для синхронизации UI.
/// </summary>
public class SuccessCountService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    DualLogger<SuccessCountService> logger)
{
    /// <summary>
    /// Событие, вызываемое при изменении счётчика.
    /// </summary>
    public event Action? OnCountChanged;

    /// <summary>
    /// Получает текущее значение счётчика.
    /// </summary>
    /// <returns>Текущее значение счётчика успешных тестов.</returns>
    public async Task<long> GetCountAsync()
    {
        try
        {
            await using var context = await dbContextFactory.CreateDbContextAsync();
            var record = await context.SuccessCounts.FirstOrDefaultAsync();
            return record?.Count ?? 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при получении счётчика успешных тестов");
            return 0;
        }
    }

    /// <summary>
    /// Обновляет значение счётчика.
    /// </summary>
    /// <param name="newCount">Новое значение счётчика.</param>
    public async Task UpdateCountAsync(long newCount)
    {
        try
        {
            await using var context = await dbContextFactory.CreateDbContextAsync();
            var record = await context.SuccessCounts.FirstOrDefaultAsync();
            if (record == null)
            {
                record = new SuccessCount { Count = newCount };
                context.SuccessCounts.Add(record);
            }
            else
            {
                record.Count = newCount;
            }
            await context.SaveChangesAsync();
            logger.LogInformation("Счётчик успешных тестов обновлён: {Count}", newCount);
            OnCountChanged?.Invoke();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обновлении счётчика успешных тестов");
            throw;
        }
    }

    /// <summary>
    /// Сбрасывает счётчик в 0.
    /// </summary>
    public async Task ResetCountAsync()
    {
        await UpdateCountAsync(0);
        logger.LogInformation("Счётчик успешных тестов обнулён");
    }

    /// <summary>
    /// Уведомляет подписчиков об изменении счётчика.
    /// Вызывается из DatabaseTestResultStorage после инкремента.
    /// </summary>
    public void NotifyCountChanged()
    {
        OnCountChanged?.Invoke();
    }
}
