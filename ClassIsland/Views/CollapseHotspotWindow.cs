using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Services;

namespace ClassIsland.Views;

public sealed class CollapseHotspotWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly Border _buttonStrip;

    public CollapseHotspotWindow(SettingsService settingsService)
    {
        _settingsService = settingsService;
        Width = 32;
        Height = 8;
        MinWidth = 32;
        MinHeight = 8;
        CanResize = false;
        CanMaximize = false;
        CanMinimize = false;
        ShowInTaskbar = false;
        ShowActivated = false;
        SystemDecorations = SystemDecorations.None;
        Topmost = true;
        Background = Brushes.Transparent;
        TransparencyBackgroundFallback = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Opacity = 0.22;
        Title = "Classisland Lite - Collapse Hotspot";

        _buttonStrip = new Border
        {
            Width = 32,
            Height = 8,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Background = new SolidColorBrush(Color.Parse("#D0FFFFFF")),
            CornerRadius = new CornerRadius(4),
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        ToolTip.SetTip(_buttonStrip, "显示主界面");
        _buttonStrip.PointerEntered += (_, _) => Opacity = 0.62;
        _buttonStrip.PointerExited += (_, _) => Opacity = 0.22;
        _buttonStrip.PointerPressed += (_, _) =>
        {
            _settingsService.Settings.IsMainWindowVisible = true;
        };
        Content = _buttonStrip;
        Opened += (_, _) => PositionAtTop();
    }

    public void PositionAtTop()
    {
        var screen = Screens.Primary;
        if (screen == null)
        {
            return;
        }

        Position = new PixelPoint(
            screen.Bounds.X + Math.Max(0, (screen.Bounds.Width - (int)Width) / 2),
            screen.Bounds.Y);
    }
}