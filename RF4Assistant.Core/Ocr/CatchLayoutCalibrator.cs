namespace RF4AssistantPro.Ocr;

public sealed record CatchPixelRegion(
    int X,
    int Y,
    int Width,
    int Height);

public sealed record CatchCalibratedLayout(
    CatchPixelRegion Title,
    CatchPixelRegion ThreeBadgeWeight,
    CatchPixelRegion TwoBadgeWeight,
    CatchPixelRegion QualityBadge,
    int RulerLeft,
    int RulerRight,
    int RulerY,
    double Scale);

public static class CatchLayoutCalibrator
{
    private const int ReferenceWidth = 1920;
    private const int ReferenceRulerLeft = 190;
    private const int ReferenceRulerRight = 1729;
    private const int ReferenceRulerY = 229;

    public static CatchCalibratedLayout? TryCalibrate(
        byte[] luminance,
        int width,
        int height)
    {
        ArgumentNullException.ThrowIfNull(luminance);
        if (width < 640 ||
            height < 360 ||
            luminance.Length < checked(width * height))
        {
            return null;
        }

        var firstRow = Math.Max(0, (int)Math.Round(height * 0.12));
        var lastRow = Math.Min(
            height - 1,
            (int)Math.Round(height * 0.35));
        var minimumRun = (int)Math.Round(width * 0.55);
        var maximumRun = (int)Math.Round(width * 0.92);
        RulerCandidate? best = null;

        for (var y = firstRow; y <= lastRow; y++)
        {
            var rowOffset = y * width;
            var runStart = -1;
            for (var x = 0; x <= width; x++)
            {
                var bright = x < width &&
                             luminance[rowOffset + x] >= 55;
                if (bright && runStart < 0)
                {
                    runStart = x;
                    continue;
                }

                if (bright || runStart < 0)
                {
                    continue;
                }

                var runLength = x - runStart;
                if (runLength >= minimumRun &&
                    runLength <= maximumRun)
                {
                    var right = x - 1;
                    var sideDifference = Math.Abs(
                        runStart - (width - 1 - right));
                    var centerDifference = Math.Abs(
                        (runStart + right) / 2.0 - width / 2.0);
                    if (centerDifference <= width * 0.08)
                    {
                        var score =
                            sideDifference * 6.0 +
                            centerDifference * 3.0 +
                            runLength * 0.05;
                        var candidate = new RulerCandidate(
                            runStart,
                            right,
                            y,
                            score);
                        if (best is null || candidate.Score < best.Score)
                        {
                            best = candidate;
                        }
                    }
                }

                runStart = -1;
            }
        }

        if (best is null)
        {
            return null;
        }

        var referenceRulerWidth =
            ReferenceRulerRight - ReferenceRulerLeft + 1;
        var detectedRulerWidth = best.Right - best.Left + 1;
        var scale = detectedRulerWidth / (double)referenceRulerWidth;
        if (scale is < 0.55 or > 2.25)
        {
            return null;
        }

        var centerX = (best.Left + best.Right) / 2.0;
        CatchPixelRegion Region(
            int referenceX,
            int referenceY,
            int referenceRegionWidth,
            int referenceRegionHeight)
        {
            var x = (int)Math.Round(
                centerX + (referenceX - ReferenceWidth / 2.0) * scale);
            var y = (int)Math.Round(
                best.Y + (referenceY - ReferenceRulerY) * scale);
            var regionWidth = Math.Max(
                1,
                (int)Math.Round(referenceRegionWidth * scale));
            var regionHeight = Math.Max(
                1,
                (int)Math.Round(referenceRegionHeight * scale));

            x = Math.Clamp(x, 0, width - 1);
            y = Math.Clamp(y, 0, height - 1);
            regionWidth = Math.Min(regionWidth, width - x);
            regionHeight = Math.Min(regionHeight, height - y);
            return new CatchPixelRegion(
                x,
                y,
                regionWidth,
                regionHeight);
        }

        return new CatchCalibratedLayout(
            Region(826, 59, 269, 65),
            Region(758, 124, 144, 59),
            Region(841, 124, 131, 59),
            Region(989, 121, 163, 70),
            best.Left,
            best.Right,
            best.Y,
            scale);
    }

    private sealed record RulerCandidate(
        int Left,
        int Right,
        int Y,
        double Score);
}