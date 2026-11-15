using System;
using System.Collections.Generic;

namespace ClassIsland.Platforms.Abstraction.Models;

public sealed record PlatformToastNotification(
    uint Id,
    DateTimeOffset CreationTime,
    string AppId,
    string AppName,
    IReadOnlyList<string> Text);

public sealed record PlatformNotificationAccessResult(bool IsAllowed, string Status);
