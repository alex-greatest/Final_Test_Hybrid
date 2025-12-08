namespace Final_Test_Hybrid.Services.Settings;

/// <summary>
/// Provides utility methods for UI indicators
/// </summary>
public static class IndicatorHelper
{
    private const string GreenLampPath = "images/GreenLamp.png";
    private const string RedLampPath = "images/RedLamp.png";

    /// <summary>
    /// Returns the appropriate lamp image path based on connection status
    /// </summary>
    /// <param name="isConnected">True for green lamp, false for red lamp</param>
    public static string GetLampImagePath(bool isConnected) => 
        isConnected ? GreenLampPath : RedLampPath;
}
