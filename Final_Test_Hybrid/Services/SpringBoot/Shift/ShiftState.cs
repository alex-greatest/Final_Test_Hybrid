namespace Final_Test_Hybrid.Services.SpringBoot.Shift;

public class ShiftState
{
    private readonly Lock _lock = new();
    public int? ShiftNumber { get; private set; }
    public event Action<int?>? ShiftNumberChanged;

    public void SetShiftNumber(int? value)
    {
        lock (_lock)
        {
            if (ShiftNumber == value)
            {
                return;
            }
            ShiftNumber = value;
        }
        ShiftNumberChanged?.Invoke(value);
    }
}
