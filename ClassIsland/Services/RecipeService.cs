using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Helpers;
using ClassIsland.Models;
using ClassIsland.Shared.Enums;
using ClassIsland.Shared.Models.Profile;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services;

public sealed class RecipeService : ObservableRecipient, IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly ILessonsService _lessonsService;
    private readonly IExactTimeService _exactTimeService;
    private readonly ILogger<RecipeService> _logger;
    private IReadOnlyList<RecipeDay> _days = [];
    private string _cachedPath = "";
    private DateTime _cachedWriteTime = DateTime.MinValue;
    private string _activeOccurrence = "";
    private string _dismissedOccurrence = "";
    private DateTime _manualPreviewUntil = DateTime.MinValue;
    private string _title = "";
    private string _subTitle = "";
    private string _content = "";
    private bool _isVisible;
    private bool _isFullscreenForeground;
    private bool _isInitialized;
    private string _statusText = "尚未导入菜单";

    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public string SubTitle { get => _subTitle; private set => SetProperty(ref _subTitle, value); }
    public string Content { get => _content; private set => SetProperty(ref _content, value); }
    public bool IsVisible { get => _isVisible; private set => SetProperty(ref _isVisible, value); }

    /// <summary>
    /// 当前前台窗口是否处于全屏状态。全屏时食谱改为在主界面内放大显示并允许滚动。
    /// </summary>
    public bool IsFullscreenForeground
    {
        get => _isFullscreenForeground;
        set => SetProperty(ref _isFullscreenForeground, value);
    }

    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public ObservableCollection<RecipeSection> ActiveSections { get; } = [];

    public RecipeService(
        SettingsService settingsService,
        ILessonsService lessonsService,
        IExactTimeService exactTimeService,
        ILogger<RecipeService> logger)
    {
        _settingsService = settingsService;
        _lessonsService = lessonsService;
        _exactTimeService = exactTimeService;
        _logger = logger;
        _settingsService.Settings.PropertyChanged += SettingsOnPropertyChanged;
        _lessonsService.PostMainTimerTicked += LessonsServiceOnPostMainTimerTicked;
    }

    public void Initialize()
    {
        _isInitialized = true;
        ReloadWorkbook();
    }

    public async Task<bool> ValidateWorkbookAsync(string path)
    {
        try
        {
            var result = await Task.Run(() => RecipeExcelReader.Read(path));
            StatusText = result.Count == 0
                ? "Excel 中没有识别到午餐或晚餐"
                : $"已识别 {result.Count} 天菜单";
            return result.Count > 0;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "读取食谱 Excel 失败。");
            StatusText = "菜单文件读取失败：" + exception.Message;
            return false;
        }
    }

    public void ShowPreview(RecipeDay day, bool lunch)
    {
        SetActiveSections(day, lunch);
        _manualPreviewUntil = DateTime.MaxValue;
        IsVisible = true;
    }

    public bool TryGetTodayRecipe(out RecipeDay? recipe)
    {
        if (_days.Count == 0)
        {
            ReloadWorkbook();
        }
        var today = _exactTimeService.GetCurrentLocalDateTime().Date;
        recipe = _days.FirstOrDefault(day => day.MatchesDate(today));
        _logger.LogInformation("食谱预览查询：日期 {Date:yyyy-MM-dd}，已识别 {Count} 天，结果 {Found}",
            today,
            _days.Count,
            recipe != null);
        return recipe != null;
    }

    public void Dismiss()
    {
        _manualPreviewUntil = DateTime.MinValue;
        if (!string.IsNullOrWhiteSpace(_activeOccurrence))
        {
            _dismissedOccurrence = _activeOccurrence;
        }
        HideAll();
    }

    private void SettingsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Settings.RecipeWorkbookPath) or nameof(Settings.IsRecipePreviewEnabled))
        {
            ReloadWorkbook();
        }
    }

    private void ReloadWorkbook()
    {
        if (!_isInitialized)
        {
            return;
        }

        var path = _settingsService.Settings.RecipeWorkbookPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            _days = [];
            _cachedPath = "";
            StatusText = string.IsNullOrWhiteSpace(path) ? "尚未导入菜单" : "菜单文件不存在";
            return;
        }

        try
        {
            var writeTime = File.GetLastWriteTimeUtc(path);
            if (!string.Equals(path, _cachedPath, StringComparison.OrdinalIgnoreCase) || writeTime != _cachedWriteTime)
            {
                _days = RecipeExcelReader.Read(path);
                _cachedPath = path;
                _cachedWriteTime = writeTime;
            }
            StatusText = _days.Count == 0 ? "未识别到菜单" : $"已识别 {_days.Count} 天菜单";
        }
        catch (Exception exception)
        {
            _days = [];
            StatusText = "菜单读取失败：" + exception.Message;
            _logger.LogError(exception, "读取食谱 Excel 失败。");
        }
    }

    private void LessonsServiceOnPostMainTimerTicked(object? sender, EventArgs e)
    {
        if (_manualPreviewUntil > DateTime.Now)
        {
            return;
        }
        _manualPreviewUntil = DateTime.MinValue;
        if (!_settingsService.Settings.IsRecipePreviewEnabled)
        {
            HideAll("未启用");
            return;
        }

        var now = _exactTimeService.GetCurrentLocalDateTime();
        var plan = _lessonsService.CurrentClassPlan;
        var current = _lessonsService.CurrentTimeLayoutItem;
        if (plan?.TimeLayout == null || current?.TimeType != 1)
        {
            HideAll($"不在课间 plan={plan != null} type={current?.TimeType}");
            return;
        }

        var hideBefore = Math.Max(0, _settingsService.Settings.RecipeHideBeforeClassSeconds);
        // 直接从当前课表计算下一节课，避免依赖可能滞后的课程状态。
        var nextClass = plan.ValidTimeLayoutItems
            .Where(item => item.TimeType == 0 && item.StartTime > now.TimeOfDay)
            .OrderBy(item => item.StartTime)
            .FirstOrDefault();
        if (nextClass != null && nextClass.StartTime - now.TimeOfDay <= TimeSpan.FromSeconds(hideBefore))
        {
            HideAll($"临近下节课 next={nextClass.StartTime} now={now.TimeOfDay} hideBefore={hideBefore}");
            return;
        }

        var previousClass = plan.ValidTimeLayoutItems
            .Where(item => item.TimeType == 0 && item.EndTime <= now.TimeOfDay)
            .OrderByDescending(item => item.EndTime)
            .FirstOrDefault();
        if (previousClass == null)
        {
            HideAll("没有已结束课程");
            return;
        }

        var lessonKey = LessonTimePointHelper.CreateKey(plan.TimeLayoutId, previousClass);
        var isLunch = lessonKey == _settingsService.Settings.RecipeLunchTriggerLessonKey;
        var isDinner = lessonKey == _settingsService.Settings.RecipeDinnerTriggerLessonKey;
        if (!isLunch && !isDinner)
        {
            HideAll($"课程不匹配 {lessonKey}");
            return;
        }

        var delay = Math.Max(0, _settingsService.Settings.RecipeShowDelaySeconds);
        if (now.TimeOfDay - current.StartTime < TimeSpan.FromSeconds(delay))
        {
            return;
        }

        var occurrence = $"{lessonKey}|{current.StartTime.Ticks}|{(isLunch ? "lunch" : "dinner")}";
        if (occurrence == _dismissedOccurrence)
        {
            return;
        }

        var recipe = _days.FirstOrDefault(day => day.MatchesDate(now.Date));
        if (recipe == null)
        {
            _logger.LogInformation("食谱预览：菜单中没有 {Date:MM-dd} 的餐食安排（已识别 {Count} 天）。", now.Date, _days.Count);
            return;
        }

        if (_activeOccurrence != occurrence)
        {
            _activeOccurrence = occurrence;
            SetActiveSections(recipe, isLunch);
            _logger.LogInformation("食谱预览：显示{Meal}，课程 {Lesson}。", isLunch ? "午餐" : "晚餐", lessonKey);
        }
        IsVisible = true;
    }

    private void SetActiveSections(RecipeDay day, bool lunch)
    {
        Title = $"今日{(lunch ? "午餐" : "晚餐")}";
        SubTitle = $"{day.Month} 月 {day.Day} 日";
        ActiveSections.Clear();
        foreach (var section in day.GetSections(lunch))
        {
            if (section.Items.Count > 0)
            {
                ActiveSections.Add(section);
            }
        }
        if (ActiveSections.Count == 0)
        {
            ActiveSections.Add(new RecipeSection
            {
                Name = lunch ? "午餐" : "晚餐",
                Items = ["今日菜单为空"]
            });
        }
    }

    private void HideAll(string reason = "")
    {
        if (IsVisible)
        {
            _logger.LogInformation("食谱预览隐藏：{Reason}", reason);
        }
        IsVisible = false;
    }

    public void Dispose()
    {
        _settingsService.Settings.PropertyChanged -= SettingsOnPropertyChanged;
        _lessonsService.PostMainTimerTicked -= LessonsServiceOnPostMainTimerTicked;
    }
}
