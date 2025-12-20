namespace Final_Test_Hybrid.Services.SpringBoot.ErrorSettings;

public class ErrorSettingsDownloadResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public List<ErrorSettingsResponseDto> Items { get; init; } = [];

    public static ErrorSettingsDownloadResult Success(List<ErrorSettingsResponseDto> items) =>
        new() { IsSuccess = true, Items = items };

    public static ErrorSettingsDownloadResult Fail(string message) =>
        new() { IsSuccess = false, ErrorMessage = message };
}
