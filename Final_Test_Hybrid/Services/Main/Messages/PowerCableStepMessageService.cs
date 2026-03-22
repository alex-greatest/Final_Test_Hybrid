using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Main.Messages;

public sealed class PowerCableStepMessageService
{
    internal const string PowerCableStepName = "Elec/Connect_Power_Cable";

    private readonly Lock _lock = new();
    private readonly OpcUaConnectionState _connectionState;
    private readonly IStepTimingService _stepTimingService;
    private readonly ILogger<PowerCableStepMessageService> _logger;
    private bool _isMessageActive;

    public event Action? OnStateChanged;

    public PowerCableStepMessageService(
        OpcUaConnectionState connectionState,
        IStepTimingService stepTimingService,
        ILogger<PowerCableStepMessageService> logger)
    {
        _connectionState = connectionState;
        _stepTimingService = stepTimingService;
        _logger = logger;
        _connectionState.ConnectionStateChanged += HandleConnectionStateChanged;
        _stepTimingService.OnChanged += HandleStepTimingChanged;
    }

    public bool IsMessageActive
    {
        get
        {
            lock (_lock)
            {
                return _isMessageActive;
            }
        }
    }

    public void Activate()
    {
        UpdateState(CanShowMessage());
    }

    public void Deactivate()
    {
        UpdateState(false);
    }

    private void HandleConnectionStateChanged(bool isConnected)
    {
        if (isConnected)
        {
            return;
        }

        Deactivate();
    }

    private void HandleStepTimingChanged()
    {
        if (CanShowMessage())
        {
            return;
        }

        Deactivate();
    }

    private bool CanShowMessage()
    {
        return _connectionState.IsConnected
            && _stepTimingService.HasActiveStep(PowerCableStepName);
    }

    private void UpdateState(bool isMessageActive)
    {
        lock (_lock)
        {
            if (_isMessageActive == isMessageActive)
            {
                return;
            }

            _isMessageActive = isMessageActive;
        }

        NotifyChanged();
    }

    private void NotifyChanged()
    {
        var handler = OnStateChanged;
        if (handler is null)
        {
            return;
        }

        try
        {
            handler();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка в обработчике OnStateChanged power cable message");
        }
    }
}
