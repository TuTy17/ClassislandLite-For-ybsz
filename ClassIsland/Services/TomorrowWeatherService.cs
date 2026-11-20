using System;
using ClassIsland.Core.Abstractions.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.Services;

/// <summary>
/// 共享“是否正在显示次日课表”以及当日/次日天气的状态，
/// 供“天气简报”小组件（第一行）和“次日天气”小组件（第二行）联动。
/// </summary>
public sealed class TomorrowWeatherService : ObservableRecipient
{
    private readonly SettingsService _settingsService;
    private readonly IWeatherService _weatherService;
    private bool _isTomorrowViewActive;
    private bool _hasTomorrowWeather;
    private bool _isTomorrowRainy;
    private string _tomorrowWeatherText = "";
    private string _tomorrowWeatherCode = "0";
    private string _tomorrowRainHint = "";
    private bool _hasCurrentWeather;
    private string _currentWeatherText = "";

    public bool IsTomorrowViewActive
    {
        get => _isTomorrowViewActive;
        private set => SetProperty(ref _isTomorrowViewActive, value);
    }

    public bool HasTomorrowWeather
    {
        get => _hasTomorrowWeather;
        private set => SetProperty(ref _hasTomorrowWeather, value);
    }

    public bool IsTomorrowRainy
    {
        get => _isTomorrowRainy;
        private set => SetProperty(ref _isTomorrowRainy, value);
    }

    public string TomorrowWeatherText
    {
        get => _tomorrowWeatherText;
        private set => SetProperty(ref _tomorrowWeatherText, value);
    }

    public string TomorrowWeatherCode
    {
        get => _tomorrowWeatherCode;
        private set => SetProperty(ref _tomorrowWeatherCode, value);
    }

    public string TomorrowRainHint
    {
        get => _tomorrowRainHint;
        private set => SetProperty(ref _tomorrowRainHint, value);
    }

    public bool HasCurrentWeather
    {
        get => _hasCurrentWeather;
        private set => SetProperty(ref _hasCurrentWeather, value);
    }

    public string CurrentWeatherText
    {
        get => _currentWeatherText;
        private set => SetProperty(ref _currentWeatherText, value);
    }

    public TomorrowWeatherService(SettingsService settingsService, IWeatherService weatherService)
    {
        _settingsService = settingsService;
        _weatherService = weatherService;
        _settingsService.Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SettingsService.Settings.LastWeatherInfo))
            {
                Refresh();
            }
        };
        Refresh();
    }

    /// <summary>
    /// 由课程表组件在每次刷新时调用，标记当前是否处于“显示次日课表”的状态。
    /// </summary>
    public void SetViewActive(bool active)
    {
        IsTomorrowViewActive = active;
    }

    /// <summary>
    /// 从天气服务里重新计算当日与次日天气。
    /// </summary>
    public void Refresh()
    {
        try
        {
            var info = _weatherService?.LastWeatherInfo;
            var current = info?.Current;
            if (current != null && !string.IsNullOrWhiteSpace(current.Weather) && current.Weather != "99")
            {
                var text = _weatherService!.GetWeatherTextByCode(current.Weather);
                var temperature = current.Temperature?.Value;
                CurrentWeatherText = string.IsNullOrWhiteSpace(temperature) ? text : $"{text} {temperature}℃";
                HasCurrentWeather = true;
            }
            else
            {
                HasCurrentWeather = false;
            }

            var daily = info?.ForecastDaily;
            var temperatures = daily?.Temperature?.Value;
            var weathers = daily?.Weather?.Value;
            if (temperatures == null || weathers == null || temperatures.Count == 0 || weathers.Count == 0)
            {
                HasTomorrowWeather = false;
                IsTomorrowRainy = false;
                return;
            }

            var tomorrow = DateTime.Today.AddDays(1);
            var index = -1;
            var sunSets = daily?.SunRiseSet?.Value;
            if (sunSets != null)
            {
                for (var i = 0; i < sunSets.Count; i++)
                {
                    if (DateTimeOffset.TryParse(sunSets[i].From, out var date) && date.Date == tomorrow)
                    {
                        index = i;
                        break;
                    }
                }
            }

            if (index < 0 || index >= temperatures.Count || index >= weathers.Count)
            {
                index = Math.Min(1, Math.Min(temperatures.Count, weathers.Count) - 1);
            }

            if (index < 0)
            {
                HasTomorrowWeather = false;
                IsTomorrowRainy = false;
                return;
            }

            var temperatureRange = temperatures[index];
            var weatherRange = weathers[index];
            var textFrom = _weatherService!.GetWeatherTextByCode(weatherRange.From);
            var textTo = _weatherService.GetWeatherTextByCode(weatherRange.To);
            var weatherText = textFrom == textTo ? textFrom : $"{textFrom}转{textTo}";
            var low = int.TryParse(temperatureRange.To, out var lowValue) ? lowValue : (int?)null;
            var high = int.TryParse(temperatureRange.From, out var highValue) ? highValue : (int?)null;

            TomorrowWeatherText = low.HasValue && high.HasValue
                ? $"{weatherText} {low}~{high}℃"
                : weatherText;
            TomorrowWeatherCode = string.IsNullOrWhiteSpace(weatherRange.From) ? "0" : weatherRange.From;
            HasTomorrowWeather = !string.IsNullOrWhiteSpace(weatherText);
            IsTomorrowRainy = weatherText.Contains('雨') || weatherText.Contains('雪') || weatherText.Contains('雷');
            TomorrowRainHint = weatherText.Contains('雪') ? "明日有雪"
                : weatherText.Contains('雷') ? "明日有雷"
                : "明日有雨";
        }
        catch
        {
            HasTomorrowWeather = false;
            IsTomorrowRainy = false;
        }
    }
}
