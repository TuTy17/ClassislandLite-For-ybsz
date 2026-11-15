using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Enums;
using SoundFlow.Structs;

namespace ClassIsland.Services;

public sealed class DecibelService : ObservableRecipient, IDisposable
{
    private readonly IAudioService _audioService;
    private readonly SettingsService _settingsService;
    private readonly ILogger<DecibelService> _logger;
    private readonly SemaphoreSlim _restartLock = new(1, 1);
    private readonly object _sampleLock = new();
    private AudioCaptureDevice? _captureDevice;
    private double _smoothedDecibel;
    private double _peakDecibel;
    private long _lastUiUpdateTicks;
    private double _currentDecibel;
    private double _peakDisplayDecibel;
    private bool _isMonitoring;
    private string _statusText = "分贝检测未启动";

    public double CurrentDecibel { get => _currentDecibel; private set => SetProperty(ref _currentDecibel, value); }
    public double PeakDecibel { get => _peakDisplayDecibel; private set => SetProperty(ref _peakDisplayDecibel, value); }
    public bool IsMonitoring { get => _isMonitoring; private set => SetProperty(ref _isMonitoring, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

    public DecibelService(
        IAudioService audioService,
        SettingsService settingsService,
        ILogger<DecibelService> logger)
    {
        _audioService = audioService;
        _settingsService = settingsService;
        _logger = logger;
        _settingsService.Settings.PropertyChanged += SettingsOnPropertyChanged;
    }

    public async Task<IReadOnlyList<string>> GetCaptureDevicesAsync()
    {
        try
        {
            return await Task.Run(() =>
            {
                _audioService.AudioEngine.UpdateDevicesInfo();
                return _audioService.AudioEngine.CaptureDevices
                    .Select(device => device.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                    .ToArray();
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "无法枚举麦克风设备。");
            StatusText = "无法读取麦克风：" + exception.Message;
            return [];
        }
    }

    public async Task StartAsync()
    {
        await _restartLock.WaitAsync();
        try
        {
            if (_captureDevice?.IsRunning == true)
            {
                IsMonitoring = true;
                return;
            }

            await StopCoreAsync();
            string? selectedDeviceName = null;
            await Task.Run(() =>
            {
                _audioService.AudioEngine.UpdateDevicesInfo();
                var devices = _audioService.AudioEngine.CaptureDevices;
                var selectedName = _settingsService.Settings.DecibelInputDeviceName;
                var deviceInfo = devices.FirstOrDefault(device =>
                                      string.Equals(device.Name, selectedName, StringComparison.OrdinalIgnoreCase));
                if (deviceInfo == default)
                {
                    deviceInfo = devices.FirstOrDefault(device => device.IsDefault);
                }
                if (deviceInfo == default)
                {
                    throw new InvalidOperationException("没有可用的麦克风设备。");
                }

                var captureDevice = _audioService.AudioEngine.InitializeCaptureDevice(
                    deviceInfo,
                    IAudioService.DefaultAudioFormat,
                    null!);
                captureDevice.OnAudioProcessed += OnAudioProcessed;
                captureDevice.Start();
                _captureDevice = captureDevice;
                selectedDeviceName = deviceInfo.Name;
            });
            if (!string.IsNullOrWhiteSpace(selectedDeviceName))
            {
                _settingsService.Settings.DecibelInputDeviceName = selectedDeviceName;
            }
            StatusText = $"正在监测：{_settingsService.Settings.DecibelInputDeviceName}";
            IsMonitoring = true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "启动分贝检测失败。");
            StatusText = "启动失败：" + exception.Message;
            IsMonitoring = false;
        }
        finally
        {
            _restartLock.Release();
        }
    }

    public async Task StopAsync()
    {
        await _restartLock.WaitAsync();
        try
        {
            await StopCoreAsync();
            StatusText = "分贝检测已停止";
            IsMonitoring = false;
        }
        finally
        {
            _restartLock.Release();
        }
    }

    public async Task RestartAsync()
    {
        if (_settingsService.Settings.IsDecibelMonitorEnabled)
        {
            await StartAsync();
        }
        else
        {
            await StopAsync();
        }
    }

    private async Task StopCoreAsync()
    {
        var device = _captureDevice;
        _captureDevice = null;
        if (device == null)
        {
            return;
        }

        await Task.Run(() =>
        {
            device.OnAudioProcessed -= OnAudioProcessed;
            device.Stop();
            device.Dispose();
        });
    }

    private void OnAudioProcessed(Span<float> samples, Capability capability)
    {
        if (samples.Length == 0)
        {
            return;
        }

        double sum = 0;
        for (var index = 0; index < samples.Length; index++)
        {
            var sample = samples[index];
            sum += sample * sample;
        }

        var rms = Math.Sqrt(sum / samples.Length);
        var instantDecibel = 100 + 20 * Math.Log10(Math.Max(rms, 0.000001)) +
                             _settingsService.Settings.DecibelCalibrationOffset;
        instantDecibel = Math.Clamp(instantDecibel, 0, 130);

        lock (_sampleLock)
        {
            _smoothedDecibel = _smoothedDecibel == 0
                ? instantDecibel
                : _smoothedDecibel * 0.82 + instantDecibel * 0.18;
            _peakDecibel = Math.Max(_peakDecibel * 0.997, _smoothedDecibel);
        }

        var now = Environment.TickCount64;
        if (now - Interlocked.Read(ref _lastUiUpdateTicks) < 100)
        {
            return;
        }
        Interlocked.Exchange(ref _lastUiUpdateTicks, now);

        double current;
        double peak;
        lock (_sampleLock)
        {
            current = _smoothedDecibel;
            peak = _peakDecibel;
        }
        Dispatcher.UIThread.Post(() =>
        {
            CurrentDecibel = current;
            PeakDecibel = peak;
        });
    }

    private void SettingsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Settings.IsDecibelMonitorEnabled) or nameof(Settings.DecibelInputDeviceName))
        {
            _ = RestartAsync();
        }
    }

    public void Dispose()
    {
        _settingsService.Settings.PropertyChanged -= SettingsOnPropertyChanged;
        if (_captureDevice != null)
        {
            _captureDevice.OnAudioProcessed -= OnAudioProcessed;
            _captureDevice.Stop();
            _captureDevice.Dispose();
        }
        _captureDevice = null;
        _restartLock.Dispose();
    }
}
