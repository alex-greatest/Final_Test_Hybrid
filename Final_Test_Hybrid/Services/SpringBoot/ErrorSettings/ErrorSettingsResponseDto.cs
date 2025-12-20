namespace Final_Test_Hybrid.Services.SpringBoot.ErrorSettings;

public class ErrorSettingsResponseDto
{
    public long Id { get; set; }
    public string AddressError { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? StepName { get; set; }
}
