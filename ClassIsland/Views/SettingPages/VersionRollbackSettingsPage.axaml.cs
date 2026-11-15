using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Models;
using ClassIsland.Services;
using ClassIsland.Shared;
using ClassIsland.ViewModels.SettingsPages;
using FluentAvalonia.UI.Controls;

namespace ClassIsland.Views.SettingPages;

[Group("classisland.general")]
[SettingsPageInfo("version-rollback", "版本回退", "\uE7BA", "\uE7B9", SettingsPageCategory.Internal)]
public partial class VersionRollbackSettingsPage : SettingsPageBase
{
    public VersionRollbackSettingsViewModel ViewModel { get; } = IAppHost.GetService<VersionRollbackSettingsViewModel>();

    public VersionRollbackSettingsPage()
    {
        InitializeComponent();
        DataContext = this;
        ViewModel.Refresh();
    }

    private async void ButtonCreateSnapshot_OnClick(object? sender, RoutedEventArgs e)
    {
        await ViewModel.CreateSnapshotAsync();
    }

    private async void ButtonRestoreSnapshot_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: VersionSnapshot snapshot })
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "回退到这个版本？",
            Content = $"将把程序文件恢复为 {snapshot.CreatedAtText} 保存的版本（{snapshot.VersionText}），然后自动重启。\n\n课表、设置、组件布局等数据不会被改动。",
            PrimaryButtonText = "回退并重启",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            VersionRollbackService.ScheduleRestore(snapshot);
            Environment.Exit(0);
        }
        catch (Exception exception)
        {
            ViewModel.StatusText = "回退失败：" + exception.Message;
        }
    }

    private void ButtonDeleteSnapshot_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: VersionSnapshot snapshot })
        {
            ViewModel.DeleteSnapshot(snapshot);
        }
    }
}
