using System;
using Avalonia;
using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Models.ComponentSettings;
using ClassIsland.Services;
using ReactiveUI;

namespace ClassIsland.Controls.Components;

/// <summary>
/// WeatherComponent.xaml 的交互逻辑
/// </summary>
[ComponentInfo("CA495086-E297-4BEB-9603-C5C1C1A8551E", "天气简报", "\uf465", "显示当前的天气概况和气象预警。")]
public partial class WeatherComponent : ComponentBase<WeatherComponentSettings>
{
    public IWeatherService WeatherService { get; }

    public SettingsService SettingsService { get; }

    public TomorrowWeatherService TomorrowWeatherService { get; }

    private int _aqiLevel;

    public static readonly DirectProperty<WeatherComponent, int> AqiLevelProperty = AvaloniaProperty.RegisterDirect<WeatherComponent, int>(
        nameof(AqiLevel), o => o.AqiLevel, (o, v) => o.AqiLevel = v);

    public int AqiLevel
    {
        get => _aqiLevel;
        set => SetAndRaise(AqiLevelProperty, ref _aqiLevel, value);
    }

    private IDisposable? _observer;

    public WeatherComponent(IWeatherService weatherService, SettingsService settingsService, TomorrowWeatherService tomorrowWeatherService)
    {
        WeatherService = weatherService;
        SettingsService = settingsService;
        TomorrowWeatherService = tomorrowWeatherService;
        InitializeComponent();
    }

    private void TomorrowWeatherStateOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        UpdateWeatherVisibility();
    }

    /// <summary>
    /// 显示次日课表时，第一行的当日天气让位给第二行的次日天气。
    /// </summary>
    private void UpdateWeatherVisibility()
    {
        IsVisible = !TomorrowWeatherService.IsTomorrowViewActive;
    }

    private void UpdateAqiInfo()
    {
        AqiLevel = SettingsService.Settings.LastWeatherInfo.Aqi.AqiLevel;
    }


    private void Control_OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs visualTreeAttachmentEventArgs)
    {
        _observer?.Dispose();
        _observer = SettingsService.Settings.ObservableForProperty(x => x.LastWeatherInfo)
            .Subscribe(_ => UpdateAqiInfo());
        UpdateAqiInfo();
        TomorrowWeatherService.PropertyChanged += TomorrowWeatherStateOnPropertyChanged;
        UpdateWeatherVisibility();
    }

    private void Control_OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs visualTreeAttachmentEventArgs)
    {
        _observer?.Dispose();
        TomorrowWeatherService.PropertyChanged -= TomorrowWeatherStateOnPropertyChanged;
    }
}

