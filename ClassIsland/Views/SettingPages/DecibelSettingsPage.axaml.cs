using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Shared;
using ClassIsland.ViewModels.SettingsPages;

namespace ClassIsland.Views.SettingPages;

[Group("classisland.decibel")]
[SettingsPageInfo("decibel", "分贝检测器", "\uE9D9", "\uE9D8", SettingsPageCategory.Internal)]
public partial class DecibelSettingsPage : SettingsPageBase
{
    public DecibelSettingsViewModel ViewModel { get; } = IAppHost.GetService<DecibelSettingsViewModel>();

    public DecibelSettingsPage()
    {
        InitializeComponent();
        DataContext = this;
    }

    private async void Control_OnLoaded(object? sender, RoutedEventArgs e)
    {
        // 设置页会被缓存，重新进入时按当前档案刷新课程时段列表。
        ViewModel.RefreshLessonChoices();
        await ViewModel.RefreshDevicesAsync();
        if (ViewModel.SettingsService.Settings.IsDecibelMonitorEnabled)
        {
            await ViewModel.DecibelService.StartAsync();
        }
    }

    private async void ButtonRefreshDevices_OnClick(object? sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshDevicesAsync();
    }
}
