namespace Scrapbook.Core;

/// <summary>Non-sensitive window state remembered between launches. Edit mode is never stored.</summary>
public sealed record WindowSettings(
    double? Left = null,
    double? Top = null,
    double? Width = null,
    double? Height = null,
    bool AlwaysOnTop = true,
    bool HideFromScreenShare = true);
