namespace Final_Test_Hybrid.Models.Errors;

public static partial class ErrorDefinitions
{
    internal static IReadOnlyList<ErrorDefinition> DeferredPlcErrors =>
    [
        AlNotConnectSensorPgbSetGasBurnerMax,
        AlNotConnectSensorPgbSetGasBurnerMin
    ];
}
