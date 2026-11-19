using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Helpers;
using ClassIsland.Models;

namespace ClassIsland.Views;

/// <summary>
/// 远程提醒的全屏显示窗口：大字号显示消息，并每 5 秒播放一次提醒音。
/// </summary>
public partial class RemoteMessageWindow : Window
{
    private readonly IAudioService _audioService;
    private readonly double _volume;
    private readonly DispatcherTimer _beepTimer;

    public event EventHandler? Dismissed;

    public RemoteMessageWindow(IAudioService audioService, double volume, string caption)
    {
        _audioService = audioService;
        _volume = Math.Clamp(volume, 0, 1);
        InitializeComponent();

        // 标题文字由用户在应用设置里自定义，留空时整行隐藏。
        CaptionText.Text = caption ?? "";
        CaptionText.IsVisible = !string.IsNullOrWhiteSpace(caption);

        _beepTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _beepTimer.Tick += (_, _) => PlayBeep();

        Root.PointerPressed += (_, _) => Close();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };
        Opened += (_, _) =>
        {
            PlayBeep();
            _beepTimer.Start();
        };
        Closed += (_, _) =>
        {
            _beepTimer.Stop();
            Dismissed?.Invoke(this, EventArgs.Empty);
        };
    }

    public void ShowMessage(RemoteMessageItem item)
    {
        MessageText.Text = item.Text;
        MessageText.FontSize = ChooseFontSize(item.Text.Length);
    }

    private static double ChooseFontSize(int length) => length switch
    {
        <= 8 => 128,
        <= 16 => 108,
        <= 28 => 88,
        <= 48 => 72,
        <= 80 => 56,
        <= 140 => 44,
        _ => 34
    };

    private async void PlayBeep()
    {
        try
        {
            await _audioService.PlayAudioAsync(ReminderTone.CreateStream(), (float)_volume);
        }
        catch
        {
            // 没有可用音频设备时静默忽略，提醒照常显示。
        }
    }
}
