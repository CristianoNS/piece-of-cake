using System.Globalization;
using PieceOfCake.Services;
using Xunit;

namespace PieceOfCake.Tests;

public sealed class PassportServiceTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1073741824, "1 GB")]
    public void FormatBytes_UsesBinaryUnits(double bytes, string expected)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            Assert.Equal(expected, PassportService.FormatBytes(bytes));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Cell_EscapesPipesAndLineBreaks()
    {
        Assert.Equal("a\\|b c d", PassportService.Cell("a|b\rc\nd"));
    }

    [Fact]
    public void Cell_TreatsNullAsEmpty()
    {
        Assert.Equal(string.Empty, PassportService.Cell(null));
    }
}
