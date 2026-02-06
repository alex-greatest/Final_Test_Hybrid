using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;

public sealed class BarcodeDebounceHandler(
    BarcodeScanService barcodeScanService,
    StepStatusReporter statusReporter,
    PreExecutionCoordinator preExecutionCoordinator,
    OpcUaConnectionState connectionState)
{
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromMilliseconds(250);
    private readonly Lock _lock = new();
    private CancellationTokenSource? _debounceCts;
    private int _sequence;
    private string? _latestBarcode;

    public void Handle(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return;
        }
        StartDebounce(barcode);
    }

    private void StartDebounce(string barcode)
    {
        var state = UpdateDebounceState(barcode);
        _ = RunDebounceAsync(state.Sequence, state.Token);
    }

    private DebounceState UpdateDebounceState(string barcode)
    {
        lock (_lock)
        {
            var previousCts = _debounceCts;
            if (previousCts != null)
            {
                previousCts.Cancel();
                previousCts.Dispose();
            }
            _debounceCts = new CancellationTokenSource();
            _latestBarcode = barcode;
            _sequence++;
            return new DebounceState(_sequence, _debounceCts.Token);
        }
    }

    private async Task RunDebounceAsync(int sequence, CancellationToken token)
    {
        try
        {
            await Task.Delay(DebounceWindow, token).ConfigureAwait(false);
            await ProcessIfCurrentAsync(sequence).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ProcessIfCurrentAsync(int sequence)
    {
        var barcode = TryGetLatestBarcode(sequence);
        if (barcode == null)
        {
            return;
        }
        await DispatchBarcodeAsync(barcode).ConfigureAwait(false);
    }

    private string? TryGetLatestBarcode(int sequence)
    {
        lock (_lock)
        {
            return sequence != _sequence ? null : _latestBarcode;
        }
    }

    private Task DispatchBarcodeAsync(string barcode)
    {
        return !CanAcceptBarcode() ? Task.CompletedTask : HandleBarcodeAsync(barcode);
    }

    private Task HandleBarcodeAsync(string barcode)
    {
        var validation = barcodeScanService.Validate(barcode);
        if (validation.IsValid)
        {
            preExecutionCoordinator.SubmitBarcode(validation.Barcode);
            return Task.CompletedTask;
        }
        statusReporter.UpdateScanStepStatus(TestStepStatus.Error, validation.Error!);
        return Task.CompletedTask;
    }

    private bool CanAcceptBarcode()
    {
        return preExecutionCoordinator.IsAcceptingInput && connectionState.IsConnected;
    }

    private readonly record struct DebounceState(int Sequence, CancellationToken Token);
}
