using System.Globalization;
using PieceOfCake.Services;
using Xunit;

namespace PieceOfCake.Tests;

public sealed class NetworkMeterTests
{
    [Theory]
    [InlineData(0, 1000, 1.0, 1000)]
    [InlineData(1000, 3000, 2.0, 1000)]
    [InlineData(500, 500, 1.0, 0)]
    public void CalculateRate_IsBytesPerSecond(long previous, long current, double seconds, double expected)
    {
        Assert.Equal(expected, NetworkMeter.CalculateRate(previous, current, seconds), precision: 6);
    }

    [Theory]
    [InlineData(1000, 500, 1.0)]
    [InlineData(0, 1000, 0.0)]
    [InlineData(0, 1000, -1.0)]
    public void CalculateRate_IsZeroForResetCountersOrNoElapsedTime(long previous, long current, double seconds)
    {
        Assert.Equal(0, NetworkMeter.CalculateRate(previous, current, seconds));
    }

    [Theory]
    [InlineData(-5, "0 B/s")]
    [InlineData(0, "0 B/s")]
    [InlineData(512, "512 B/s")]
    [InlineData(1536, "1.5 KB/s")]
    [InlineData(15360, "15 KB/s")]
    [InlineData(1048576, "1.0 MB/s")]
    [InlineData(52428800, "50 MB/s")]
    [InlineData(1073741824, "1.0 GB/s")]
    public void FormatSpeed_PicksUnitAndPrecision(double bytesPerSecond, string expected)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            Assert.Equal(expected, NetworkMeter.FormatSpeed(bytesPerSecond));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Sample_DoesNotThrowOnThisMachine()
    {
        var meter = new NetworkMeter();
        meter.Refresh();

        var exception = Record.Exception(() => meter.TrySample(out _, out _));

        Assert.Null(exception);
    }
}
