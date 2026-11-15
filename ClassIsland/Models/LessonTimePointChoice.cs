using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.Models;

public sealed class LessonTimePointChoice : ObservableRecipient
{
    private bool _isSelected;

    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public Action<LessonTimePointChoice, bool>? SelectionChanged { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (value == _isSelected) return;
            _isSelected = value;
            OnPropertyChanged();
            SelectionChanged?.Invoke(this, value);
        }
    }

    public override string ToString() => DisplayName;
}
