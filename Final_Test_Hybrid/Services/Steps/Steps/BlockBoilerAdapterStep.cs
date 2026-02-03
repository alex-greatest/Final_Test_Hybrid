using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Main.Messages;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;

namespace Final_Test_Hybrid.Services.Steps.Steps;

public class BlockBoilerAdapterStep(
    PausableTagWaiter tagWaiter,
    ExecutionPhaseState phaseState,
    DualLogger<BlockBoilerAdapterStep> logger) : IPreExecutionStep, IHasPlcBlockPath, IRequiresPlcTags
{
    private const string BlockPath = "DB_VI.Block_Boiler_Adapter";
    private const string StartTag = "ns=3;s=\"DB_VI\".\"Block_Boiler_Adapter\".\"Start\"";
    private const string EndTag = "ns=3;s=\"DB_VI\".\"Block_Boiler_Adapter\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"Block_Boiler_Adapter\".\"Error\"";
    private static readonly TimeSpan EndResetTimeout = TimeSpan.FromSeconds(5);

    public string Id => "block-boiler-adapter";
    public string Name => "Block boiler adapter";
    public string Description => "Блокирование адаптера";
    public bool IsVisibleInStatusGrid => true;
    public bool IsSkippable => false;
    public string ErrorSourceTitle => ErrorSourceDefaults.Stand;
    public string PlcBlockPath => BlockPath;

    // IRequiresPlcTags - валидация тегов при старте
    public IReadOnlyList<string> RequiredPlcTags => [StartTag, EndTag, ErrorTag];

    public async Task<PreExecutionResult> ExecuteAsync(PreExecutionContext context, CancellationToken ct)
    {
        phaseState.SetPhase(ExecutionPhase.WaitingForAdapter);
        logger.LogInformation("Запуск блокировки адаптера");
        var endResetResult = await WaitForEndResetAsync(ct);
        if (endResetResult != null)
        {
            return endResetResult;
        }
        var writeResult = await context.OpcUa.WriteAsync(StartTag, true, ct);
        return writeResult.Error != null
            ? CreateWriteError(writeResult.Error)
            : await WaitForCompletionAsync(context, ct);
    }

    private async Task<PreExecutionResult?> WaitForEndResetAsync(CancellationToken ct)
    {
        try
        {
            await tagWaiter.WaitForFalseAsync(EndTag, EndResetTimeout, ct);
            return null;
        }
        catch (TimeoutException)
        {
            return PreExecutionResult.FailRetryable("PLC не сбросил End", canSkip: false, userMessage: "PLC не сбросил End");
        }
    }

    private async Task<PreExecutionResult> WaitForCompletionAsync(PreExecutionContext context, CancellationToken ct)
    {
        var waitResult = await tagWaiter.WaitAnyAsync(
            tagWaiter.CreateWaitGroup<BlockResult>()
                .WaitForTrue(EndTag, () => BlockResult.Success, "End")
                .WaitForTrue(ErrorTag, () => BlockResult.Error, "Error"),
            ct);
        return waitResult.Result switch
        {
            BlockResult.Success => await HandleSuccessAsync(context, ct),
            BlockResult.Error => CreateRetryableError(),
            _ => PreExecutionResult.Fail("Неизвестный результат")
        };
    }

    private async Task<PreExecutionResult> HandleSuccessAsync(PreExecutionContext context, CancellationToken ct)
    {
        logger.LogInformation("Адаптер заблокирован успешно");

        var writeResult = await context.OpcUa.WriteAsync(StartTag, false, ct);
        if (writeResult.Error != null)
        {
            return CreateWriteError(writeResult.Error);
        }

        phaseState.Clear();
        return PreExecutionResult.Continue();
    }

    private PreExecutionResult CreateRetryableError()
    {
        const string error = "Ошибка блокировки адаптера";
        logger.LogWarning("{Error}", error);
        return PreExecutionResult.FailRetryable(
            error,
            canSkip: false,
            userMessage: error,
            errors: []);
    }

    private PreExecutionResult CreateWriteError(string error)
    {
        logger.LogError("Ошибка записи Start: {Error}", error);
        return PreExecutionResult.FailRetryable(
            $"Ошибка записи Start: {error}",
            canSkip: false,
            userMessage: "Ошибка связи с ПЛК",
            errors: []);
    }

    private enum BlockResult { Success, Error }
}
