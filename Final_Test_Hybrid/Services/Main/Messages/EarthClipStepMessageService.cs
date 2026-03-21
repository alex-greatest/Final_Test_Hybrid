using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Main.Messages;

public sealed class EarthClipStepMessageService
{
    internal const string EarthClipStepName = "Elec/Connect_Earth_Clip";

    private readonly Lock _lock = new();
    private readonly OpcUaConnectionState _connectionState;
    private readonly IStepTimingService _stepTimingService;
    private readonly ILogger<EarthClipStepMessageService> _logger;
    private bool _isMessageActive;

    public event Action? OnStateChanged;

    public EarthClipStepMessageService(
        OpcUaConnectionState connectionState,
        IStepTimingService stepTimingService,
        ILogger<EarthClipStepMessageService> logger)
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
            && _stepTimingService.HasActiveStep(EarthClipStepName);
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
            _logger.LogWarning(ex, "Ошибка в обработчике OnStateChanged earth clip message");
        }
    }
}
