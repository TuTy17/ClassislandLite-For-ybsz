using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Models;
using ClassIsland.Services;
using ClassIsland.Core.Models.AttachedSettings;
using ClassIsland.Models.ComponentSettings;
using ClassIsland.Shared.Abstraction.Models;
using ClassIsland.Shared.Enums;
using ClassIsland.Shared.Models.Profile;
using ClassIsland.Shared.ComponentModels;
using ReactiveUI;

namespace ClassIsland.Controls.Components;

/// <summary>
/// ScheduleComponent.xaml 的交互逻辑
/// </summary>
[MigrateFrom("E7831603-61A0-4180-B51B-54AD75B1A4D3")]  // 课程表（旧）
[ComponentInfo("1DB2017D-E374-4BC6-9D57-0B4ADF03A6B8", "课程表", "\ue751", "显示当前的课程表信息。")]
[PseudoClasses(":show-tomorrow-schedule", ":show-tomorrow-schedule-after-school", ":show-tomorrow-schedule-on-empty", ":show-tomorrow-schedule-always", ":show-tomorrow-schedule-at-last-class")]
public partial class ScheduleComponent : ComponentBase<LessonControlSettings>, INotifyPropertyChanged
{
    private bool _hideFinishedClass;

    public static readonly DirectProperty<ScheduleComponent, bool> HideFinishedClassProperty = AvaloniaProperty.RegisterDirect<ScheduleComponent, bool>(
        nameof(HideFinishedClass), o => o.HideFinishedClass, (o, v) => o.HideFinishedClass = v);

    public bool HideFinishedClass
    {
        get => _hideFinishedClass;
        set => SetAndRaise(HideFinishedClassProperty, ref _hideFinishedClass, value);
    }

    private bool _showEmptyPlaceholder;

    public static readonly DirectProperty<ScheduleComponent, bool> ShowEmptyPlaceholderProperty = AvaloniaProperty.RegisterDirect<ScheduleComponent, bool>(
        nameof(ShowEmptyPlaceholder), o => o.ShowEmptyPlaceholder, (o, v) => o.ShowEmptyPlaceholder = v);

    public bool ShowEmptyPlaceholder
    {
        get => _showEmptyPlaceholder;
        set => SetAndRaise(ShowEmptyPlaceholderProperty, ref _showEmptyPlaceholder, value);
    }
    
    private bool _showCurrentLessonOnlyOnClass = false;
    private bool _showCurrentLessonOnlyFromSettings;
    private bool _isLastLessonPeriod;
    private bool _isAfterSchool = false;
    private ClassPlan? _tomorrowClassPlan;
    private ClassPlan? _tomorrowClassPlan1;
    public ILessonsService LessonsService { get; }

    public SettingsService SettingsService { get; }

    public IProfileService ProfileService { get; }
    public IExactTimeService ExactTimeService { get; }

    public IWeatherService WeatherService { get; }

    public TomorrowWeatherService TomorrowWeatherService { get; }

    public static readonly StyledProperty<int> LessonsListBoxSelectedIndexProperty = AvaloniaProperty.Register<ScheduleComponent, int>(
        nameof(LessonsListBoxSelectedIndex));

    private IDisposable? _tomorrowScheduleShowModeObserver;
    private IDisposable? _hideFinishedClassObserver;
    private IDisposable? _showEmptyPlaceholderObserver;
    private IDisposable? _carnivalModeObserver;
    private readonly Avalonia.Threading.DispatcherTimer _tomorrowRefreshTimer = new()
    {
        Interval = TimeSpan.FromSeconds(1)
    };
    private string _tomorrowWeatherText = "";
    private string _tomorrowRainHint = "";
    private bool _hasTomorrowWeather;
    private bool _isTomorrowRainy;
    private ObservableDictionary<Guid, Subject> _displaySubjects = new();

    private static readonly Subject CarnivalSubject = new()
    {
        Name = "语文",
        Initial = "语"
    };

    public ObservableDictionary<Guid, Subject> DisplaySubjects
    {
        get => _displaySubjects;
        private set => SetField(ref _displaySubjects, value);
    }
    public int LessonsListBoxSelectedIndex
    {
        get => GetValue(LessonsListBoxSelectedIndexProperty);
        set => SetValue(LessonsListBoxSelectedIndexProperty, value);
    }

    public bool IsAfterSchool
    {
        get => _isAfterSchool;
        set
        {
            if (value == _isAfterSchool) return;
            _isAfterSchool = value;
            OnPropertyChanged();
        }
    }

    public bool ShowCurrentLessonOnlyOnClass
    {
        get => _showCurrentLessonOnlyOnClass;
        set
        {
            if (value == _showCurrentLessonOnlyOnClass) return;
            _showCurrentLessonOnlyOnClass = value;
            OnPropertyChanged();
        }
    }

    public ClassPlan? TomorrowClassPlan
    {
        get => _tomorrowClassPlan1;
        set
        {
            if (Equals(value, _tomorrowClassPlan1)) return;
            _tomorrowClassPlan1 = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 是否取到了明天的天气（有数据才显示在“明天”标签后面）。
    /// </summary>
    public bool HasTomorrowWeather
    {
        get => _hasTomorrowWeather;
        private set
        {
            if (value == _hasTomorrowWeather) return;
            _hasTomorrowWeather = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 明天天气的简要文字，例如“多云转小雨 8~15℃”。
    /// </summary>
    public string TomorrowWeatherText
    {
        get => _tomorrowWeatherText;
        private set
        {
            if (value == _tomorrowWeatherText) return;
            _tomorrowWeatherText = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 明天是否有雨雪，需要在“明天”标签后常驻一个醒目提示。
    /// </summary>
    public bool IsTomorrowRainy
    {
        get => _isTomorrowRainy;
        private set
        {
            if (value == _isTomorrowRainy) return;
            _isTomorrowRainy = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 雨雪提示文字，例如“明日有雨”。
    /// </summary>
    public string TomorrowRainHint
    {
        get => _tomorrowRainHint;
        private set
        {
            if (value == _tomorrowRainHint) return;
            _tomorrowRainHint = value;
            OnPropertyChanged();
        }
    }

    public ScheduleComponent(ILessonsService lessonsService, SettingsService settingsService, IProfileService profileService, IExactTimeService exactTimeService, IWeatherService weatherService, TomorrowWeatherService tomorrowWeatherService)
    {
        LessonsService = lessonsService;
        SettingsService = settingsService;
        ProfileService = profileService;
        ExactTimeService = exactTimeService;
        WeatherService = weatherService;
        TomorrowWeatherService = tomorrowWeatherService;
        
        AttachedToVisualTree += (_, _) => LessonsService.PostMainTimerTicked += LessonsServiceOnPostMainTimerTicked;
        AttachedToVisualTree += (_, _) => LessonsService.CurrentTimeStateChanged += OnLessonsServiceOnCurrentTimeStateChanged;
        AttachedToVisualTree += (_, _) => LessonsService.PropertyChanged += LessonsServiceOnPropertyChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        AttachedToVisualTree += (_, _) => _tomorrowRefreshTimer.Start();
        DetachedFromVisualTree += (_, _) => _tomorrowRefreshTimer.Stop();
        DetachedFromVisualTree += (_, _) => LessonsService.PostMainTimerTicked -= LessonsServiceOnPostMainTimerTicked;
        DetachedFromVisualTree += (_, _) => LessonsService.CurrentTimeStateChanged -= OnLessonsServiceOnCurrentTimeStateChanged;
        DetachedFromVisualTree += (_, _) => LessonsService.PropertyChanged -= LessonsServiceOnPropertyChanged;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        InitializeComponent();
        _tomorrowRefreshTimer.Tick += (_, _) => RefreshTomorrowState();
        RefreshDisplaySubjects();
        CurrentTimeStateChanged();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs visualTreeAttachmentEventArgs)
    {
        _tomorrowScheduleShowModeObserver?.Dispose();
        _hideFinishedClassObserver?.Dispose();
        _showEmptyPlaceholderObserver?.Dispose();
        
        _tomorrowScheduleShowModeObserver = null;
        _hideFinishedClassObserver = null;
        _showEmptyPlaceholderObserver = null;
        _carnivalModeObserver?.Dispose();
        _carnivalModeObserver = null;
        ProfileService.Profile.Subjects.CollectionChanged -= ProfileSubjectsOnCollectionChanged;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs visualTreeAttachmentEventArgs)
    {
        CurrentTimeStateChanged();
        CheckTomorrowClassShowMode();
        _tomorrowScheduleShowModeObserver ??= Settings
            .ObservableForProperty(x => x.TomorrowScheduleShowMode)
            .Subscribe(_ => CheckTomorrowClassShowMode());
        _hideFinishedClassObserver ??= Settings
            .ObservableForProperty(x => x.HideFinishedClass)
            .Subscribe(_ =>
            {
                HideFinishedClass = Settings.HideFinishedClass;
                UpdateTomorrowVisibility();
        UpdateTomorrowWeather();
            });
        _showEmptyPlaceholderObserver ??= Settings
            .ObservableForProperty(x => x.ShowPlaceholderOnEmptyClassPlan)
            .Subscribe(_ => ShowEmptyPlaceholder = Settings.ShowPlaceholderOnEmptyClassPlan);
        _carnivalModeObserver ??= SettingsService.Settings
            .ObservableForProperty(x => x.IsCarnivalModeEnabled)
            .Subscribe(_ => RefreshDisplaySubjects());
        ProfileService.Profile.Subjects.CollectionChanged -= ProfileSubjectsOnCollectionChanged;
        ProfileService.Profile.Subjects.CollectionChanged += ProfileSubjectsOnCollectionChanged;
        RefreshDisplaySubjects();

        HideFinishedClass = Settings.HideFinishedClass;
        ShowEmptyPlaceholder = Settings.ShowPlaceholderOnEmptyClassPlan;
        MainLessonsListBox.SelectedIndex = LessonsListBoxSelectedIndex;
        UpdateTomorrowVisibility();
    }

    /// <summary>
    /// 重新计算“明天的课表”。档案加载晚于组件创建时，TomorrowClassPlan 会一直是 null，
    /// 导致“最后一节课显示次日课表”永远不触发，所以定时器每次 tick 都校对一遍。
    /// </summary>
    /// <summary>
    /// 每秒校对一次“次日课表 / 最后一节课 / 次日天气”。
    /// 课表档案可能晚于组件创建才加载完成，只依赖一次性初始化会一直显示不出来。
    /// </summary>
    private void RefreshTomorrowState()
    {
        CurrentTimeStateChanged();
        RefreshTomorrowClassPlan();
        UpdateLastLessonState();
        UpdateTomorrowVisibility();
        UpdateTomorrowWeather();
    }

    private void RefreshTomorrowClassPlan()
    {
        var tomorrow = LessonsService.GetClassPlanByDate(ExactTimeService.GetCurrentLocalDateTime() + TimeSpan.FromDays(1));
        if (!Equals(TomorrowClassPlan, tomorrow))
        {
            TomorrowClassPlan = tomorrow;
        }
    }

    private void CheckTomorrowClassShowMode()
    {
        PseudoClasses.Set(":show-tomorrow-schedule", Settings.TomorrowScheduleShowMode is 1 or 2 or 3 or 4);
        PseudoClasses.Set(":show-tomorrow-schedule-after-school", Settings.TomorrowScheduleShowMode is 1 or 4);
        PseudoClasses.Set(":show-tomorrow-schedule-always", Settings.TomorrowScheduleShowMode is 2);
        RefreshTomorrowClassPlan();
        UpdateLastLessonState();
        UpdateTomorrowVisibility();
    }

    private void LessonsServiceOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LessonsService.CurrentClassPlan))
        {
            CurrentTimeStateChanged();
        }
    }

    private void OnLessonsServiceOnCurrentTimeStateChanged(object? o, EventArgs eventArgs)
    {
        CurrentTimeStateChanged();
        UpdateTomorrowVisibility();
    }

    private void CurrentTimeStateChanged()
    {
        IsAfterSchool =
            LessonsService.CurrentState == TimeState.AfterSchool ||
            LessonsService.CurrentClassPlan == null;
    }

    public override void OnMigrated(Guid sourceId, object? settings)
    {
        var appSettings = SettingsService.Settings;
        Settings.CountdownSeconds = appSettings.CountdownSeconds;
        Settings.ExtraInfoType = appSettings.ExtraInfoType;
        Settings.IsCountdownEnabled = appSettings.IsCountdownEnabled;
        Settings.ShowExtraInfoOnTimePoint = appSettings.ShowExtraInfoOnTimePoint;
        base.OnMigrated(sourceId, appSettings);
    }

    private void LessonsServiceOnPostMainTimerTicked(object? sender, EventArgs e)
    {
        RefreshTomorrowState();
        var settingsSource =
            (ILessonControlSettings?)IAttachedSettingsHostService.GetAttachedSettingsByPriority<LessonControlAttachedSettings>(
                new Guid("58e5b69a-764a-472b-bcf7-003b6a8c7fdf"),
                LessonsService.CurrentSubject,
                LessonsService.CurrentTimeLayoutItem,
                LessonsService.CurrentClassPlan,
                LessonsService.CurrentClassPlan?.TimeLayout) ??
            Settings;
        _showCurrentLessonOnlyFromSettings = settingsSource.ShowCurrentLessonOnlyOnClass;
        TomorrowClassPlan = LessonsService.GetClassPlanByDate(ExactTimeService.GetCurrentLocalDateTime() + TimeSpan.FromDays(1));
        UpdateLastLessonState();
        MainLessonsListBox.SelectedIndex = LessonsListBoxSelectedIndex;
        UpdateTomorrowVisibility();
    }

    private void ProfileSubjectsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshDisplaySubjects();
    }

    private void RefreshDisplaySubjects()
    {
        var subjects = new ObservableDictionary<Guid, Subject>();
        foreach (var (id, subject) in ProfileService.Profile.Subjects)
        {
            subjects[id] = SettingsService.Settings.IsCarnivalModeEnabled
                ? CarnivalSubject
                : subject;
        }

        DisplaySubjects = subjects;
    }

    private void UpdateLastLessonState()
    {
        var currentPlan = LessonsService.CurrentClassPlan;
        var items = currentPlan?.ValidTimeLayoutItems?
            .Where(item => item.TimeType == 0 && !item.IsHideDefault)
            .ToList() ?? [];
        var classes = currentPlan?.Classes?.ToList() ?? [];
        var now = ExactTimeService.GetCurrentLocalDateTime();

        // 最后一节课如果属于一组连续的同科目课程（重复课程分组），
        // 那么组内任意一节课都视为“最后一节课”，提前显示次日课表。
        var inLastLessonGroup = false;
        if (items.Count > 0)
        {
            var lastIndex = items.Count - 1;
            var groupStart = lastIndex;
            if (lastIndex < classes.Count)
            {
                var lastSubject = classes[lastIndex].SubjectId;
                while (groupStart > 0 && groupStart - 1 < classes.Count && classes[groupStart - 1].SubjectId == lastSubject)
                {
                    groupStart--;
                }
            }
            else
            {
                groupStart = lastIndex;
            }

            for (var i = groupStart; i <= lastIndex && i < items.Count; i++)
            {
                var item = items[i];
                if (now.TimeOfDay >= item.StartTime && now.TimeOfDay <= item.EndTime)
                {
                    inLastLessonGroup = true;
                    break;
                }
            }
        }

        _isLastLessonPeriod = Settings.TomorrowScheduleShowMode == 4 && inLastLessonGroup;

        ShowCurrentLessonOnlyOnClass = _showCurrentLessonOnlyFromSettings || _isLastLessonPeriod;
        PseudoClasses.Set(":show-tomorrow-schedule-at-last-class", _isLastLessonPeriod);
    }

    /// <summary>
    /// 取“明天”的天气：按日出日落数据里的日期定位明天，避免不同接口起始日不一致。
    /// </summary>
    private void UpdateTomorrowWeather()
    {
        try
        {
            var daily = WeatherService?.LastWeatherInfo?.ForecastDaily;
            var temperatures = daily?.Temperature?.Value;
            var weathers = daily?.Weather?.Value;
            if (temperatures == null || weathers == null || temperatures.Count == 0 || weathers.Count == 0)
            {
                HasTomorrowWeather = false;
                IsTomorrowRainy = false;
                return;
            }

            var tomorrow = ExactTimeService.GetCurrentLocalDateTime().Date.AddDays(1);
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

            var temperature = temperatures[index];
            var weather = weathers[index];
            var textFrom = WeatherService.GetWeatherTextByCode(weather.From);
            var textTo = WeatherService.GetWeatherTextByCode(weather.To);
            var weatherText = textFrom == textTo ? textFrom : $"{textFrom}转{textTo}";
            var low = int.TryParse(temperature.To, out var lowValue) ? lowValue : (int?)null;
            var high = int.TryParse(temperature.From, out var highValue) ? highValue : (int?)null;
            var temperatureText = low.HasValue && high.HasValue ? $" {low}~{high}℃" : "";

            TomorrowWeatherText = weatherText + temperatureText;
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

    /// <summary>
    /// 把“当前是否显示次日课表”同步给共享状态，供“次日天气”小组件和“天气简报”联动。
    /// 判定必须和课表的伪类保持一致，否则会出现“课表显示了次日、天气却没显示”。
    /// </summary>
    private bool ComputeTomorrowViewActive(bool onEmpty)
    {
        if (TomorrowClassPlan == null)
        {
            return false;
        }

        return Settings.TomorrowScheduleShowMode switch
        {
            1 => IsAfterSchool,
            2 => true,
            3 => onEmpty,
            4 => _isLastLessonPeriod || IsAfterSchool,
            _ => false
        };
    }

    private void PushTomorrowViewState(bool onEmpty)
    {
        TomorrowWeatherService.SetViewActive(ComputeTomorrowViewActive(onEmpty));
    }

    private void UpdateTomorrowVisibility()
    {
        var showOnEmpty = Settings.TomorrowScheduleShowMode == 3;
        if (!showOnEmpty)
        {
            PseudoClasses.Set(":show-tomorrow-schedule-on-empty", false);
            PushTomorrowViewState(false);
            return;
        }
        var now = ExactTimeService.GetCurrentLocalDateTime().TimeOfDay;
        var selectedItem = LessonsService.CurrentTimeLayoutItem;
        var classPlan = LessonsService.CurrentClassPlan;
        var hasDisplayable = HasDisplayableItems(classPlan, now, selectedItem);
        PseudoClasses.Set(":show-tomorrow-schedule-on-empty", !hasDisplayable);
        PushTomorrowViewState(!hasDisplayable);
    }

    private bool HasDisplayableItems(ClassPlan? classPlan, TimeSpan now, TimeLayoutItem? selectedItem)
    {
        var items = classPlan?.ValidTimeLayoutItems;
        if (items == null || items.Count == 0) return false;
        return items.Any(item => ShouldDisplay(item, now, selectedItem, HideFinishedClass, ShowCurrentLessonOnlyOnClass));
    }

    private static bool ShouldDisplay(TimeLayoutItem item, TimeSpan now, TimeLayoutItem? selectedItem, bool hideFinishedClass, bool showCurrentLessonOnlyOnClass)
    {
        if (item.TimeType is 2 or 3) return false;
        if (hideFinishedClass && item.EndTime < now) return false;
        if (item.IsHideDefault && !Equals(selectedItem, item)) return false;
        if (item.TimeType == 1 && !Equals(selectedItem, item)) return false;
        if (showCurrentLessonOnlyOnClass && selectedItem?.TimeType == 0 && !Equals(selectedItem, item)) return false;
        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

