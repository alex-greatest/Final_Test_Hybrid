namespace Final_Test_Hybrid.Components.Main.Modals.Rework;

public class AdminAuthResult
{
    public bool Success { get; init; }
    public bool IsRepeatBypass { get; init; }
    public string Username { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }

    public static AdminAuthResult RepeatBypass() => new() { IsRepeatBypass = true, ErrorMessage = null };
}
