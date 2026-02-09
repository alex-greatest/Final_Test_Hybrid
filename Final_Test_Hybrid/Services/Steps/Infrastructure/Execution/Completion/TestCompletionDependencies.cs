using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Storage;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Completion;

public record TestCompletionDependencies(
    OpcUaTagService PlcService,
    TagWaiter TagWaiter,
    OpcUaSubscription Subscription,
    ITestResultsService TestResultsService,
    ITestResultStorage Storage);
