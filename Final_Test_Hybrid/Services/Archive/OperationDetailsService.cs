using Final_Test_Hybrid.Models.Archive;
using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Database.Config;
using Microsoft.EntityFrameworkCore;

namespace Final_Test_Hybrid.Services.Archive;

/// <summary>
/// Сервис для загрузки детальной информации об операции для архива.
/// </summary>
public class OperationDetailsService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    DualLogger<OperationDetailsService> logger)
{
    /// <summary>
    /// Загружает результаты измерений для операции по типу аудита.
    /// </summary>
    /// <param name="operationId">Идентификатор операции.</param>
    /// <param name="auditType">Тип аудита.</param>
    /// <returns>Список результатов измерений.</returns>
    public async Task<IReadOnlyList<ArchiveResultItem>> GetResultsAsync(
        long operationId,
        AuditType auditType)
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();

            return await db.Results
                .AsNoTracking()
                .Where(r => r.OperationId == operationId)
                .Where(r => r.ResultSettingHistory!.AuditType == auditType)
                .OrderBy(r => r.ResultSettingHistory!.ParameterName)
                .Select(r => new ArchiveResultItem
                {
                    Id = r.Id,
                    TestName = r.StepFinalTestHistory != null ? r.StepFinalTestHistory.Name : null,
                    ParameterName = r.ResultSettingHistory!.ParameterName,
                    Value = r.Value,
                    Min = r.Min,
                    Max = r.Max,
                    Status = r.Status,
                    Unit = r.ResultSettingHistory.Unit
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Ошибка загрузки результатов для операции {OperationId}, AuditType={AuditType}",
                operationId, auditType);
            return [];
        }
    }

    /// <summary>
    /// Загружает ошибки для операции.
    /// </summary>
    /// <param name="operationId">Идентификатор операции.</param>
    /// <returns>Список ошибок.</returns>
    public async Task<IReadOnlyList<ArchiveErrorItem>> GetErrorsAsync(long operationId)
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();

            return await db.Errors
                .AsNoTracking()
                .Where(e => e.OperationId == operationId)
                .OrderBy(e => e.ErrorSettingsHistory!.AddressError)
                .Select(e => new ArchiveErrorItem
                {
                    Id = e.Id,
                    Code = e.ErrorSettingsHistory!.AddressError,
                    Description = e.ErrorSettingsHistory.Description ?? string.Empty
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Ошибка загрузки ошибок для операции {OperationId}",
                operationId);
            return [];
        }
    }

    /// <summary>
    /// Загружает время шагов для операции.
    /// </summary>
    /// <param name="operationId">Идентификатор операции.</param>
    /// <returns>Список времён шагов.</returns>
    public async Task<IReadOnlyList<ArchiveStepTimeItem>> GetStepTimesAsync(long operationId)
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();

            return await db.StepTimes
                .AsNoTracking()
                .Where(st => st.OperationId == operationId)
                .OrderBy(st => st.StepFinalTestHistory!.Name)
                .Select(st => new ArchiveStepTimeItem
                {
                    Id = st.Id,
                    StepName = st.StepFinalTestHistory!.Name,
                    Duration = st.Duration
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Ошибка загрузки времени шагов для операции {OperationId}",
                operationId);
            return [];
        }
    }
}
