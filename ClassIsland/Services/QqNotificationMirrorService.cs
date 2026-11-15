using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using ClassIsland.Core;
using ClassIsland.Models;
using ClassIsland.Platforms.Abstraction;
using ClassIsland.Platforms.Abstraction.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services;

public sealed class QqNotificationMirrorService : ObservableRecipient, IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly ILogger<QqNotificationMirrorService> _logger;
    private readonly DispatcherTimer _pollTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly HashSet<string> _knownNotifications = [];
    private readonly object _mainWindowLock = new();
    private bool _initialized;
    private bool _isPolling;
    private string _currentMessage = "";
    private bool _isMessageVisible;
    private string _statusText = "QQ 消息镜像未启用";

    public string CurrentMessage { get => _currentMessage; private set => SetProperty(ref _currentMessage, value); }
    public bool IsMessageVisible { get => _isMessageVisible; private set => SetProperty(ref _isMessageVisible, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

    public QqNotificationMirrorService(SettingsService settingsService, ILogger<QqNotificationMirrorService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
        _settingsService.Settings.PropertyChanged += SettingsOnPropertyChanged;
        _pollTimer.Tick += async (_, _) => await PollAsync();
        _hideTimer.Tick += (_, _) => HideMessage();
    }

    public void Initialize()
    {
        _initialized = true;
        _ = RestartAsync();
    }

    public void ShowPreviewMessage(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            ShowMessage(message);
        }
    }

    private async Task RestartAsync()
    {
        _pollTimer.Stop();
        HideMessage();
        if (!_initialized || !_settingsService.Settings.IsQqNotificationMirrorEnabled)
        {
            StatusText = "QQ 消息镜像未启用";
            return;
        }

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240))
        {
            StatusText = "当前系统不支持 Windows 通知镜像";
            return;
        }

        try
        {
            var access = await PlatformServices.WindowPlatformService.RequestNotificationAccessAsync();
            if (!access.IsAllowed)
            {
                StatusText = access.Status;
                return;
            }

            _knownNotifications.Clear();
            var existing = await PlatformServices.WindowPlatformService.GetToastNotificationsAsync();
            foreach (var notification in existing)
            {
                _knownNotifications.Add(GetNotificationKey(notification));
            }
            StatusText = "正在监听 QQ 的 Windows 消息通知";
            _pollTimer.Start();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "无法启用 Windows 通知读取。");
            StatusText = "此安装形式无法读取系统通知：" + exception.Message;
        }
    }

    private async Task PollAsync()
    {
        if (_isPolling || !_settingsService.Settings.IsQqNotificationMirrorEnabled)
        {
            return;
        }

        _isPolling = true;
        try
        {
            var notifications = await PlatformServices.WindowPlatformService.GetToastNotificationsAsync();
            foreach (var notification in notifications.OrderBy(item => item.CreationTime))
            {
                var key = GetNotificationKey(notification);
                if (!_knownNotifications.Add(key) || !IsQqNotification(notification))
                {
                    continue;
                }

                var message = ExtractText(notification);
                if (!string.IsNullOrWhiteSpace(message))
                {
                    ShowMessage(message);
                }
            }

            var activeKeys = notifications.Select(GetNotificationKey).ToHashSet(StringComparer.Ordinal);
            _knownNotifications.IntersectWith(activeKeys);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "读取 QQ 系统通知失败。");
            StatusText = "读取系统通知失败：" + exception.Message;
            _pollTimer.Stop();
        }
        finally
        {
            _isPolling = false;
        }
    }

    private static bool IsQqNotification(PlatformToastNotification notification)
    {
        var displayName = notification.AppName;
        var appId = notification.AppId;
        return displayName.Contains("QQ", StringComparison.OrdinalIgnoreCase) ||
               displayName.Contains("腾讯", StringComparison.OrdinalIgnoreCase) ||
               appId.Contains("QQ", StringComparison.OrdinalIgnoreCase) ||
               appId.Contains("Tencent", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractText(PlatformToastNotification notification)
    {
        return string.Join("  ·  ", notification.Text.Select(text => text.Trim()).Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private static string GetNotificationKey(PlatformToastNotification notification)
    {
        return $"{notification.Id}:{notification.CreationTime.UtcTicks}";
    }

    private void ShowMessage(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            CurrentMessage = message;
            IsMessageVisible = true;
            _hideTimer.Stop();
            _hideTimer.Start();
            if (AppBase.Current.MainWindow is MainWindow mainWindow)
            {
                lock (_mainWindowLock)
                {
                    mainWindow.AcquireTopmostLock(this);
                }
            }
        });
    }

    private void HideMessage()
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsMessageVisible = false;
            _hideTimer.Stop();
            if (AppBase.Current.MainWindow is MainWindow mainWindow)
            {
                lock (_mainWindowLock)
                {
                    mainWindow.ReleaseTopmostLock(this);
                }
            }
        });
    }

    private void SettingsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Settings.IsQqNotificationMirrorEnabled))
        {
            _ = RestartAsync();
        }
    }

    public void Dispose()
    {
        _settingsService.Settings.PropertyChanged -= SettingsOnPropertyChanged;
        _pollTimer.Stop();
        _hideTimer.Stop();
        HideMessage();
    }
}
