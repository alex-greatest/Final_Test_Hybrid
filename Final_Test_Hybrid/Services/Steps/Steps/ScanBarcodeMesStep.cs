using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Steps;

public class ScanBarcodeMesStep(
    ExecutionMessageState messageState,
    ILogger<ScanBarcodeMesStep> logger,
    ITestStepLogger testStepLogger) : ITestStep, IPreExecutionStep
{
    public string Id => "scan-barcode-mes";
    public string Name => "Сканирование штрихкода MES";
    public string Description => "Сканирует штрихкод и отправляет в MES";
    public bool IsVisibleInEditor => false;
    public bool IsVisibleInStatusGrid => true;

    public Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        return Task.FromResult(TestStepResult.Pass());
    }

    public Task<PreExecutionResult> ExecuteAsync(PreExecutionContext context, CancellationToken ct)
    {
        testStepLogger.LogStepStart(Name);
        LogInfo("Штрихкод получен: {Barcode}", context.Barcode);
        messageState.SetMessage("Штрихкод получен");
        testStepLogger.LogStepEnd(Name);
        return Task.FromResult(PreExecutionResult.Continue(context.Barcode));
    }

    private void LogInfo(string message, params object?[] args)
    {
        logger.LogInformation(message, args);
        testStepLogger.LogInformation(message, args);
    }
}
