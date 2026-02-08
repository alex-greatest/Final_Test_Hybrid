namespace Final_Test_Hybrid.Services.Diagnostic.Services;

/// <summary>
/// Единый критерий lock-контекста по данным ping.
/// Используется для согласованной интерпретации BoilerStatus и LastErrorId.
/// </summary>
internal static class BoilerLockCriteria
{
    private const short StatusPauseBranch = 1;
    private const short StatusPlcSignalBranch = 2;

    // Коды из 111.txt
    private static readonly HashSet<ushort> TargetErrorIds =
    [
        1, 2, 3, 4, 5, 7, 8, 9, 10, 11, 12, 13, 14, 18, 23, 26
    ];

    public static bool IsTargetErrorId(ushort errorId)
    {
        return TargetErrorIds.Contains(errorId);
    }

    public static bool IsLockStatus(short boilerStatus)
    {
        return boilerStatus is StatusPauseBranch or StatusPlcSignalBranch;
    }
}
