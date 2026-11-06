using System.Windows.Media;
using PieceOfCake.Native;
using PieceOfCake.Services;

namespace PieceOfCake.ViewModels;

/// <summary>A launcher tile for a program; the icon is read from the shell the first time it is shown.</summary>
public sealed class AppTileVm
{
    private ImageSource? _icon;
    private bool _iconLoaded;

    public AppTileVm(AppEntry entry)
    {
        Name = entry.Name;
        Target = entry.Target;
    }

    public string Name { get; }

    public string Target { get; }

    public ImageSource? Icon
    {
        get
        {
            if (!_iconLoaded)
            {
                _icon = ShellIcon.Load(Target);
                _iconLoaded = true;
            }

            return _icon;
        }
    }
}
