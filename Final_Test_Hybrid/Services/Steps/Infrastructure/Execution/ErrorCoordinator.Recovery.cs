using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Models.Steps;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;

/// <summary>
/// ErrorCoordinator partial: Reset and recovery logic.
/// </summary>
public partial class ErrorCoordinator
{
    #region Full Reset

    /// <summary>
    /// Полный сброс — очищает ВСЁ включая BoilerState и Grid.
    /// Вызывается при таймаутах, критических ошибках.
    /// </summary>
    public void Reset()
    {
        _logger.LogInformation("=== ПОЛНЫЙ СБРОС ===");
        ClearAllState();
        InvokeEventSafe(OnReset, "OnReset");
    }

    private void ClearAllState()
    {
        _interruptMessage.Clear();
        _pauseToken.Resume();
        _stateManager.ClearErrors();
        _stateManager.TransitionTo(ExecutionState.Failed);
        _statusReporter.ClearAll();
        _boilerState.Clear();
        _errorService.ClearActiveApplicationErrors();
    }

    #endregion

    #region Force Stop (Soft Reset)

    /// <summary>
    /// Мягкий сброс — очищает ошибки и сообщения, но СОХРАНЯЕТ BoilerState и Grid.
    /// Используется при успешном PLC reset (Ask_End получен вовремя).
    /// </summary>
    public void ForceStop()
    {
        _logger.LogInformation("=== МЯГКИЙ СБРОС (данные сохранены) ===");
        ClearErrorsOnly();
    }

    private void ClearErrorsOnly()
    {
        _interruptMessage.Clear();
        _pauseToken.Resume();
        _stateManager.ClearErrors();
        _stateManager.TransitionTo(ExecutionState.Idle);
        _errorService.ClearActiveApplicationErrors();
        // НЕ очищаем: _statusReporter, _boilerState
    }

    #endregion

    #region Recovery

    private async Task TryResumeFromPauseAsync(CancellationToken ct)
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

    private void ResumeIfPaused()
    {
        if (!_pauseToken.IsPaused) { return; }
        ResumeExecution();
    }

    private void ResumeExecution()
    {
        _interruptMessage.Clear();
        _pauseToken.Resume();
        ClearConnectionErrors();
        _notifications.ShowSuccess("Автомат восстановлен", "Тест продолжается");
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
