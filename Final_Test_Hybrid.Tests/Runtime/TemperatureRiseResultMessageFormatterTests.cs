using Final_Test_Hybrid.Services.Steps.Infrastructure;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class TemperatureRiseResultMessageFormatterTests
{
    [Fact]
    public void Format_PlacesEachValueImmediatelyAfterItsLabel()
    {
        var message = TemperatureRiseResultMessageFormatter.Format(12.3456f, 7.8912f);

        Assert.Equal("Температура: 12,346, Разница: 7,891", message);
    }

    [Fact]
    public void FormatWithFlow_PlacesEachValueImmediatelyAfterItsLabel()
    {
        var message = TemperatureRiseResultMessageFormatter.FormatWithFlow(12.3456f, 7.8912f, 3.4567f);

        Assert.Equal("Температура: 12,346, Разница: 7,891, Расход: 3,457", message);
    }
}
