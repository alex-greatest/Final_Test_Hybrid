using Final_Test_Hybrid.Models.Results;

namespace Final_Test_Hybrid.Services.Results;

public interface ITestResultsService
{
    event Action? OnChanged;
    IReadOnlyList<TestResultItem> GetResults();
    void Add(string parameterName, string value, string min, string max, int status, bool isRanged, string unit, string test);
    void Remove(string parameterName);
    void Clear();
}
