using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.OpcUa.Subscription;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Completion;

public record TestCompletionDependencies(
    OpcUaTagService PlcService,
    TagWaiter TagWaiter,
    OpcUaSubscription Subscription);
