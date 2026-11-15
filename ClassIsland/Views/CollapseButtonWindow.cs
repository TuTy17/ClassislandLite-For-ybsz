using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using ClassIsland.Services;

namespace ClassIsland.Views;

public sealed class CollapseButtonWindow : Window
{
    private readonly SettingsService _settingsService;

    public CollapseButtonWindow(SettingsService settingsService)
    {
        _settingsService = settingsService;
        Width = 28;
        Height = 26;
        MinWidth = 28;
        MinHeight = 26;
        CanResize = false;
        CanMaximize = false;
        CanMinimize = false;
        ShowInTaskbar = false;
        ShowActivated = false;
        SystemDecorations = SystemDecorations.None;
        Background = Brushes.Transparent;
        TransparencyBackgroundFallback = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Title = "Classisland Lite - Collapse Button";

        var hitArea = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        ToolTip.SetTip(hitArea, "收起主界面");
        hitArea.PointerPressed += (_, _) =>
        {
            _settingsService.Settings.IsMainWindowVisible = false;
        };
        Content = hitArea;
    }

    public void MoveTo(PixelPoint point, bool topmost)
    {
        Topmost = topmost;
        Position = new PixelPoint(
            Math.Max(0, point.X - 14),
            Math.Max(0, point.Y - 13));
    }
}