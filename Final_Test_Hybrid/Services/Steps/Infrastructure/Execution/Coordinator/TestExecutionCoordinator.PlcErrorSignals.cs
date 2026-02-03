using Final_Test_Hybrid.Models.Plc.Tags;
using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Plc;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;

public partial class TestExecutionCoordinator
{
    /// <summary>
    /// Устанавливает тег Selected для PLC-блока.
    /// </summary>
    private async Task SetSelectedAsync(StepError error, bool value)
    {
        if (error.FailedStep is not IHasPlcBlockPath plcStep)
        {
            return;
        }
        var selectedTag = PlcBlockTagHelper.GetSelectedTag(plcStep);
        if (selectedTag == null)
        {
            return;
        }
        _logger.LogDebug("Установка Selected={Value} для {BlockPath}", value, plcStep.PlcBlockPath);
        var result = await _plcService.WriteAsync(selectedTag, value);
        if (result.Error != null)
        {
            _logger.LogWarning("Ошибка записи Selected: {Error}", result.Error);
        }
    }

    /// <summary>
    /// Устанавливает Fault для шагов без PLC-блока.
    /// </summary>
    private async Task SetFaultIfNoBlockAsync(ITestStep? step, CancellationToken ct)
    {
        if (step is IHasPlcBlockPath)
        {
            return;
        }

        _logger.LogDebug("Установка Fault=true для шага без блока");
        await _plcService.WriteAsync(BaseTags.Fault, true, ct);
    }

    /// <summary>
    /// Сбрасывает Fault для шагов без PLC-блока.
    /// </summary>
    private async Task ResetFaultIfNoBlockAsync(ITestStep? step, CancellationToken ct)
    {
        if (step is IHasPlcBlockPath)
        {
            return;
        }

        _logger.LogDebug("Сброс Fault=false для шага без блока");
        await _plcService.WriteAsync(BaseTags.Fault, false, ct);
    }

    /// <summary>
    /// Ожидает сброса сигналов после пропуска.
    /// </summary>
    /// <exception cref="TimeoutException">Сигнал не сброшен за 60 секунд.</exception>
    private async Task WaitForSkipSignalsResetAsync(ITestStep? step, CancellationToken ct)
    {
        if (step is IHasPlcBlockPath plcStep)
        {
            // Для шагов С блоком: ждём сброс Block.Error И Block.End
            // Skip детектируется по (End=true AND Error=true), нужно сбросить оба
            // Защита от stale сигналов при следующей ошибке в том же блоке
            var errorTag = PlcBlockTagHelper.GetErrorTag(plcStep);
            var endTag = PlcBlockTagHelper.GetEndTag(plcStep);

            if (errorTag != null)
            {
                _logger.LogDebug("Ожидание сброса Block.Error: {Tag}", errorTag);
                await _tagWaiter.WaitForFalseAsync(errorTag, TimeSpan.FromSeconds(60), ct);
            }
            if (endTag != null)
            {
                _logger.LogDebug("Ожидание сброса Block.End: {Tag}", endTag);
                await _tagWaiter.WaitForFalseAsync(endTag, TimeSpan.FromSeconds(60), ct);
            }
            return;
        }

        // Для шагов БЕЗ блока: ждём Test_End_Step=false с таймаутом
        // (PLC сбросит после того как PC сбросит Fault)
        _logger.LogDebug("Ожидание сброса Test_End_Step");
        await _tagWaiter.WaitForFalseAsync(BaseTags.TestEndStep, timeout: TimeSpan.FromSeconds(60), ct);
    }

    /// <summary>
    /// Сбрасывает сигнал Start для PLC-блока.
    /// </summary>
    private async Task ResetBlockStartAsync(ITestStep? step, CancellationToken ct)
    {
        if (step is not IHasPlcBlockPath plcStep)
        {
            return;
        }
        var startTag = PlcBlockTagHelper.GetStartTag(plcStep);
        if (startTag == null)
        {
            return;
        }
        _logger.LogDebug("Сброс Start для {BlockPath}", plcStep.PlcBlockPath);
        await _plcService.WriteAsync(startTag, false, ct);
    }

    /// <summary>
    /// Возвращает тег End для PLC-блока.
    /// </summary>
    private static string? GetBlockEndTag(ITestStep? step)
    {
        return PlcBlockTagHelper.GetEndTag(step as IHasPlcBlockPath);
    }

    /// <summary>
    /// Возвращает тег Error для PLC-блока.
    /// </summary>
    private static string? GetBlockErrorTag(ITestStep? step)
    {
        return PlcBlockTagHelper.GetErrorTag(step as IHasPlcBlockPath);
    }
}

