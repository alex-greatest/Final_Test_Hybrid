using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Test;

namespace Final_Test_Hybrid.Models.Steps;

public class TestMapRow
{
    public int RowIndex { get; init; }
    public ITestStep?[] Steps { get; } = new ITestStep?[4];
}
