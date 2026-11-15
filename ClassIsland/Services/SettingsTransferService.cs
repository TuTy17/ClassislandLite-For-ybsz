using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ClassIsland.Core;
using ClassIsland.Models;
using ClassIsland.Shared.Helpers;

namespace ClassIsland.Services;

public sealed class SettingsExportBundle
{
    public string Schema { get; set; } = "classisland-lite-config/1";
    public DateTime ExportedAt { get; set; } = DateTime.Now;
    public string AppVersion { get; set; } = AppBase.AppVersion;
    public Settings Settings { get; set; } = new();
}

public static class SettingsTransferService
{
    private static readonly HashSet<string> ExcludedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "WindowDockingLocation",
        "WindowDockingOffsetX",
        "WindowDockingOffsetY",
        "WindowDockingMonitorIndex",
        "Scale",
        "UseRawInput",
        "IsErrorLoadingRawInput",
        "IsIgnoreWorkAreaEnabled",
        "IsScreenRecordingModeEnabled",
        "IsWindowCaptureBlockingEnabled",
        "WindowTopmostRecheckMode",
        "MainWindowFont",
        "MainWindowFontWeight2",
        "WeatherLongitude",
        "WeatherLatitude",
        "WeatherLocationSource",
        "AutoRefreshWeatherLocation",
        "NoTLSWeatherRequests",
        "CityId",
        "CityName",
        "LastWeatherInfo",
        "ExcludedWeatherAlerts",
        "WeatherIconId",
        "SelectedSpeechProvider",
        "IsMainWindowVisible",
        "SelectedProfile",
        "IsWelcomeWindowShowed",
        "LastCheckUpdateTime",
        "LastCheckUpdateInfoCacheGitHub",
        "SelectedUpdateChannelV2",
        "SelectedUpdateChannelV3",
        "SelectedChannel",
        "OfficialIndexMirrors",
        "OfficialSelectedMirror",
        "PluginIndexes",
        "PluginIndexSelectedMirrors",
        "UserPluginIndexes",
        "AdditionalPluginIndexes",
        "IsPluginMarketWarningVisible"
    };

    public static bool IsExcluded(string propertyName)
    {
        return ExcludedProperties.Contains(propertyName)
               || propertyName.Contains("Weather", StringComparison.OrdinalIgnoreCase)
               || propertyName.Contains("Plugin", StringComparison.OrdinalIgnoreCase)
               || propertyName.Contains("Update", StringComparison.OrdinalIgnoreCase)
               || propertyName.Contains("Sentry", StringComparison.OrdinalIgnoreCase)
               || propertyName.Contains("WindowDocking", StringComparison.OrdinalIgnoreCase)
               || propertyName.Contains("RawInput", StringComparison.OrdinalIgnoreCase)
               || propertyName.Contains("Management", StringComparison.OrdinalIgnoreCase)
               || propertyName.Contains("MainWindowFont", StringComparison.OrdinalIgnoreCase)
               || propertyName.Contains("City", StringComparison.OrdinalIgnoreCase);
    }

    public static void Export(Settings settings, string path)
    {
        var copy = ConfigureFileHelper.CopyObject(settings);
        var defaults = new Settings();
        foreach (var property in GetWritableProperties())
        {
            if (!IsExcluded(property.Name))
            {
                continue;
            }

            try
            {
                property.SetValue(copy, property.GetValue(defaults));
            }
            catch
            {
                // Device-specific properties are never imported; failing to reset one is harmless.
            }
        }

        ConfigureFileHelper.SaveConfig(path, new SettingsExportBundle { Settings = copy }, true);
    }

    public static int Import(Settings target, string path)
    {
        var bundle = ConfigureFileHelper.LoadConfigUnWrapped<SettingsExportBundle>(path, false);
        var imported = bundle.Settings ?? new Settings();
        var applied = 0;

        var settingsPath = Path.Combine(CommonDirectories.AppRootFolderPath, "Settings.json");
        if (File.Exists(settingsPath))
        {
            var backupName = $"Settings.lite-import-{DateTime.Now:yyyyMMdd-HHmmss}.json.bak";
            File.Copy(settingsPath, Path.Combine(CommonDirectories.AppRootFolderPath, backupName), true);
        }

        foreach (var property in GetWritableProperties())
        {
            if (IsExcluded(property.Name))
            {
                continue;
            }

            try
            {
                var value = property.GetValue(imported);
                property.SetValue(target, value);
                applied++;
            }
            catch
            {
                // A single incompatible property must not abort the whole import.
            }
        }

        ConfigureFileHelper.SaveConfig(settingsPath, target);
        return applied;
    }

    private static IEnumerable<PropertyInfo> GetWritableProperties()
    {
        return typeof(Settings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0);
    }
}
