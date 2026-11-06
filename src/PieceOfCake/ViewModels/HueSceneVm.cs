using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PieceOfCake.ViewModels;

public sealed class HueSceneVm : INotifyPropertyChanged
{
    private bool _isActive;

    public HueSceneVm(string id, string name, bool isActive = false)
    {
        Id = id;
        Name = name;
        _isActive = isActive;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; }

    public string Name { get; }

    /// <summary>True for the scene the bridge reports as currently applied (shown with an accent bar).</summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
            {
                return;
            }

            _isActive = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
        }
    }
}
