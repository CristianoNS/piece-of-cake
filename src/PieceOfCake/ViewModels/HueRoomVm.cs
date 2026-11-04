using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace PieceOfCake.ViewModels;

public sealed class HueRoomVm : INotifyPropertyChanged
{
    private readonly DispatcherTimer _debounce;
    private bool _isOn;
    private double _brightness;
    private bool _suppress;

    public HueRoomVm(
        string id,
        string name,
        string groupedLightId,
        bool isOn,
        double brightness,
        IReadOnlyList<HueSceneVm> scenes)
    {
        Id = id;
        Name = name;
        GroupedLightId = groupedLightId;
        Scenes = scenes;
        _isOn = isOn;
        _brightness = Math.Clamp(brightness, 1.0, 100.0);

        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            BrightnessCommitted?.Invoke(this);
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when the user flips the switch.</summary>
    public event Action<HueRoomVm>? PowerChanged;

    /// <summary>Raised shortly after the user stops moving the brightness slider.</summary>
    public event Action<HueRoomVm>? BrightnessCommitted;

    public string Id { get; }

    public string Name { get; }

    public string GroupedLightId { get; }

    public IReadOnlyList<HueSceneVm> Scenes { get; }

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
            if (!_suppress)
            {
                PowerChanged?.Invoke(this);
            }
        }
    }

    public double Brightness
    {
        get => _brightness;
        set
        {
            var clamped = Math.Clamp(value, 1.0, 100.0);
            if (Math.Abs(_brightness - clamped) < 0.01)
            {
                return;
            }

            _brightness = clamped;
            OnPropertyChanged();
            if (!_suppress)
            {
                _debounce.Stop();
                _debounce.Start();
            }
        }
    }

    /// <summary>Updates the state coming from the bridge without raising user-change events.</summary>
    public void SetState(bool isOn, double brightness)
    {
        _suppress = true;
        try
        {
            _debounce.Stop();
            IsOn = isOn;
            Brightness = brightness;
        }
        finally
        {
            _suppress = false;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
