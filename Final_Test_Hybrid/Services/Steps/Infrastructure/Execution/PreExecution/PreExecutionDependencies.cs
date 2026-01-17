using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.Main.Messages;
using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Completion;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;
using Final_Test_Hybrid.Services.Steps.Steps;
using Final_Test_Hybrid.Services.Steps.Steps.Misc;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

/// <summary>
/// Шаги PreExecution.
/// </summary>
public class PreExecutionSteps(
    AppSettingsService appSettings,
    ScanBarcodeStep scanBarcodeStep,
    ScanBarcodeMesStep scanBarcodeMesStep,
    StartTimer1Step startTimer1Step,
    BlockBoilerAdapterStep blockBoilerAdapterStep)
{
    public ScanStepBase GetScanStep() => appSettings.UseMes ? scanBarcodeMesStep : scanBarcodeStep;
    public StartTimer1Step StartTimer1 => startTimer1Step;
    public BlockBoilerAdapterStep BlockBoilerAdapter => blockBoilerAdapterStep;
}

/// <summary>
/// Инфраструктурные сервисы для PreExecution.
/// </summary>
public class PreExecutionInfrastructure(
    PausableOpcUaTagService opcUa,
    OpcUaTagService plcService,
    PauseTokenSource pauseToken,
    IStepTimingService stepTimingService,
    StepStatusReporter statusReporter,
    ITestStepLogger testStepLogger,
    IErrorService errorService,
    ITestResultsService testResultsService,
    DualLogger<PreExecutionCoordinator> logger)
{
    public PausableOpcUaTagService OpcUa => opcUa;
    public OpcUaTagService PlcService => plcService;
    public PauseTokenSource PauseToken => pauseToken;
    public IStepTimingService StepTimingService => stepTimingService;
    public StepStatusReporter StatusReporter => statusReporter;
    public ITestStepLogger TestStepLogger => testStepLogger;
    public IErrorService ErrorService => errorService;
    public ITestResultsService TestResultsService => testResultsService;
    public DualLogger<PreExecutionCoordinator> Logger => logger;
}

/// <summary>
/// Связанные координаторы.
/// </summary>
public class PreExecutionCoordinators(
    TestExecutionCoordinator testCoordinator,
    IErrorCoordinator errorCoordinator,
    PlcResetCoordinator plcResetCoordinator,
    ScanDialogCoordinator dialogCoordinator,
    TestCompletionCoordinator completionCoordinator,
    TestCompletionUiState completionUiState)
{
    public TestExecutionCoordinator TestCoordinator => testCoordinator;
    public IErrorCoordinator ErrorCoordinator => errorCoordinator;
    public PlcResetCoordinator PlcResetCoordinator => plcResetCoordinator;
    public ScanDialogCoordinator DialogCoordinator => dialogCoordinator;
    public TestCompletionCoordinator CompletionCoordinator => completionCoordinator;
    public TestCompletionUiState CompletionUiState => completionUiState;
}

/// <summary>
/// Состояние выполнения.
/// </summary>
public class PreExecutionState(
    BoilerState boilerState,
    ExecutionActivityTracker activityTracker,
    ExecutionPhaseState phaseState)
{
    public BoilerState BoilerState => boilerState;
    public ExecutionActivityTracker ActivityTracker => activityTracker;
    public ExecutionPhaseState PhaseState => phaseState;
}
