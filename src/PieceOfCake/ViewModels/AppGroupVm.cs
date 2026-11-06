using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using PieceOfCake.Native;
using PieceOfCake.Services;

namespace PieceOfCake.ViewModels;

/// <summary>A tile that starts a group of programs; it shows up to four of their icons, like a folder.</summary>
public sealed class AppGroupVm
{
    private const int MaxIcons = 4;

    private IReadOnlyList<ImageSource>? _icons;

    public AppGroupVm(AppGroupEntry entry)
    {
        Name = entry.Name;
        Apps = entry.Apps;
    }

    public string Name { get; }

    public IReadOnlyList<AppEntry> Apps { get; }

    public string ToolTip => string.Join(", ", Apps.Select(a => a.Name));

    /// <summary>The icons are read from the shell the first time the tile is shown.</summary>
    public IReadOnlyList<ImageSource> Icons =>
        _icons ??= Apps
            .Select(a => ShellIcon.Load(a.Target))
            .OfType<ImageSource>()
            .Take(MaxIcons)
            .ToList();
}
