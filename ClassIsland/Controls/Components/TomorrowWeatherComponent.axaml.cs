using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using ClassIsland.Core.Controls;
using ClassIsland.Controls;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Services;

namespace ClassIsland.Controls.Components;

/// <summary>
/// “次日天气”小组件：放在主界面第二行，只有在显示次日课表时才出现。
/// </summary>
[ComponentInfo("8E4B7C21-3A5D-4C9E-9B02-6F1D2A7C5E10", "次日天气", "\uE753",
    "显示次日课表时，在第二行显示明天的天气（含天气图标与雨雪提示）。")]
public partial class TomorrowWeatherComponent : ComponentBase, INotifyPropertyChanged
{
    public TomorrowWeatherService State { get; }

    public TomorrowWeatherComponent(TomorrowWeatherService state)
    {
        State = state;
        InitializeComponent();
        DataContext = this;

        AttachedToVisualTree += OnAttached;
        DetachedFromVisualTree += OnDetached;
    }

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        State.PropertyChanged += StateOnPropertyChanged;
        State.Refresh();
        UpdateVisibility();
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        State.PropertyChanged -= StateOnPropertyChanged;
    }

    private void StateOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        // 不满足“显示次日课表”条件时整块隐藏，并连宿主一起收起来，
        // 否则宿主会保留上次宽度，在第二行画出一个空的背景方块。
        var visible = State.IsTomorrowViewActive && State.HasTomorrowWeather;
        IsVisible = visible;

        var parent = this.GetVisualParent();
        while (parent != null && parent is not ComponentPresenter)
        {
            parent = parent.GetVisualParent();
        }

        if (parent is ComponentPresenter presenter)
        {
            presenter.IsVisibleInternal = visible;
            if (presenter.Settings is { } settings && !visible)
            {
                settings.LastWidthCache = 0;
            }
        }

        // 这一行只有“次日天气”一个组件时，直接整行显示/隐藏，杜绝任何残留空块。
        var line = this.FindAncestorOfType<MainWindowLine>();
        if (line?.Settings is { Children.Count: 1 })
        {
            line.IsVisible = visible;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
