using System;
using System.IO;
using PieceOfCake.Services;
using Xunit;

namespace PieceOfCake.Tests;

public sealed class LoggerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PieceOfCake.Tests." + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Append_CreatesDirectoryAndWritesTimestampedMessage()
    {
        var path = Path.Combine(_directory, "error.log");

        Logger.Append(path, "first error", maxBytes: 1024);

        var text = File.ReadAllText(path);
        Assert.Contains("first error", text, StringComparison.Ordinal);
        Assert.StartsWith("[", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Append_KeepsAddingBelowTheLimit()
    {
        var path = Path.Combine(_directory, "error.log");

        Logger.Append(path, "one", maxBytes: 1024 * 1024);
        Logger.Append(path, "two", maxBytes: 1024 * 1024);

        var text = File.ReadAllText(path);
        Assert.Contains("one", text, StringComparison.Ordinal);
        Assert.Contains("two", text, StringComparison.Ordinal);
        Assert.False(File.Exists(path + ".old"));
    }

    [Fact]
    public void Append_RotatesWhenTheLimitIsReached()
    {
        var path = Path.Combine(_directory, "error.log");

        Logger.Append(path, new string('a', 200), maxBytes: 100);
        Logger.Append(path, "after rotation", maxBytes: 100);

        Assert.Contains("after rotation", File.ReadAllText(path), StringComparison.Ordinal);
        Assert.DoesNotContain("aaaa", File.ReadAllText(path), StringComparison.Ordinal);
        Assert.Contains("aaaa", File.ReadAllText(path + ".old"), StringComparison.Ordinal);
    }

    [Fact]
    public void Append_ReplacesTheOldBackupSoSizeStaysBounded()
    {
        var path = Path.Combine(_directory, "error.log");

        Logger.Append(path, new string('a', 200), maxBytes: 100);
        Logger.Append(path, new string('b', 200), maxBytes: 100);
        Logger.Append(path, "latest", maxBytes: 100);

        var backup = File.ReadAllText(path + ".old");
        Assert.Contains("bbbb", backup, StringComparison.Ordinal);
        Assert.DoesNotContain("aaaa", backup, StringComparison.Ordinal);
        Assert.Contains("latest", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public void Append_DoesNotThrowWhenThePathIsUnusable()
    {
        Directory.CreateDirectory(_directory);
        var blocker = Path.Combine(_directory, "blocker");
        File.WriteAllText(blocker, "a file where a folder is expected");

        var exception = Record.Exception(() => Logger.Append(Path.Combine(blocker, "error.log"), "x", maxBytes: 100));

        Assert.Null(exception);
    }
}
