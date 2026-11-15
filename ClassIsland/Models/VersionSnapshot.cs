using System;
using System.IO;

namespace ClassIsland.Models;

/// <summary>
/// 一个可回退的版本快照。
/// </summary>
public sealed class VersionSnapshot
{
    public string Path { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public string Version { get; init; } = "";
    public long SizeBytes { get; init; }
    public string Note { get; init; } = "";

    public string Name => System.IO.Path.GetFileName(Path);
    public string CreatedAtText => CreatedAt.ToString("yyyy-MM-dd HH:mm");
    public string SizeText => SizeBytes <= 0
        ? "-"
        : SizeBytes >= 1024L * 1024 * 1024
            ? $"{SizeBytes / 1024d / 1024 / 1024:0.0} GB"
            : $"{SizeBytes / 1024d / 1024:0.0} MB";
    public string VersionText => string.IsNullOrWhiteSpace(Version) ? "未知版本" : Version;
    public string DisplayText => $"{CreatedAtText} · {VersionText} · {SizeText}";
}
