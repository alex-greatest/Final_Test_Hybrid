using Final_Test_Hybrid.Services.Steps.Interaces;

namespace Final_Test_Hybrid.Services.Steps.Models;

public class TestMapRow
{
    public int RowIndex { get; init; }
    public ITestStep?[] Steps { get; } = new ITestStep?[4];
}
