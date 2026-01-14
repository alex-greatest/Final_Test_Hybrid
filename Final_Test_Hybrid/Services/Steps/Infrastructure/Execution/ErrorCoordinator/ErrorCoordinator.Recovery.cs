using Final_Test_Hybrid.Models.Errors;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;

/// <summary>
/// ErrorCoordinator partial: Reset and recovery logic.
/// </summary>
public partial class ErrorCoordinator
{
    #region Full Reset

    /// <summary>
    /// Полный сброс — снимает паузу и сигнализирует подписчикам.
    /// Подписчики решают что очищать.
    /// </summary>
    public void Reset()
    {
        _logger.LogInformation("=== ПОЛНЫЙ СБРОС ===");
        _pauseToken.Resume();
        InvokeEventSafe(OnReset, "OnReset");
    }

    #endregion

    #region Force Stop (Soft Reset)

    /// <summary>
    /// Мягкий сброс — снимает паузу.
    /// Используется при успешном PLC reset (Ask_End получен вовремя).
    /// </summary>
    public void ForceStop()
    {
        _logger.LogInformation("=== МЯГКИЙ СБРОС (данные сохранены) ===");
        _pauseToken.Resume();
    }

    #endregion

    #region Recovery

    private async Task TryResumeFromPauseAsync(CancellationToken ct)
    {
        if (_disposed) { return; }

        IncrementActiveOperations();
        try
        {
            if (!await TryAcquireLockAsync(ct)) { return; }

            try
            {
                ResumeIfPaused();
            }
            finally
            {
                ReleaseLockSafe();
            }
        }
        finally
        {
            DecrementActiveOperations();
        }
    }

    private void ResumeIfPaused()
    {
        if (!_pauseToken.IsPaused) { return; }
        ResumeExecution();
    }

    private void ResumeExecution()
    {
        _pauseToken.Resume();
        ClearConnectionErrors();
        InvokeEventSafe(OnRecovered, "OnRecovered");
    }

    private void ClearConnectionErrors()
    {
        _errorService.Clear(ErrorDefinitions.OpcConnectionLost.Code);
        _errorService.Clear(ErrorDefinitions.TagReadTimeout.Code);
    }

    #endregion

    #region Helpers

    private void InvokeEventSafe(Action? handler, string eventName)
    {
        try
        {
            handler?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в обработчике {EventName}", eventName);
        }
    }

    #endregion
}
