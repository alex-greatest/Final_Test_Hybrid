using Final_Test_Hybrid.Services.Steps.Models;

namespace Final_Test_Hybrid.Services.Steps.Execution;

public interface ITestMapBuilder
{
    /// <summary>
    /// Создаёт Maps из сырых данных Excel
    /// </summary>
    /// <param name="rawData">Сырые данные из Excel (список строк по 4 ячейки)</param>
    /// <returns>Список Maps или null при ошибке валидации</returns>
    List<TestMap>? Build(List<string?[]> rawData);
}
