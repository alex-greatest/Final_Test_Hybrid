namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

public partial class PreExecutionCoordinator
{
    public async Task StartMainLoopAsync(CancellationToken ct)
    {
        EnsureSubscribed();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await RunSingleCycleAsync(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        finally
        {
            SetAcceptingInput(false);
        }
    }

    private async Task RunSingleCycleAsync(CancellationToken ct)
    {
        SetAcceptingInput(true);

        var barcode = await WaitForBarcodeAsync(ct);
        SetAcceptingInput(false);

        infra.StatusReporter.UpdateScanStepStatus(Models.Steps.TestStepStatus.Running, "Обработка штрихкода");
        _currentCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            state.ActivityTracker.SetPreExecutionActive(true);
            var result = await ExecutePreExecutionPipelineAsync(barcode, _currentCts.Token);

            if (result.Status == Interfaces.PreExecution.PreExecutionStatus.TestStarted)
            {
                await WaitForTestCompletionAsync(ct);
                HandlePostTestCompletion();
            }
        }
        catch (OperationCanceledException) when (_resetRequested)
        {
            _resetRequested = false;
            ClearStateOnReset();
        }
        finally
        {
            state.ActivityTracker.SetPreExecutionActive(false);
            _currentCts?.Dispose();
            _currentCts = null;
        }
    }

    private async Task<string> WaitForBarcodeAsync(CancellationToken ct)
    {
        _barcodeSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var reg = ct.Register(() => _barcodeSource.TrySetCanceled());
        var barcode = await _barcodeSource.Task;
        CurrentBarcode = barcode;
        OnStateChanged?.Invoke();
        return barcode;
    }

    private async Task WaitForTestCompletionAsync(CancellationToken ct)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void Handler() => tcs.TrySetResult();
        coordinators.TestCoordinator.OnSequenceCompleted += Handler;

        try
        {
            await using var reg = ct.Register(() => tcs.TrySetCanceled());
            await tcs.Task;
        }
        finally
        {
            coordinators.TestCoordinator.OnSequenceCompleted -= Handler;
        }
    }

    private void HandlePostTestCompletion()
    {
        coordinators.DialogCoordinator.ShowCompletionNotification(coordinators.TestCoordinator.HasErrors);
        if (_resetRequested)
        {
            _resetRequested = false;
            ClearStateOnReset();
            infra.StatusReporter.ClearAllExceptScan();
        }
    }
}
