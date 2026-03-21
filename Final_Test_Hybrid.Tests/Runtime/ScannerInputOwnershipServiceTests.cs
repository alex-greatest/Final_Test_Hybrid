using System.Runtime.CompilerServices;
using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.Scanner.RawInput;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;
using Final_Test_Hybrid.Tests.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class ScannerInputOwnershipServiceTests
{
    [Fact]
    public void HandleBarcodeFromRawInput_RoutesToPreExecutionOwner()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        using var ownership = CreateOwnershipService(loggerFactory);
        string? receivedBarcode = null;

        ownership.EnsurePreExecutionOwner(barcode => receivedBarcode = barcode);
        TestInfrastructure.InvokePrivate(ownership, "HandleBarcodeFromRawInput", "12345678901");

        Assert.Equal("12345678901", receivedBarcode);
        Assert.Equal(ScannerInputOwnerKind.PreExecution, ownership.GetCurrentOwnerState().CurrentOwner);
        Assert.True(ownership.IsPreExecutionOwnerActive);
    }

    [Fact]
    public void HandleBarcodeFromRawInput_PrefersDialogOwner_AndReturnsToPreExecutionAfterRelease()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        using var ownership = CreateOwnershipService(loggerFactory);
        string? preExecutionBarcode = null;
        string? dialogBarcode = null;

        ownership.EnsurePreExecutionOwner(barcode => preExecutionBarcode = barcode);
        ownership.AcquireDialogOwner("dialog-a", barcode => dialogBarcode = barcode);

        TestInfrastructure.InvokePrivate(ownership, "HandleBarcodeFromRawInput", "dialog-scan");

        Assert.Null(preExecutionBarcode);
        Assert.Equal("dialog-scan", dialogBarcode);
        Assert.Equal(ScannerInputOwnerKind.Dialog, ownership.GetCurrentOwnerState().CurrentOwner);

        ownership.ReleaseDialogOwner("dialog-a");
        TestInfrastructure.InvokePrivate(ownership, "HandleBarcodeFromRawInput", "pre-scan");

        Assert.Equal("pre-scan", preExecutionBarcode);
        Assert.Equal(ScannerInputOwnerKind.PreExecution, ownership.GetCurrentOwnerState().CurrentOwner);
    }

    [Fact]
    public void ReleaseDialogOwner_WithStaleKey_DoesNotDropCurrentDialogOwner()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        using var ownership = CreateOwnershipService(loggerFactory);
        string? firstDialogBarcode = null;
        string? secondDialogBarcode = null;

        ownership.AcquireDialogOwner("dialog-a", barcode => firstDialogBarcode = barcode);
        ownership.AcquireDialogOwner("dialog-b", barcode => secondDialogBarcode = barcode);
        ownership.ReleaseDialogOwner("dialog-a");

        TestInfrastructure.InvokePrivate(ownership, "HandleBarcodeFromRawInput", "dialog-b-scan");

        Assert.Null(firstDialogBarcode);
        Assert.Equal("dialog-b-scan", secondDialogBarcode);
        Assert.Equal(ScannerInputOwnerKind.Dialog, ownership.GetCurrentOwnerState().CurrentOwner);
    }

    [Fact]
    public void HandleBarcodeFromRawInput_LogsWhenOwnerMissingAfterResetRelease()
    {
        using var loggerFactory = CreateLoggerFactory(out var provider);
        using var ownership = CreateOwnershipService(loggerFactory);
        var preExecutionCalled = false;

        ownership.EnsurePreExecutionOwner(_ => preExecutionCalled = true);
        ownership.AcquireDialogOwner("dialog-a", _ => preExecutionCalled = true);
        ownership.ReleaseAllForReset();
        TestInfrastructure.InvokePrivate(ownership, "HandleBarcodeFromRawInput", "orphan-scan");

        Assert.False(preExecutionCalled);
        Assert.Equal(ScannerInputOwnerKind.None, ownership.GetCurrentOwnerState().CurrentOwner);
        Assert.Contains(
            provider.Entries,
            entry => entry.Level == LogLevel.Warning
                && entry.Message.Contains("barcode_rejected_no_owner", StringComparison.Ordinal));
    }

    private static ScannerInputOwnershipService CreateOwnershipService(ILoggerFactory loggerFactory)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Scanner:VendorId"] = "1FBB",
                ["Scanner:ProductId"] = "3681"
            })
            .Build();
        var connectionState = (ScannerConnectionState)RuntimeHelpers.GetUninitializedObject(typeof(ScannerConnectionState));
        var detector = new ScannerDeviceDetector(configuration, loggerFactory.CreateLogger<ScannerDeviceDetector>());
        var rawInputService = new RawInputService(
            loggerFactory.CreateLogger<RawInputService>(),
            connectionState,
            detector);

        return new ScannerInputOwnershipService(
            rawInputService,
            loggerFactory.CreateLogger<ScannerInputOwnershipService>());
    }

    private static ILoggerFactory CreateLoggerFactory(out LocalRecordingLoggerProvider provider)
    {
        provider = new LocalRecordingLoggerProvider();
        var loggerProvider = provider;
        return LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(loggerProvider);
        });
    }

    private sealed class LocalRecordingLoggerProvider : ILoggerProvider
    {
        public List<LogEntry> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName)
        {
            return new LocalRecordingLogger(Entries);
        }

        public void Dispose()
        {
        }
    }

    private sealed class LocalRecordingLogger(List<LogEntry> entries) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
