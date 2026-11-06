using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PieceOfCake.ViewModels;

/// <summary>A plain tile on the home screen (refresh, passport, autostart).</summary>
public sealed class ActionTileVm : INotifyPropertyChanged
{
    private bool _isOn;
    private bool _isEnabled = true;

    public ActionTileVm(string title, string glyph, Action action)
    {
        Title = title;
        Glyph = glyph;
        Action = action;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title { get; }

    public string Glyph { get; }

    public Action Action { get; }

    /// <summary>True for toggle tiles (autostart) that are currently switched on.</summary>
    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (_isOn == value)
            {
                return;
            }

            _isOn = value;
            OnPropertyChanged();
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
            {
                return;
            }

            _isEnabled = value;
            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
