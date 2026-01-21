using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Database.Config;

namespace Final_Test_Hybrid.Services.Storage.Interfaces;

/// <summary>
/// Сервис для сохранения Error (ошибок тестирования) в базу данных.
/// </summary>
public interface IErrorStorageService
{
    /// <summary>
    /// Создает список Error для batch insert.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <param name="operation">Operation, к которой привязываются ошибки.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Список Error для добавления в БД.</returns>
    Task<List<Error>> CreateErrorsAsync(
        AppDbContext context,
        Operation operation,
        CancellationToken ct);
}
