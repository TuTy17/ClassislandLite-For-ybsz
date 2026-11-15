using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Shared;
using ClassIsland.ViewModels.SettingsPages;

namespace ClassIsland.Views.SettingPages;

[Group("classisland.reminders")]
[SettingsPageInfo("fullscreen-reminder", "全屏强调提醒", "\uE7BA", "\uE7B9", SettingsPageCategory.Internal)]
public partial class FullscreenReminderSettingsPage : SettingsPageBase
{
    public FullscreenReminderSettingsViewModel ViewModel { get; } = IAppHost.GetService<FullscreenReminderSettingsViewModel>();

    public FullscreenReminderSettingsPage()
    {
        InitializeComponent();
        DataContext = this;
    }
}
