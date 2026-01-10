using Final_Test_Hybrid.Models.Results;

namespace Final_Test_Hybrid.Services.Results;

public interface ITestResultsService
{
    event Action? OnChanged;
    IReadOnlyList<TestResultItem> GetResults();
    void Add(string parameterName, string value, string tolerances, string unit);
    void Clear();
}
