using Final_Test_Hybrid.Services.Common.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Completion;

public partial class TestCompletionCoordinator(
    TestCompletionDependencies deps,
    TestCompletionUiState uiState,
    DualLogger<TestCompletionCoordinator> logger)
{
    public bool IsWaitingForCompletion { get; private set; }
}
