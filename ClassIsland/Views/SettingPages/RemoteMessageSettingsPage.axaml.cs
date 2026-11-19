using Avalonia.Controls;
using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Services;
using ClassIsland.Shared;
using ClassIsland.ViewModels.SettingsPages;

namespace ClassIsland.Views.SettingPages;

[Group("classisland.general")]
[SettingsPageInfo("remote-message", "远程提醒", "\uE7ED", "\uE7EE", SettingsPageCategory.Internal)]
public partial class RemoteMessageSettingsPage : SettingsPageBase
{
    public RemoteMessageSettingsViewModel ViewModel { get; } = IAppHost.GetService<RemoteMessageSettingsViewModel>();

    public RemoteMessageSettingsPage()
    {
        InitializeComponent();
        DataContext = this;
        ViewModel.Refresh();
    }

    private async void ButtonTestConnection_OnClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.StatusMessage = "正在连接…";
        var result = await ViewModel.RemoteMessageService.TestConnectionAsync();
        ViewModel.StatusMessage = result;
    }

    private void ButtonPreview_OnClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.RemoteMessageService.ShowLocalPreview("这是一条远程提醒的预览消息，下课后来办公室一趟。");
    }

}
