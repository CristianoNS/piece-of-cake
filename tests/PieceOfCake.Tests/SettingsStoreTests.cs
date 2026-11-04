using PieceOfCake.Services;
using Xunit;

namespace PieceOfCake.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public void AppKey_RoundTripsThroughProtectedStorage()
    {
        var store = new SettingsStore();

        store.SetAppKey("secret-key");

        Assert.NotEqual("secret-key", store.Current.ProtectedAppKey);
        Assert.Equal("secret-key", store.GetAppKey());
    }

    [Fact]
    public void AppKey_IsNullWhenNothingStored()
    {
        Assert.Null(new SettingsStore().GetAppKey());
    }

    [Fact]
    public void AppKey_IsClearedWhenSetToEmpty()
    {
        var store = new SettingsStore();
        store.SetAppKey("secret-key");

        store.SetAppKey("");

        Assert.Equal(string.Empty, store.Current.ProtectedAppKey);
        Assert.Null(store.GetAppKey());
    }

    [Fact]
    public void AppKey_IsNullWhenStoredValueIsCorrupt()
    {
        var store = new SettingsStore();
        store.Current.ProtectedAppKey = "not-base64!";

        Assert.Null(store.GetAppKey());
    }
}
