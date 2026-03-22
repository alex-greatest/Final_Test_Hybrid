namespace Final_Test_Hybrid.Services.OpcUa.Subscription;

/// <summary>
/// Последнее известное значение тега вместе с sequence номером обновления.
/// </summary>
public readonly record struct SubscriptionValueEntry(object? Value, ulong UpdateSequence);
