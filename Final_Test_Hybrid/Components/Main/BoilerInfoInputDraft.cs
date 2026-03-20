namespace Final_Test_Hybrid.Components.Main;

internal sealed class BoilerInfoInputDraft
{
    private string _lastSyncedPreservedValue = string.Empty;

    public string Draft { get; private set; } = string.Empty;

    public void SyncFromPreserved(string? preservedValue)
    {
        var normalizedValue = preservedValue ?? string.Empty;
        if (string.Equals(normalizedValue, _lastSyncedPreservedValue, StringComparison.Ordinal))
        {
            return;
        }

        _lastSyncedPreservedValue = normalizedValue;
        Draft = normalizedValue;
    }

    public void Update(string? value)
    {
        Draft = value ?? string.Empty;
    }

    public void Clear()
    {
        Draft = string.Empty;
        _lastSyncedPreservedValue = string.Empty;
    }

    public string? GetSubmitValue()
    {
        return string.IsNullOrWhiteSpace(Draft) ? null : Draft;
    }
}
