using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Steps;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

/// <summary>
/// Причина выхода из цикла PreExecution.
/// Используется для явного управления очисткой состояния.
/// </summary>
public enum CycleExitReason
{
    PipelineFailed,        // Pipeline вернул ошибку
    PipelineCancelled,     // Pipeline отменён (не сброс)
    TestCompleted,         // Тест завершился нормально
    SoftReset,             // Мягкий сброс (wasInScanPhase = true)
    HardReset,             // Жёсткий сброс
    RepeatRequested,       // OK повтор теста
    NokRepeatRequested,    // NOK повтор с подготовкой
}

/// <summary>
/// Координатор PreExecution — оркестратор для управления циклом ввода и подготовки.
/// Делегирует работу: ExecutionLoopManager (main loop), PreExecutionPipeline (подготовка),
/// RetryCoordinator (повторы шагов).
/// </summary>
public class PreExecutionCoordinator
{
    private readonly ExecutionLoopManager _loopManager;
    private readonly PreExecutionPipeline _pipeline;
    private readonly RetryCoordinator _retryCoordinator;
    private readonly PreExecutionInfrastructure _infra;
    private readonly PlcResetCoordinator _plcResetCoordinator;
    private readonly IErrorCoordinator _errorCoordinator;
    private bool _subscribed;

    public PreExecutionCoordinator(
        ExecutionLoopManager loopManager,
        PreExecutionPipeline pipeline,
        RetryCoordinator retryCoordinator,
        PreExecutionInfrastructure infra,
        PlcResetCoordinator plcResetCoordinator,
        IErrorCoordinator errorCoordinator)
    {
        _loopManager = loopManager;
        _pipeline = pipeline;
        _retryCoordinator = retryCoordinator;
        _infra = infra;
        _plcResetCoordinator = plcResetCoordinator;
        _errorCoordinator = errorCoordinator;
    }

    /// <summary>
    /// Принимает ли система ввод штрихкода.
    /// </summary>
    public bool IsAcceptingInput => _loopManager.IsAcceptingInput;

    /// <summary>
    /// Выполняется ли обработка (не принимает ввод, но PreExecution активен).
    /// </summary>
    public bool IsProcessing => _loopManager.IsProcessing;

    /// <summary>
    /// Текущий штрихкод.
    /// </summary>
    public string? CurrentBarcode => _loopManager.CurrentBarcode;

    /// <summary>
    /// Вызывается при изменении состояния.
    /// </summary>
    public event Action? OnStateChanged
    {
        add => _loopManager.OnStateChanged += value;
        remove => _loopManager.OnStateChanged -= value;
    }

    /// <summary>
    /// Запускает основной цикл PreExecution.
    /// </summary>
    public Task StartMainLoopAsync(CancellationToken ct)
    {
        EnsureSubscribed();
        return _loopManager.StartMainLoopAsync(ct);
    }

    /// <summary>
    /// Отправляет штрихкод в систему.
    /// </summary>
    public void SubmitBarcode(string barcode)
    {
        _loopManager.SubmitBarcode(barcode);
    }

    /// <summary>
    /// Возвращает текущий ScanStep (зависит от MES-режима).
    /// </summary>
    public ScanStepBase GetScanStep() => _pipeline.GetScanStep();

    /// <summary>
    /// Очищает текущий штрихкод (для UI совместимости).
    /// Фактически, barcode управляется через SystemLifecycleManager.
    /// </summary>
    public void ClearBarcode()
    {
        _loopManager.NotifyStateChanged();
    }

    #region Stop Signal Handling

    private void EnsureSubscribed()
    {
        if (_subscribed)
        {
            return;
        }
        _subscribed = true;
        SubscribeToStopSignals();
    }

    private void SubscribeToStopSignals()
    {
        _plcResetCoordinator.OnForceStop += HandleSoftStop;
        _plcResetCoordinator.OnAskEndReceived += HandleGridClear;
        _errorCoordinator.OnReset += HandleHardReset;
    }

    private void HandleStopSignal(PreExecutionResolution resolution)
    {
        var exitReason = resolution == PreExecutionResolution.SoftStop
            ? CycleExitReason.SoftReset
            : CycleExitReason.HardReset;

        if (_loopManager.HasActiveOperation())
        {
            _loopManager.SetPendingExitReason(exitReason);
        }
        else
        {
            HandleInactiveExit(exitReason);
        }
        _retryCoordinator.SignalExternalResolution(resolution);
    }

    private void HandleInactiveExit(CycleExitReason exitReason)
    {
        if (exitReason == CycleExitReason.HardReset)
        {
            _pipeline.ClearStateOnReset();
            _infra.StatusReporter.ClearAllExceptScan();
        }
    }

    private void HandleGridClear()
    {
        _pipeline.ClearStateOnReset();
        _infra.StatusReporter.ClearAllExceptScan();
    }

    private void HandleSoftStop() => HandleStopSignal(PreExecutionResolution.SoftStop);

    private void HandleHardReset()
    {
        HandleStopSignal(PreExecutionResolution.HardReset);
        _infra.StatusReporter.ClearAllExceptScan();
    }

    #endregion
}
