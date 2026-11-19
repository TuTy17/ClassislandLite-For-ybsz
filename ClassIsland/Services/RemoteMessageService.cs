using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Models;
using ClassIsland.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services;

/// <summary>
/// 轮询云端消息服务器，把收到的提醒以全屏大字 + 每 5 秒一次的提示音显示出来。
/// </summary>
public sealed class RemoteMessageService : ObservableRecipient, IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly IAudioService _audioService;
    private readonly ILogger<RemoteMessageService> _logger;
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(12) };
    private readonly Queue<RemoteMessageItem> _queue = [];
    private DispatcherTimer? _timer;
    private RemoteMessageWindow? _window;
    private bool _isPolling;
    private bool _initialized;
    private string _statusText = "未启用";

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsDisplaying => _window != null;

    public RemoteMessageService(
        SettingsService settingsService,
        IAudioService audioService,
        ILogger<RemoteMessageService> logger)
    {
        _settingsService = settingsService;
        _audioService = audioService;
        _logger = logger;
        _settingsService.Settings.PropertyChanged += SettingsOnPropertyChanged;
    }

    public void Initialize()
    {
        _initialized = true;
        RestartTimer();
    }

    /// <summary>
    /// 让设置页可以手动测试连接。
    /// </summary>
    public async Task<string> TestConnectionAsync()
    {
        try
        {
            var result = await PollAsync();
            return result.HasValue
                ? $"连接成功，服务器最新消息编号 {result.Value.LatestId}。"
                : "连接失败，请检查服务器地址与访问密钥。";
        }
        catch (Exception exception)
        {
            return "连接失败：" + exception.Message;
        }
    }

    /// <summary>
    /// 立即显示一条消息（用于预览/测试，不经过服务器）。
    /// </summary>
    public void ShowLocalPreview(string text)
    {
        Enqueue(new RemoteMessageItem { Id = 0, Text = text, CreatedAt = DateTimeOffset.Now });
    }

    private void SettingsOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Settings.IsRemoteMessageEnabled)
            or nameof(Settings.RemoteMessageServerUrl)
            or nameof(Settings.RemoteMessagePollSeconds))
        {
            RestartTimer();
        }
    }

    private void RestartTimer()
    {
        _timer?.Stop();
        if (!_initialized)
        {
            return;
        }

        var settings = _settingsService.Settings;
        if (!settings.IsRemoteMessageEnabled ||
            string.IsNullOrWhiteSpace(settings.RemoteMessageServerUrl))
        {
            StatusText = "未启用";
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Clamp(settings.RemoteMessagePollSeconds, 2, 120));
        _timer = new DispatcherTimer { Interval = interval };
        _timer.Tick += async (_, _) => await TickAsync();
        _timer.Start();
        StatusText = $"正在监听 {settings.RemoteMessageServerUrl}";
        _ = TickAsync();
    }

    private async Task TickAsync()
    {
        if (_isPolling)
        {
            return;
        }

        _isPolling = true;
        try
        {
            var result = await PollAsync();
            if (result == null)
            {
                return;
            }

            var settings = _settingsService.Settings;
            if (!settings.RemoteMessageSynced)
            {
                // 首次连接时只记录当前位置，避免把历史消息一次性全部弹出。
                settings.RemoteMessageLastId = result.Value.LatestId;
                settings.RemoteMessageSynced = true;
                StatusText = $"已连接，等待新消息（最新 {result.Value.LatestId}）";
                return;
            }

            foreach (var message in result.Value.Messages)
            {
                Enqueue(message);
            }

            if (result.Value.LatestId > settings.RemoteMessageLastId)
            {
                settings.RemoteMessageLastId = result.Value.LatestId;
            }

            StatusText = $"运行中 · 最后同步 {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "轮询远程提醒消息失败。");
            StatusText = "同步失败：" + exception.Message;
        }
        finally
        {
            _isPolling = false;
        }
    }

    private async Task<(List<RemoteMessageItem> Messages, long LatestId)?> PollAsync()
    {
        var settings = _settingsService.Settings;
        var baseUrl = settings.RemoteMessageServerUrl?.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        var after = settings.RemoteMessageLastId;
        // 广播模式：所有设备拉取同一批消息，各自用 lastId 去重。
        var url = $"{baseUrl}/api/messages?after={after}";
        using var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"服务器返回 {(int)response.StatusCode}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;

        var messages = new List<RemoteMessageItem>();
        if (root.TryGetProperty("messages", out var array))
        {
            foreach (var element in array.EnumerateArray())
            {
                messages.Add(new RemoteMessageItem
                {
                    Id = element.TryGetProperty("id", out var id) ? id.GetInt64() : 0,
                    Text = element.TryGetProperty("text", out var text) ? text.GetString() ?? "" : "",
                    CreatedAt = element.TryGetProperty("ts", out var ts)
                        ? DateTimeOffset.FromUnixTimeSeconds(ts.GetInt64())
                        : DateTimeOffset.Now
                });
            }
        }

        var latestId = root.TryGetProperty("latestId", out var latest) ? latest.GetInt64() : after;
        return (messages, latestId);
    }

    private void Enqueue(RemoteMessageItem message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _queue.Enqueue(message);
            TryShowNext();
        });
    }

    private void TryShowNext()
    {
        if (_window != null || _queue.Count == 0)
        {
            return;
        }

        var message = _queue.Dequeue();
        var settings = _settingsService.Settings;
        var window = new RemoteMessageWindow(_audioService, settings.RemoteMessageSoundVolume, settings.RemoteMessageTitle);
        window.ShowMessage(message);
        window.Dismissed += (_, _) =>
        {
            _window = null;
            TryShowNext();
        };
        _window = window;
        window.Show();
        window.Activate();
    }

    public void Dispose()
    {
        _settingsService.Settings.PropertyChanged -= SettingsOnPropertyChanged;
        _timer?.Stop();
        _httpClient.Dispose();
    }
}
