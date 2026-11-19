using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services.Management;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Core.Helpers.UI;
using ClassIsland.Models;
using ClassIsland.Services;
using ClassIsland.Shared;
using ClassIsland.Platforms.Abstraction;
using ClassIsland.ViewModels.SettingsPages;
using Microsoft.Extensions.Logging;
using Path = System.IO.Path;

namespace ClassIsland.Views.SettingPages;

/// <summary>
/// StorageSettingsPage.xaml 的交互逻辑
/// </summary>
[Group("classisland.general")]
[SettingsPageInfo("storage", "存储", "\ue6b7", "\ue6b6", SettingsPageCategory.Internal)]
public partial class StorageSettingsPage : SettingsPageBase
{
    public StorageSettingsViewModel ViewModel { get; } = IAppHost.GetService<StorageSettingsViewModel>();

    public ILogger<StorageSettingsPage> Logger => ViewModel.Logger;

    public StorageSettingsPage()
    {
        ViewModel.SettingsService.Settings.BackupFilesSize = Helpers.StorageSizeHelper.FormatSize(Helpers.StorageSizeHelper.GetFolderStorageSize(Path.Combine(CommonDirectories.AppRootFolderPath, "Backups/")));
        DataContext = this;
        InitializeComponent();
    }

    private async void ButtonCreateBackup_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.IsBackupFinished = false;
        ViewModel.IsBackingUp = true;
        try
        {
            await FileFolderService.CreateBackupAsync();
            ViewModel.IsBackupFinished = true;
            this.ShowSuccessToast("备份成功。");
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "无法创建备份。");
            this.ShowErrorToast("无法创建备份", exception);
        }
        ViewModel.IsBackingUp = false;
    }

    private void ButtonViewBackupFiles_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName = System.IO.Path.GetFullPath(Path.Combine(CommonDirectories.AppRootFolderPath, "Backups")),
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "无法浏览备份文件。");
            this.ShowErrorToast($"无法浏览备份文件", exception);
        }
    }

    private async void ButtonExportConfig_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var root = TopLevel.GetTopLevel(this);
            if (root == null)
            {
                return;
            }
            var path = await PlatformServices.FilePickerService.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = $"ClassislandLite-Config-{DateTime.Now:yyyyMMdd}.json",
                DefaultExtension = ".json",
                FileTypeChoices =
                [
                    new FilePickerFileType("ClassIsland Lite 配置")
                    {
                        Patterns = ["*.json"]
                    }
                ]
            }, root);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            SettingsTransferService.Export(ViewModel.SettingsService.Settings, path);
            this.ShowSuccessToast($"配置已导出到 {path}");
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "导出配置失败。");
            this.ShowErrorToast("导出配置失败", exception);
        }
    }

    private async void ButtonImportConfig_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var root = TopLevel.GetTopLevel(this);
            if (root == null)
            {
                return;
            }
            var paths = await PlatformServices.FilePickerService.OpenFilesPickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("ClassIsland Lite 配置")
                    {
                        Patterns = ["*.json"]
                    }
                ]
            }, root);
            var path = paths.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var count = SettingsTransferService.Import(ViewModel.SettingsService.Settings, path);
            this.ShowSuccessToast($"已导入 {count} 项配置，重启应用后生效。");
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "导入配置失败。");
            this.ShowErrorToast("导入配置失败", exception);
        }
    }
    private async void ButtonRecoverBackup_OnClick(object sender, RoutedEventArgs e)
    {
        if (!await ViewModel.ManagementService.AuthorizeByLevel(ViewModel.ManagementService.CredentialConfig.ExitApplicationAuthorizeLevel))
        {
            return;
        }
        AppBase.Current.Restart(["-m", "-r"]);
    }
}