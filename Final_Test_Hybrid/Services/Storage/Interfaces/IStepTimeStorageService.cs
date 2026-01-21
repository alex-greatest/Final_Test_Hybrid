using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Database.Config;

namespace Final_Test_Hybrid.Services.Storage.Interfaces;

/// <summary>
/// Сервис для сохранения StepTime (времен выполнения шагов) в базу данных.
/// </summary>
public interface IStepTimeStorageService
{
    /// <summary>
    /// Создает список StepTime для batch insert.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="operation">Operation, к которой привязываются времена шагов.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Список StepTime для добавления в БД.</returns>
    Task<List<StepTime>> CreateStepTimesAsync(
        AppDbContext context,
        Operation operation,
        CancellationToken ct);
}
