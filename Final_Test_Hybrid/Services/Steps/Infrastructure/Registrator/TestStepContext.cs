using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Diagnostic.Protocol;
using Final_Test_Hybrid.Services.OpcUa;
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
    PausableTagWaiter tagWaiter)
{
    public int ColumnIndex { get; } = columnIndex;
    public PausableOpcUaTagService OpcUa { get; } = opcUa;
    public ILogger Logger { get; } = logger;
    public IRecipeProvider RecipeProvider { get; } = recipeProvider;
    public Dictionary<string, object> Variables { get; } = [];
    public PauseTokenSource PauseToken { get; } = pauseToken;
    public PausableRegisterReader DiagReader { get; } = diagReader;
    public PausableRegisterWriter DiagWriter { get; } = diagWriter;
    public PausableTagWaiter TagWaiter { get; } = tagWaiter;

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
}
