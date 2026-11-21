using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
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
    private readonly HttpClient _httpClient = CreateHttpClient();
    private readonly Queue<RemoteMessageItem> _queue = [];
    private DispatcherTimer? _timer;
    private RemoteMessageWindow? _window;
    private bool _isPolling;
    private bool _initialized;
    private string? _activeBaseUrl;
    private DateTime _lastSuccessAt;
    private static volatile string? _lastEdgeIp;
    private string _statusText = "未启用";

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsDisplaying => _window != null;

    /// <summary>
    /// 最近一次成功连接所用到的 Cloudflare 边缘 IP，便于排查国内线路差异。
    /// </summary>
    public static string? LastEdgeIp => _lastEdgeIp;


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
        var urls = ParseServerUrls(settings.RemoteMessageServerUrl);
        StatusText = urls.Length > 1 ? $"正在监听 {urls.Length} 个地址" : $"正在监听 {urls.FirstOrDefault()}";
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

            var host = Uri.TryCreate(_activeBaseUrl, UriKind.Absolute, out var statusUri) ? statusUri.Host : _activeBaseUrl;
            StatusText = LastEdgeIp is { } edge
                ? $"运行中 · {host} · 边缘 {edge} · 最后同步 {_lastSuccessAt:HH:mm:ss}"
                : $"运行中 · {host} · 最后同步 {_lastSuccessAt:HH:mm:ss}";
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "轮询远程提醒消息失败。");
            StatusText = "同步失败，稍后自动重试：" + exception.Message;
        }
        finally
        {
            _isPolling = false;
        }
    }

    /// <summary>
    /// 服务器地址支持用逗号、分号或换行分隔多个，按顺序尝试，并记住可用的那个。
    /// 国内访问 Cloudflare 时线路时好时坏，多地址能显著提升到达率。
    /// </summary>
    public static string[] ParseServerUrls(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw.Split([',', ';', '\n', '\r', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(url => url.Trim().TrimEnd('/'))
            .Where(url => url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                          url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<(List<RemoteMessageItem> Messages, long LatestId)?> PollAsync()
    {
        var settings = _settingsService.Settings;
        var urls = ParseServerUrls(settings.RemoteMessageServerUrl);
        if (urls.Length == 0)
        {
            return null;
        }

        var ordered = _activeBaseUrl is not null && urls.Contains(_activeBaseUrl, StringComparer.OrdinalIgnoreCase)
            ? new[] { _activeBaseUrl }.Concat(urls.Where(u => !u.Equals(_activeBaseUrl, StringComparison.OrdinalIgnoreCase)))
            : urls;

        Exception? lastError = null;
        foreach (var baseUrl in ordered)
        {
            try
            {
                var result = await PollOnceAsync(baseUrl, settings.RemoteMessageLastId);
                _activeBaseUrl = baseUrl;
                _lastSuccessAt = DateTime.Now;
                return result;
            }
            catch (Exception exception)
            {
                lastError = exception;
            }
        }

        throw lastError ?? new InvalidOperationException("所有服务器地址都连接失败");
    }

    private async Task<(List<RemoteMessageItem> Messages, long LatestId)?> PollOnceAsync(string baseUrl, long after)
    {
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

    private static readonly TimeSpan DnsCacheLifetime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// 国内访问 Cloudflare 时不同边缘 IP 的可用性差异极大，这里预置一批常见边缘 IP，
    /// 与 DNS 解析结果一起并发尝试，谁先连上就用谁（等价于“优选 IP”）。
    /// </summary>
    private static readonly string[] EdgeIpSeeds =
    [
        "104.20.0.1", "104.19.192.1", "104.16.132.229", "104.18.32.1",
        "104.21.73.247", "172.67.151.122", "172.64.0.1", "162.159.0.1"
    ];

    private static IPAddress? _preferredEdgeAddress;
    private static DateTime _preferredEdgeExpiresAt;
    private static readonly Dictionary<string, (IPAddress[] Addresses, DateTime ExpiresAt)> DnsCache = new();
    private static readonly object DnsCacheLock = new();

    /// <summary>
    /// 建立远程提醒用的 HttpClient。系统 DNS 解析不到域名时（新域名、运营商缓存、
    /// 校园网 DNS 过滤等），自动改用 DNS over HTTPS 解析后再按 IP 连接，TLS 仍然使用原域名。
    /// </summary>
    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(15),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectCallback = async (context, cancellationToken) =>
            {
                var host = context.DnsEndPoint.Host;
                var addresses = IPAddress.TryParse(host, out var literal)
                    ? [literal]
                    : await ResolveAddressesAsync(host, cancellationToken).ConfigureAwait(false);
                addresses = ExpandCandidates(addresses);

                // 多个地址并发连接，谁先连上就用谁：国内访问 Cloudflare 时不同边缘 IP
                // 的可用性差异很大，串行尝试会让一次轮询卡满超时。
                var sockets = new List<Socket>();
                var attempts = new List<Task<Socket>>();
                try
                {
                    foreach (var address in addresses)
                    {
                        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                        {
                            NoDelay = true
                        };
                        sockets.Add(socket);
                        attempts.Add(ConnectAsync(socket, address, context.DnsEndPoint.Port, cancellationToken));
                    }

                    while (attempts.Count > 0)
                    {
                        var finished = await Task.WhenAny(attempts).ConfigureAwait(false);
                        attempts.Remove(finished);
                        Socket? connected = null;
                        try
                        {
                            connected = await finished.ConfigureAwait(false);
                        }
                        catch (Exception)
                        {
                            // 这个地址连不上，继续等其他地址
                        }

                        if (connected != null)
                        {
                            sockets.Remove(connected);
                            RememberPreferredEdge(connected.RemoteEndPoint as IPEndPoint);
                            _lastEdgeIp = (connected.RemoteEndPoint as IPEndPoint)?.Address.ToString();
                            return new NetworkStream(connected, ownsSocket: true);
                        }
                    }
                }
                finally
                {
                    foreach (var socket in sockets)
                    {
                        socket.Dispose();
                    }
                }

                throw new SocketException((int)SocketError.HostUnreachable);
            }
        };

        return new HttpClient(handler, disposeHandler: true) { Timeout = TimeSpan.FromSeconds(30) };
    }

    private static IPAddress[] ExpandCandidates(IPAddress[] resolved)
    {
        var candidates = new List<IPAddress>();
        lock (DnsCacheLock)
        {
            if (_preferredEdgeAddress != null && _preferredEdgeExpiresAt > DateTime.UtcNow)
            {
                candidates.Add(_preferredEdgeAddress);
            }
        }

        foreach (var address in resolved)
        {
            if (!candidates.Contains(address))
            {
                candidates.Add(address);
            }
        }

        foreach (var seed in EdgeIpSeeds)
        {
            if (IPAddress.TryParse(seed, out var address) && !candidates.Contains(address))
            {
                candidates.Add(address);
            }
        }

        return candidates.ToArray();
    }

    private static void RememberPreferredEdge(IPEndPoint? endpoint)
    {
        if (endpoint?.Address is not { } address)
        {
            return;
        }

        lock (DnsCacheLock)
        {
            _preferredEdgeAddress = address;
            _preferredEdgeExpiresAt = DateTime.UtcNow.AddMinutes(15);
        }
    }

    private static async Task<Socket> ConnectAsync(Socket socket, IPAddress address, int port,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(6));
        await socket.ConnectAsync(new IPEndPoint(address, port), timeout.Token).ConfigureAwait(false);
        return socket;
    }

    private static async Task<IPAddress[]> ResolveAddressesAsync(string host, CancellationToken cancellationToken)
    {
        lock (DnsCacheLock)
        {
            if (DnsCache.TryGetValue(host, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
            {
                return cached.Addresses;
            }
        }

        IPAddress[]? addresses = null;
        try
        {
            var systemAddresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
            addresses = systemAddresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork).ToArray();
            if (addresses.Length == 0)
            {
                addresses = systemAddresses;
            }
        }
        catch (Exception)
        {
            // 系统 DNS 失败时继续用 DoH
        }

        if (addresses == null || addresses.Length == 0)
        {
            addresses = await ResolveByDohAsync(host, cancellationToken).ConfigureAwait(false);
        }

        if (addresses.Length > 0)
        {
            lock (DnsCacheLock)
            {
                DnsCache[host] = (addresses, DateTime.UtcNow.Add(DnsCacheLifetime));
            }
        }

        return addresses;
    }

    private static async Task<IPAddress[]> ResolveByDohAsync(string host, CancellationToken cancellationToken)
    {
        string[] endpoints =
        [
            "https://dns.alidns.com/resolve?name={0}&type=A",
            "https://doh.pub/dns-query?name={0}&type=A",
            "https://cloudflare-dns.com/dns-query?name={0}&type=A"
        ];

        using var dohClient = new HttpClient(new SocketsHttpHandler { UseProxy = false })
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        foreach (var endpoint in endpoints)
        {
            try
            {
                var url = string.Format(endpoint, Uri.EscapeDataString(host));
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("accept", "application/dns-json");
                using var response = await dohClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (!document.RootElement.TryGetProperty("Answer", out var answers) ||
                    answers.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var addresses = new List<IPAddress>();
                foreach (var answer in answers.EnumerateArray())
                {
                    if (answer.TryGetProperty("type", out var type) && type.GetInt32() == 1 &&
                        answer.TryGetProperty("data", out var data) &&
                        IPAddress.TryParse(data.GetString(), out var address))
                    {
                        addresses.Add(address);
                    }
                }

                if (addresses.Count > 0)
                {
                    return addresses.ToArray();
                }
            }
            catch (Exception)
            {
                // 换下一个 DoH 服务
            }
        }

        return [];
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
