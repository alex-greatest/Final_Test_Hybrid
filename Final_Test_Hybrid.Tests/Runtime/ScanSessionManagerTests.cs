using System.Runtime.CompilerServices;
using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.Scanner.RawInput;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class ScanSessionManagerTests
{
    [Fact]
    public void AcquireSession_RearmsOwnership_WhenResetClearedOwnerButHandlerIsCached()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        using var ownership = CreateOwnershipService(loggerFactory);
        using var sessionManager = new ScanSessionManager(
            ownership,
            loggerFactory.CreateLogger<ScanSessionManager>());
        Action<string> handler = _ => { };

        sessionManager.AcquireSession(handler);
        ownership.ReleaseAllForReset();

        sessionManager.AcquireSession(handler);

        var ownerState = ownership.GetCurrentOwnerState();
        Assert.Equal(ScannerInputOwnerKind.PreExecution, ownerState.CurrentOwner);
        Assert.True(ownerState.HasPreExecutionOwner);
    }

    [Fact]
    public void AcquireSession_KeepsExistingOwnership_WhenSessionIsAlreadyActive()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        using var ownership = CreateOwnershipService(loggerFactory);
        using var sessionManager = new ScanSessionManager(
            ownership,
            loggerFactory.CreateLogger<ScanSessionManager>());
        Action<string> handler = _ => { };

        sessionManager.AcquireSession(handler);
        sessionManager.AcquireSession(handler);

        var ownerState = ownership.GetCurrentOwnerState();
        Assert.Equal(ScannerInputOwnerKind.PreExecution, ownerState.CurrentOwner);
        Assert.True(ownerState.HasPreExecutionOwner);
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
}
