using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Test;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Plc;

public interface IRequiresPlcSubscriptions : ITestStep
{
    IReadOnlyList<string> RequiredPlcTags { get; }
}
