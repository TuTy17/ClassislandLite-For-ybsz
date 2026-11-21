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
        // 隐藏时要走 ComponentPresenter.SetComponentVisible：它会同步 Settings.IsVisible
        // 并通知所在行重算，行内组件全部隐藏时整行才会消失。
        var visible = State.IsTomorrowViewActive && State.HasTomorrowWeather;
        IsVisible = visible;

        var parent = this.GetVisualParent();
        while (parent != null && parent is not ComponentPresenter)
        {
            parent = parent.GetVisualParent();
        }

        if (parent is ComponentPresenter presenter)
        {
            presenter.SetComponentVisible(visible);
            if (presenter.Settings is { } settings && !visible)
            {
                settings.LastWidthCache = 0;
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
