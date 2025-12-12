namespace Final_Test_Hybrid.Services.Common;

public static class IndicatorHelper
{
    private const string GreenLampPath = "images/GreenLamp.png";
    private const string RedLampPath = "images/RedLamp.png";

    public static string GetLampImagePath(bool isConnected) =>
        isConnected ? GreenLampPath : RedLampPath;
}
