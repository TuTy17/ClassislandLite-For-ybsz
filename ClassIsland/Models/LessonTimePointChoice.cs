using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.Models;

public sealed class LessonTimePointChoice : ObservableRecipient
{
    private bool _isSelected;

    public required string Key { get; init; }

    /// <summary>
    /// 该课程对应的所有时间点键（单周/双周合并时会有多个）。
    /// </summary>
    public IReadOnlyList<string> Keys { get; init; } = [];
    public required string DisplayName { get; init; }

    /// <summary>
    /// 短名称，用于表格式选择界面，例如“第 3 节”。
    /// </summary>
    public string ShortName { get; init; } = "";

    /// <summary>
    /// 时间范围，例如“09:50-10:30”。
    /// </summary>
    public string TimeText { get; init; } = "";

    /// <summary>
    /// 所属时间表名称。
    /// </summary>
    public string GroupName { get; init; } = "";

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

/// <summary>
/// 一组课程时间点（同一张时间表），用于表格式选择界面。
/// </summary>
public sealed class LessonTimePointGroup
{
    public required string Name { get; set; }
    public ObservableCollection<LessonTimePointChoice> Items { get; } = [];
}
