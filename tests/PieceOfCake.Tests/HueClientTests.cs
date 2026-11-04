using PieceOfCake.Services;
using Xunit;

namespace PieceOfCake.Tests;

public sealed class HueClientTests
{
    [Theory]
    [InlineData("192.168.1.10")]
    [InlineData("10.0.0.5")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.254")]
    [InlineData("169.254.1.1")]
    public void IsValidBridgeAddress_AcceptsPrivateIPv4(string address)
    {
        Assert.True(HueClient.IsValidBridgeAddress(address));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-ip")]
    [InlineData("8.8.8.8")]
    [InlineData("172.32.0.1")]
    [InlineData("172.15.0.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    public void IsValidBridgeAddress_RejectsEverythingElse(string? address)
    {
        Assert.False(HueClient.IsValidBridgeAddress(address));
    }
}
