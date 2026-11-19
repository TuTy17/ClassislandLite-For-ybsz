using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services;

public class TaskBarIconService : IHostedService, ITaskBarIconService
{
    public TaskBarIconService(ILogger<TaskBarIconService> logger)
    {
        Logger = logger;
        
        AppBase.Current.AppStopping += CurrentOnAppStopping;
    }

    private void CurrentOnAppStopping(object? sender, EventArgs e)
    {
        MainTaskBarIcon.IsVisible = false;
    }

    public ILogger<TaskBarIconService> Logger { get; }

    public TrayIcon MainTaskBarIcon
    {
        get;
    } = new()
    {
        Icon = new WindowIcon(ResolveIconPath()),
        ToolTipText = "ClassIsland Lite"
    };

    /// <summary>
    /// 托盘图标按程序所在目录解析。启动器的工作目录和程序目录可能不同，用相对路径会取不到图标而显示成旧图标。
    /// </summary>
    private static string ResolveIconPath()
    {
        var fileName = OperatingSystem.IsMacOS() ? "AppLogo_Monochrome.png" : "AppLogo.png";
        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine("..", "Resources", "Assets", fileName);
        }

        var absolute = Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
        return File.Exists(absolute) ? absolute : Path.Combine("Assets", fileName);
    }

    public IList<NativeMenuItemBase> MoreOptionsMenuItems => MoreOptionsMenu!.Items;
    
    public NativeMenu? MoreOptionsMenu { get; set; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        return;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        return;
    }
}