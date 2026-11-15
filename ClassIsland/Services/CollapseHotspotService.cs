using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Threading;
using ClassIsland;
using ClassIsland.Controls.Components;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Models;
using ClassIsland.Platforms.Abstraction;
using ClassIsland.Services;
using ClassIsland.Shared;
using ClassIsland.Views;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services;

public sealed class CollapseHotspotService : IDisposable
{
    public static readonly Guid ComponentId = Guid.Parse("7E2A9C41-5B3D-4F8E-9A6C-1D2E3F4A5B6C");

    private readonly SettingsService _settingsService;
    private readonly IComponentsService _componentsService;
    private readonly ILogger<CollapseHotspotService> _logger;
    private readonly Dictionary<CollapseButtonComponent, PixelPoint> _buttonAnchors = [];
    private readonly Dictionary<CollapseButtonComponent, CollapseButtonWindow> _buttonWindows = [];
    private CollapseHotspotWindow? _window;
    private DispatcherTimer? _timer;
    private bool _isStarted;
    private bool _isDisposed;

    public CollapseHotspotService(
        SettingsService settingsService,
        IComponentsService componentsService,
        ILogger<CollapseHotspotService> logger)
    {
        _settingsService = settingsService;
        _componentsService = componentsService;
        _logger = logger;
    }

    public void Start()
    {
        if (_isStarted || _isDisposed)
        {
            return;
        }

        _isStarted = true;
        _window = new CollapseHotspotWindow(_settingsService);
        _settingsService.Settings.PropertyChanged += SettingsOnPropertyChanged;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _timer.Tick += (_, _) => UpdateVisibility();
        _timer.Start();
        UpdateVisibility();
    }

    public void Register(CollapseButtonComponent component, PixelPoint point)
    {
        _buttonAnchors[component] = point;
        Refresh();
    }

    public void Unregister(CollapseButtonComponent component)
    {
        _buttonAnchors.Remove(component);
        if (_buttonWindows.Remove(component, out var window))
        {
            window.Close();
        }
        Refresh();
    }

    private void SettingsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Settings.IsMainWindowVisible) or nameof(Settings.HideOnFullscreen))
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        if (!_isStarted || _isDisposed)
        {
            return;
        }
        Dispatcher.UIThread.Post(UpdateVisibility);
    }

    private bool HasCollapseComponent()
    {
        bool Contains(ClassIsland.Core.Models.Components.ComponentSettings settings)
        {
            return string.Equals(settings.Id, ComponentId.ToString(), StringComparison.OrdinalIgnoreCase)
                   || settings.Children?.Any(Contains) == true;
        }

        return _componentsService.CurrentComponents.Lines.Any(line => line.Children.Any(Contains));
    }

    private bool IsForegroundFullscreen(MainWindow? mainWindow)
    {
        if (!_settingsService.Settings.HideOnFullscreen || mainWindow == null)
        {
            return false;
        }

        try
        {
            var screen = mainWindow.GetSelectedScreenSafe() ?? _window?.Screens.Primary;
            return screen != null && PlatformServices.WindowPlatformService.IsForegroundWindowFullscreen(screen);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "无法判断前台窗口是否全屏");
            return false;
        }
    }

    private void UpdateVisibility()
    {
        if (_isDisposed || _window == null)
        {
            return;
        }

        try
        {
            var mainWindow = IAppHost.TryGetService<MainWindow>();
            var fullscreen = IsForegroundFullscreen(mainWindow);
            var mainVisible = _settingsService.Settings.IsMainWindowVisible;
            var hasComponent = HasCollapseComponent();

            var showHotspot = hasComponent && !mainVisible && !fullscreen;
            if (showHotspot)
            {
                if (!_window.IsVisible)
                {
                    _window.Show();
                }
                _window.PositionAtTop();
            }
            else if (_window.IsVisible)
            {
                _window.Hide();
            }

            var showButtons = mainVisible && hasComponent && !fullscreen;
            foreach (var (component, point) in _buttonAnchors.ToList())
            {
                if (!showButtons)
                {
                    if (_buttonWindows.TryGetValue(component, out var hiddenWindow) && hiddenWindow.IsVisible)
                    {
                        hiddenWindow.Hide();
                    }
                    continue;
                }

                if (!_buttonWindows.TryGetValue(component, out var buttonWindow))
                {
                    buttonWindow = new CollapseButtonWindow(_settingsService);
                    _buttonWindows[component] = buttonWindow;
                }

                var topmost = mainWindow?.Topmost == true;
                buttonWindow.MoveTo(point, topmost);
                if (!buttonWindow.IsVisible)
                {
                    buttonWindow.Show();
                }
                buttonWindow.MoveTo(point, topmost);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "更新收起按钮失败");
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _timer?.Stop();
        _settingsService.Settings.PropertyChanged -= SettingsOnPropertyChanged;
        if (_window?.IsVisible == true)
        {
            _window.Close();
        }
        foreach (var window in _buttonWindows.Values)
        {
            window.Close();
        }
        _buttonWindows.Clear();
        _buttonAnchors.Clear();
    }
}