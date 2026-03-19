using Final_Test_Hybrid.Services.Common.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;

/// <summary>
/// Хранит terminal window runtime-пайплайна, где PLC-решение ещё не подтверждено.
/// </summary>
public sealed class RuntimeTerminalState(DualLogger<RuntimeTerminalState> logger)
{
    private int _completionActive;
    private int _postAskEndActive;

    public event Action? OnChanged;

    public bool IsCompletionActive => Volatile.Read(ref _completionActive) == 1;

    public bool IsPostAskEndActive => Volatile.Read(ref _postAskEndActive) == 1;

    public bool HasTerminalHandshake => IsCompletionActive || IsPostAskEndActive;

    /// <summary>
    /// Помечает active-window штатного completion-handshake.
    /// </summary>
    public void SetCompletionActive(bool isActive)
    {
        SetFlag(ref _completionActive, isActive, nameof(IsCompletionActive));
    }

    /// <summary>
    /// Помечает active-window post-AskEnd decision flow.
    /// </summary>
    public void SetPostAskEndActive(bool isActive)
    {
        SetFlag(ref _postAskEndActive, isActive, nameof(IsPostAskEndActive));
    }

    private void SetFlag(ref int field, bool isActive, string flagName)
    {
        var nextValue = isActive ? 1 : 0;
        var previousValue = Interlocked.Exchange(ref field, nextValue);
        if (previousValue == nextValue)
        {
            return;
        }

        try
        {
            OnChanged?.Invoke();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка в обработчике RuntimeTerminalState.{FlagName}", flagName);
        }
    }
}
