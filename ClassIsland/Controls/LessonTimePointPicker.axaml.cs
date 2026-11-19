using System.Collections;
using Avalonia;
using Avalonia.Controls;

namespace ClassIsland.Controls;

/// <summary>
/// 课程时间点选择器：按时间表分组，用可点选的胶囊按钮列出每节课，避免长列表下拉。
/// </summary>
public partial class LessonTimePointPicker : UserControl
{
    public static readonly StyledProperty<IEnumerable?> GroupsProperty =
        AvaloniaProperty.Register<LessonTimePointPicker, IEnumerable?>(nameof(Groups));

    public IEnumerable? Groups
    {
        get => GetValue(GroupsProperty);
        set => SetValue(GroupsProperty, value);
    }

    public LessonTimePointPicker()
    {
        InitializeComponent();
    }
}
