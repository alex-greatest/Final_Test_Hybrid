using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;

public interface IRequiresPlcSubscriptions : ITestStep, IRequiresPlcTags
{
    // RequiredPlcTags наследуется от IRequiresPlcTags
}
