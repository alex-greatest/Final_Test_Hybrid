using System.Collections.Concurrent;
using System.Reflection;
using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Common.UI;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator.Behaviors;
using Final_Test_Hybrid.Settings.OpcUa;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Tests.TestSupport;

internal sealed class TestStepLoggerStub : ITestStepLogger
{
    public void StartNewSession()
    {
    }

    public void LogStepStart(string stepName)
    {
    }

    public void LogStepEnd(string stepName)
    {
    }

    public void LogDebug(string message, params object?[] args)
    {
    }

    public void LogInformation(string message, params object?[] args)
    {
    }

    public void LogWarning(string message, params object?[] args)
    {
    }

    public void LogError(Exception? ex, string message, params object?[] args)
    {
    }

    public string? GetCurrentLogFilePath()
    {
        return null;
    }
}

internal sealed class TestErrorService : IErrorService
{
    public List<string> ClearedCodes { get; } = [];
    public List<string> ClearedPlcCodes { get; } = [];
    public List<string> RaisedPlcCodes { get; } = [];

    public event Action? OnActiveErrorsChanged
    {
        add { }
        remove { }
    }

    public event Action? OnHistoryChanged
    {
        add { }
        remove { }
    }

    public IReadOnlyList<ActiveError> GetActiveErrors()
    {
        return [];
    }

    public IReadOnlyList<ErrorHistoryItem> GetHistory()
    {
        return [];
    }

    public void Raise(ErrorDefinition error, string? details = null)
    {
    }

    public void RaiseInStep(ErrorDefinition error, string stepId, string stepName, string? details = null)
    {
    }

    public void Clear(string errorCode)
    {
        ClearedCodes.Add(errorCode);
    }

    public void ClearActiveApplicationErrors()
    {
    }

    public void RaisePlc(ErrorDefinition error, string? stepId = null, string? stepName = null)
    {
        RaisedPlcCodes.Add(error.Code);
    }

    public void ClearPlc(string errorCode)
    {
        ClearedPlcCodes.Add(errorCode);
    }

    public void ClearAllActiveErrors()
    {
    }

    public void ClearHistory()
    {
    }

    public bool HasResettableErrors => false;

    public bool HasActiveErrors => false;

    public bool IsHistoryEnabled { get; set; }
}

internal sealed class TestNotificationService : INotificationService
{
    public void ShowSuccess(string summary, string detail, double? duration = null, bool closeOnClick = true, string? id = null, string? style = null)
    {
    }

    public void ShowError(string summary, string detail, double? duration = null, bool closeOnClick = true, string? id = null, string? style = null)
    {
    }

    public void ShowWarning(string summary, string detail, double? duration = null, bool closeOnClick = true, string? id = null, string? style = null)
    {
    }

    public void ShowInfo(string summary, string detail, double? duration = null, bool closeOnClick = true, string? id = null, string? style = null)
    {
    }
}

internal sealed class InterruptBehaviorStub(
    InterruptReason reason,
    Func<IInterruptContext, CancellationToken, Task> executeAsync,
    TaskCompletionSource<InterruptReason>? executed = null) : IInterruptBehavior
{
    public InterruptReason Reason => reason;

    public string Message => reason.ToString();

    public ErrorDefinition? AssociatedError => null;

    public async Task ExecuteAsync(IInterruptContext context, CancellationToken ct)
    {
        executed?.TrySetResult(reason);
        await executeAsync(context, ct);
    }
}

internal static class TestInfrastructure
{
    private static readonly ILoggerFactory SharedLoggerFactory = LoggerFactory.Create(_ => { });

    public static DualLogger<T> CreateDualLogger<T>()
    {
        return new DualLogger<T>(SharedLoggerFactory.CreateLogger<T>(), new TestStepLoggerStub());
    }

    public static ILogger<T> CreateLogger<T>()
    {
        return SharedLoggerFactory.CreateLogger<T>();
    }

    public static IOptions<OpcUaSettings> CreateOpcUaOptions()
    {
        return Options.Create(new OpcUaSettings
        {
            EndpointUrl = "opc.tcp://localhost:4840",
            ApplicationName = "Final_Test_Hybrid.Tests",
            ReconnectIntervalMs = 1000,
            SessionTimeoutMs = 10000
        });
    }

    public static TField GetPrivateField<TField>(object instance, string name)
    {
        var field = Assert.IsAssignableFrom<FieldInfo>(instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic));
        return Assert.IsType<TField>(field.GetValue(instance));
    }

    public static void SetPrivateField(object instance, string name, object? value)
    {
        var field = Assert.IsAssignableFrom<FieldInfo>(instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic));
        field.SetValue(instance, value);
    }

    public static object? InvokePrivate(object instance, string name, params object?[] args)
    {
        var method = Assert.IsAssignableFrom<MethodInfo>(instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic));
        return method.Invoke(instance, args);
    }

    public static Task InvokePrivateAsync(object instance, string name, params object?[] args)
    {
        return Assert.IsAssignableFrom<Task>(InvokePrivate(instance, name, args));
    }

    public static ConcurrentDictionary<string, object?> GetSubscriptionValues(object subscription)
    {
        return GetPrivateField<ConcurrentDictionary<string, object?>>(subscription, "_values");
    }

    public static OpcUaSubscription CreateSubscription()
    {
        var connectionState = new OpcUaConnectionState(CreateLogger<OpcUaConnectionState>());
        return new OpcUaSubscription(
            connectionState,
            CreateOpcUaOptions(),
            CreateDualLogger<OpcUaSubscription>());
    }
}
