using System.ComponentModel;
using System.Runtime.CompilerServices;
using KicsitLibrary.Core.Interfaces;

namespace KicsitLibrary.Services.Preferences;

public sealed class HintService : IHintService, INotifyPropertyChanged
{
    private bool _showHelpfulHints = true;

    public static HintService Current { get; } = new();

    public bool ShowHelpfulHints
    {
        get => _showHelpfulHints;
        set
        {
            if (_showHelpfulHints == value)
            {
                return;
            }

            _showHelpfulHints = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
