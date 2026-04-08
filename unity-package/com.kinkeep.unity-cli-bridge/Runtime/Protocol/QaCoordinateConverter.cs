namespace UnityCli.Protocol
{
    public static class QaCoordinateConverter
    {
        public static int ConvertScreenshotXToScreenX(int rawX, int screenWidth, int screenshotWidth)
        {
            return ScaleCoordinate(rawX, screenWidth, screenshotWidth);
        }

        public static int ConvertScreenshotYToScreenY(int rawY, int screenHeight, int screenshotHeight)
        {
            if (screenshotHeight <= 0)
            {
                return rawY;
            }

            int scaledY = ScaleCoordinate(rawY, screenHeight, screenshotHeight);
            return screenHeight - scaledY;
        }

        private static int ScaleCoordinate(int rawValue, int screenSize, int screenshotSize)
        {
            if (screenshotSize <= 0 || screenSize <= 0)
            {
                return rawValue;
            }

            long scaledValue = (long)rawValue * screenSize / screenshotSize;
            if (scaledValue > int.MaxValue)
            {
                return int.MaxValue;
            }

            if (scaledValue < int.MinValue)
            {
                return int.MinValue;
            }

            return (int)scaledValue;
        }
    }
}
