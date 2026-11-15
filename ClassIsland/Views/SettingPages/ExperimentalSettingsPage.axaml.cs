using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Core.Helpers.UI;
using ClassIsland.Platforms.Abstraction;
using ClassIsland.Shared;
using ClassIsland.ViewModels.SettingsPages;

namespace ClassIsland.Views.SettingPages;

[Group("classisland.experimental")]
[SettingsPageInfo("experimental", "实验性功能", "\uE945", "\uE944", SettingsPageCategory.Internal)]
public partial class ExperimentalSettingsPage : SettingsPageBase
{
    public ExperimentalSettingsViewModel ViewModel { get; } = IAppHost.GetService<ExperimentalSettingsViewModel>();

    public ExperimentalSettingsPage()
    {
        InitializeComponent();
        DataContext = this;
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // 设置页会被缓存，重新进入时按当前档案刷新课程下拉列表。
        ViewModel.RefreshLessonChoices();
    }

    private async void ButtonImportRecipe_OnClick(object? sender, RoutedEventArgs e)
    {
        var root = TopLevel.GetTopLevel(this);
        if (root == null)
        {
            return;
        }

        var files = await PlatformServices.FilePickerService.OpenFilesPickerAsync(new FilePickerOpenOptions
        {
            Title = "选择菜单 Excel 文件",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Excel 工作簿")
                {
                    Patterns = ["*.xlsx"]
                }
            ]
        }, root);
        var path = files.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (await ViewModel.RecipeService.ValidateWorkbookAsync(path))
        {
            ViewModel.SettingsService.Settings.RecipeWorkbookPath = path;
            this.ShowSuccessToast("菜单 Excel 导入成功。");
        }
        else
        {
            this.ShowWarningToast(ViewModel.RecipeService.StatusText);
        }
    }

    private void ButtonPreviewLunch_OnClick(object? sender, RoutedEventArgs e)
    {
        PreviewRecipe(true);
    }

    private void ButtonPreviewDinner_OnClick(object? sender, RoutedEventArgs e)
    {
        PreviewRecipe(false);
    }

    private void PreviewRecipe(bool lunch)
    {
        if (ViewModel.RecipeService.TryGetTodayRecipe(out var recipe) && recipe != null)
        {
            ViewModel.RecipeService.ShowPreview(recipe, lunch);
            return;
        }
        this.ShowWarningToast("菜单中没有找到今天对应日期，请检查 Excel 中的日期列。");
    }
}
