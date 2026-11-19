using System;

namespace ClassIsland.Models;

/// <summary>
/// 从服务器收到的远程提醒消息。
/// </summary>
public sealed class RemoteMessageItem
{
    public long Id { get; init; }
    public string Text { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }

    public string CreatedAtText => CreatedAt.ToLocalTime().ToString("HH:mm");
}
