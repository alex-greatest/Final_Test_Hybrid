namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces;

public interface ITestSequenceLoader
{
    /// <summary>
    /// Загружает сырые данные из Excel по артикулу
    /// </summary>
    /// <param name="articleNumber">10-значный артикул котла</param>
    /// <returns>Список строк (каждая строка = 4 ячейки) или null при ошибке</returns>
    Task<List<string?[]>?> LoadRawDataAsync(string articleNumber);
}
