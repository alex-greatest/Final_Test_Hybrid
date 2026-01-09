using Final_Test_Hybrid.Models.Errors;

namespace Final_Test_Hybrid.Services.Errors;

public interface IActiveErrorsService
{
    event Action? OnChanged;
    IReadOnlyList<ActiveError> GetErrors();
    void Add(string code, string description, string testName);
    void Remove(string code);
    void Clear();
}
