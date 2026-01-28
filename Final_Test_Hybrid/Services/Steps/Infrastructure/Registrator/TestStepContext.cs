using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Diagnostic.Protocol;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Completion;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

public class TestStepContext(
    int columnIndex,
    PausableOpcUaTagService opcUa,
    ILogger logger,
    IRecipeProvider recipeProvider,
    PauseTokenSource pauseToken,
    PausableRegisterReader diagReader,
    PausableRegisterWriter diagWriter,
    PausableTagWaiter tagWaiter,
    RangeSliderUiState rangeSliderUiState)
{
    private Action<string>? _progressCallback;

    public int ColumnIndex { get; } = columnIndex;
    public PausableOpcUaTagService OpcUa { get; } = opcUa;
    public ILogger Logger { get; } = logger;
    public IRecipeProvider RecipeProvider { get; } = recipeProvider;
    public Dictionary<string, object> Variables { get; } = [];
    public PauseTokenSource PauseToken { get; } = pauseToken;
    public PausableRegisterReader DiagReader { get; } = diagReader;
    public PausableRegisterWriter DiagWriter { get; } = diagWriter;
    public PausableTagWaiter TagWaiter { get; } = tagWaiter;
    public RangeSliderUiState RangeSliderUiState { get; } = rangeSliderUiState;

    /// <summary>
    /// Устанавливает callback для промежуточных результатов.
    /// Вызывается из ColumnExecutor при старте шага.
    /// </summary>
    internal void SetProgressCallback(Action<string>? callback)
    {
        _progressCallback = callback;
    }

    /// <summary>
    /// Сообщает промежуточный результат выполнения шага.
    /// Обновляет текущую строку в гриде без изменения статуса.
    /// </summary>
    public void ReportProgress(string message)
    {
        _progressCallback?.Invoke(message);
    }

    /// <summary>
    /// Pausable версия Task.Delay — останавливается при Auto OFF.
    /// </summary>
    public async Task DelayAsync(TimeSpan delay, CancellationToken ct)
    {
        var remaining = delay;
        while (remaining > TimeSpan.Zero)
        {
            await PauseToken.WaitWhilePausedAsync(ct);
            ct.ThrowIfCancellationRequested();

            var chunk = TimeSpan.FromMilliseconds(Math.Min(100, remaining.TotalMilliseconds));
            await Task.Delay(chunk, ct);
            remaining -= chunk;
        }
    }

    /// <summary>
    /// Показать RangeSlider для текущей колонки.
    /// </summary>
    /// <param name="config">Конфигурация слайдера.</param>
    /// <param name="ct">Токен отмены.</param>
    public Task ShowRangeSliderAsync(RangeSliderConfig config, CancellationToken ct)
    {
        return RangeSliderUiState.ShowAsync(ColumnIndex, config, ct);
    }

    /// <summary>
    /// Скрыть RangeSlider для текущей колонки.
    /// </summary>
    /// <param name="ct">Токен отмены.</param>
    public Task HideRangeSliderAsync(CancellationToken ct = default)
    {
        return RangeSliderUiState.HideAsync(ColumnIndex, ct);
    }
}
