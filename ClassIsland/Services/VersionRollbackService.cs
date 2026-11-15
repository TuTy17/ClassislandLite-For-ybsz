using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ClassIsland.Core;
using ClassIsland.Models;

namespace ClassIsland.Services;

/// <summary>
/// 版本快照与回退。快照保存的是当前运行的程序目录，回退时在程序退出后由脚本把快照覆盖回去并重新启动。
/// </summary>
public static class VersionRollbackService
{
    private const int MaxSnapshots = 3;

    public static string SnapshotRoot =>
        Path.Combine(CommonDirectories.AppRootFolderPath, "Backups", "Versions");

    /// <summary>
    /// 当前运行的程序目录（即包含 ClassislandLite.Desktop.exe 的目录）。
    /// </summary>
    public static string CurrentAppDirectory =>
        Path.GetDirectoryName(Environment.ProcessPath ?? "") ?? AppContext.BaseDirectory;

    public static string CurrentVersionText
    {
        get
        {
            var folder = Path.GetFileName(CurrentAppDirectory.TrimEnd(Path.DirectorySeparatorChar));
            return string.IsNullOrWhiteSpace(folder) ? "未知" : folder;
        }
    }

    public static IReadOnlyList<VersionSnapshot> ListSnapshots()
    {
        if (!Directory.Exists(SnapshotRoot))
        {
            return [];
        }

        var result = new List<VersionSnapshot>();
        foreach (var directory in Directory.EnumerateDirectories(SnapshotRoot))
        {
            var appDirectory = Path.Combine(directory, "app");
            if (!File.Exists(Path.Combine(appDirectory, "ClassislandLite.Desktop.exe")))
            {
                continue;
            }

            var createdAt = Directory.GetCreationTime(directory);
            var metaPath = Path.Combine(directory, "snapshot.json");
            var version = "";
            if (File.Exists(metaPath))
            {
                try
                {
                    var meta = JsonSerializer.Deserialize<SnapshotMeta>(File.ReadAllText(metaPath));
                    if (meta != null)
                    {
                        version = meta.Version;
                        if (DateTime.TryParse(meta.CreatedAt, out var parsed))
                        {
                            createdAt = parsed;
                        }
                    }
                }
                catch
                {
                    // 忽略损坏的元数据
                }
            }

            result.Add(new VersionSnapshot
            {
                Path = directory,
                CreatedAt = createdAt,
                Version = version,
                SizeBytes = GetDirectorySize(directory)
            });
        }

        return result.OrderByDescending(snapshot => snapshot.CreatedAt).ToList();
    }

    public static async Task<VersionSnapshot> CreateSnapshotAsync(IProgress<string>? progress = null)
    {
        var source = CurrentAppDirectory;
        if (!File.Exists(Path.Combine(source, "ClassislandLite.Desktop.exe")))
        {
            throw new InvalidOperationException("找不到当前程序目录，无法创建快照。");
        }

        Directory.CreateDirectory(SnapshotRoot);
        var name = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var target = Path.Combine(SnapshotRoot, name);
        var appTarget = Path.Combine(target, "app");

        progress?.Report("正在复制程序文件…");
        await Task.Run(() => CopyDirectory(source, appTarget));

        var meta = new SnapshotMeta
        {
            Version = CurrentVersionText,
            CreatedAt = DateTime.Now.ToString("o")
        };
        await File.WriteAllTextAsync(Path.Combine(target, "snapshot.json"),
            JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));

        PruneOldSnapshots();
        progress?.Report("快照已创建：" + name);
        return ListSnapshots().FirstOrDefault(snapshot => snapshot.Name == name)
               ?? new VersionSnapshot { Path = target, CreatedAt = DateTime.Now, Version = CurrentVersionText };
    }

    /// <summary>
    /// 安排一次回退：写一个脚本，等当前进程退出后用快照覆盖程序目录并重新启动。
    /// </summary>
    public static void ScheduleRestore(VersionSnapshot snapshot)
    {
        var appSource = Path.Combine(snapshot.Path, "app");
        if (!File.Exists(Path.Combine(appSource, "ClassislandLite.Desktop.exe")))
        {
            throw new InvalidOperationException("该快照不完整，缺少程序文件。");
        }

        var appTarget = CurrentAppDirectory;
        var packageRoot = CommonDirectories.AppPackageRoot;
        var launcher = Path.Combine(packageRoot, "ClassislandLite.exe");
        var startTarget = File.Exists(launcher)
            ? launcher
            : Path.Combine(appTarget, "ClassislandLite.Desktop.exe");

        var scriptPath = Path.Combine(Path.GetTempPath(), $"classisland-rollback-{Guid.NewGuid():N}.cmd");
        var logPath = Path.Combine(CommonDirectories.AppLogFolderPath, "rollback.log");
        Directory.CreateDirectory(CommonDirectories.AppLogFolderPath);

        var script = new StringBuilder();
        script.AppendLine("@echo off");
        script.AppendLine("setlocal");
        script.AppendLine(":wait");
        script.AppendLine("timeout /t 1 /nobreak >nul");
        script.AppendLine($"tasklist /FI \"PID eq {Environment.ProcessId}\" 2>nul | find \"{Environment.ProcessId}\" >nul");
        script.AppendLine("if not errorlevel 1 goto wait");
        script.AppendLine($"robocopy \"{appSource}\" \"{appTarget}\" /MIR /R:2 /W:2 /NFL /NDL /NJH /NJS > \"{logPath}\" 2>&1");
        script.AppendLine($"start \"\" \"{startTarget}\"");
        script.AppendLine("del \"%~f0\"");
        File.WriteAllText(scriptPath, script.ToString(), Encoding.Default);

        var info = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{scriptPath}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        Process.Start(info);
    }

    public static void DeleteSnapshot(VersionSnapshot snapshot)
    {
        if (Directory.Exists(snapshot.Path))
        {
            Directory.Delete(snapshot.Path, true);
        }
    }

    private static void PruneOldSnapshots()
    {
        var snapshots = ListSnapshots();
        foreach (var snapshot in snapshots.Skip(MaxSnapshots))
        {
            try
            {
                DeleteSnapshot(snapshot);
            }
            catch
            {
                // 忽略删除失败
            }
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }

    private static long GetDirectorySize(string path)
    {
        try
        {
            return new DirectoryInfo(path)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(file => file.Length);
        }
        catch
        {
            return 0;
        }
    }

    private sealed class SnapshotMeta
    {
        public string Version { get; set; } = "";
        public string CreatedAt { get; set; } = "";
    }
}
