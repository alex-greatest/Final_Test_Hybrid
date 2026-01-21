using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Database.Config;

namespace Final_Test_Hybrid.Services.Storage.Interfaces;

/// <summary>
/// Сервис для сохранения Result (результатов измерений) в базу данных.
/// </summary>
public interface IResultStorageService
{
    /// <summary>
    /// Создает список Result для batch insert.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="operation">Operation, к которой привязываются результаты.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Список Result для добавления в БД.</returns>
    Task<List<Result>> CreateResultsAsync(
        AppDbContext context,
        Operation operation,
        CancellationToken ct);
}
