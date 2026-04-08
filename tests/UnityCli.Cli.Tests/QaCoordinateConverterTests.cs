using UnityCli.Protocol;

namespace UnityCli.Cli.Tests;

public sealed class QaCoordinateConverterTests
{
    [Fact]
    public void ConvertScreenshotXToScreenX_WithoutScreenshotWidth_ReturnsRawX()
    {
        int convertedX = QaCoordinateConverter.ConvertScreenshotXToScreenX(rawX: 300, screenWidth: 960, screenshotWidth: 0);

        Assert.Equal(300, convertedX);
    }

    [Fact]
    public void ConvertScreenshotYToScreenY_WithoutScreenshotHeight_ReturnsRawY()
    {
        int convertedY = QaCoordinateConverter.ConvertScreenshotYToScreenY(rawY: 200, screenHeight: 1080, screenshotHeight: 0);

        Assert.Equal(200, convertedY);
    }

    [Fact]
    public void ConvertScreenshotCoordinates_WithScreenshotDimensions_ScalesAndInvertsY()
    {
        int convertedX = QaCoordinateConverter.ConvertScreenshotXToScreenX(rawX: 300, screenWidth: 960, screenshotWidth: 1920);
        int convertedY = QaCoordinateConverter.ConvertScreenshotYToScreenY(rawY: 200, screenHeight: 540, screenshotHeight: 1080);

        Assert.Equal(150, convertedX);
        Assert.Equal(440, convertedY);
    }
}
