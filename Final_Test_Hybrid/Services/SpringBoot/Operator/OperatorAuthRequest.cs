namespace Final_Test_Hybrid.Services.SpringBoot.Operator;

public class OperatorAuthRequest
{
    public string Login { get; init; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string StationName { get; set; } = string.Empty;
}
