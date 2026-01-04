using Final_Test_Hybrid.Services.Common.Settings;

namespace Final_Test_Hybrid.Services.Main;

public class OrderState
{
    private readonly Lock _lock = new();
    private int? _orderNumber;
    private int? _amountBoilerOrder;
    private int? _amountBoilerMadeOrder;

    public OrderState(AppSettingsService appSettings)
    {
        appSettings.UseMesChanged += _ => Clear();
    }

    public event Action? OnChanged;

    public int? OrderNumber
    {
        get
        {
            lock (_lock)
            {
                return _orderNumber;
            }
        }
    }

    public int? AmountBoilerOrder
    {
        get
        {
            lock (_lock)
            {
                return _amountBoilerOrder;
            }
        }
    }

    public int? AmountBoilerMadeOrder
    {
        get
        {
            lock (_lock)
            {
                return _amountBoilerMadeOrder;
            }
        }
    }

    public void SetData(int orderNumber, int amountBoilerOrder, int amountBoilerMadeOrder)
    {
        lock (_lock)
        {
            _orderNumber = orderNumber;
            _amountBoilerOrder = amountBoilerOrder;
            _amountBoilerMadeOrder = amountBoilerMadeOrder;
        }
        OnChanged?.Invoke();
    }

    public void Clear()
    {
        lock (_lock)
        {
            _orderNumber = null;
            _amountBoilerOrder = null;
            _amountBoilerMadeOrder = null;
        }
        OnChanged?.Invoke();
    }
}
