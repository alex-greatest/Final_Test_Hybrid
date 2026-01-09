using Final_Test_Hybrid.Models.Errors;

namespace Final_Test_Hybrid.Services.Errors;

public interface IErrorHistoryService
{
    event Action? OnChanged;
    IReadOnlyList<ErrorHistoryItem> GetHistory();
    void Add(string code, string description, string testName);
    void MarkResolved(string code);
    void Clear();
}
