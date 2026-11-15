using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using ClassIsland.Controls.NotificationEffects;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Helpers;
using ClassIsland.Shared.Enums;

namespace ClassIsland.Services;

public sealed class FullscreenReminderService : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly ILessonsService _lessonsService;
    private readonly ITopmostEffectPlayer _effectPlayer;

    public FullscreenReminderService(
        SettingsService settingsService,
        ILessonsService lessonsService,
        ITopmostEffectPlayer effectPlayer)
    {
        _settingsService = settingsService;
        _lessonsService = lessonsService;
        _effectPlayer = effectPlayer;
        _lessonsService.OnClass += LessonsServiceOnOnClass;
    }

    private void LessonsServiceOnOnClass(object? sender, EventArgs e)
    {
        var plan = _lessonsService.CurrentClassPlan;
        var item = _lessonsService.CurrentTimeLayoutItem;
        if (_lessonsService.CurrentState != TimeState.OnClass || plan?.TimeLayout == null || item?.TimeType != 0)
        {
            return;
        }

        var key = LessonTimePointHelper.CreateKey(plan.TimeLayoutId, item);
        if (!_settingsService.Settings.FullscreenEmphasisLessonKeys.Contains(key))
        {
            return;
        }

        PlayReminderEffect();
    }

    public void Preview()
    {
        PlayReminderEffect();
    }

    private void PlayReminderEffect()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var mainWindow = AppBase.Current.MainWindow;
            var screen = mainWindow?.Screens.ScreenFromWindow(mainWindow) ?? mainWindow?.Screens.Primary;
            if (screen == null)
            {
                return;
            }
            var center = new PixelPoint(
                screen.Bounds.X + screen.Bounds.Width / 2,
                screen.Bounds.Y + screen.Bounds.Height / 2);
            var brush = new SolidColorBrush(_settingsService.Settings.PrimaryColor);
            _effectPlayer.PlayEffect(new RippleEffect(center, brush));
        });
    }

    public void Dispose()
    {
        _lessonsService.OnClass -= LessonsServiceOnOnClass;
    }
}
