using Final_Test_Hybrid.Services.Common.Settings;

namespace Final_Test_Hybrid.Services.Storage;

/// <summary>
/// Маршрутизатор для сохранения результатов теста.
/// Выбирает между MES и локальной БД на основе настройки UseMes.
/// </summary>
public class TestResultStorageRouter(
    DatabaseTestResultStorage dbStorage,
    MesTestResultStorage mesStorage,
    AppSettingsService appSettings) : ITestResultStorage
{
    /// <summary>
    /// Сохраняет результаты теста в MES или локальную БД в зависимости от настройки UseMes.
    /// </summary>
    /// <param name="testResult">Результат теста (1 = Ok, 2 = Nok).</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат сохранения.</returns>
    public Task<SaveResult> SaveAsync(int testResult, CancellationToken ct)
    {
        return appSettings.UseMes
            ? mesStorage.SaveAsync(testResult, ct)
            : dbStorage.SaveAsync(testResult, ct);
    }
}
