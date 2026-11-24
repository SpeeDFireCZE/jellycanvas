using System;
using System.Globalization;
using Jellyfin.Plugin.Jellycanvas.Configuration;

namespace Jellyfin.Plugin.Jellycanvas.Theme;

/// <summary>
/// The info bar's time window: when it starts and stops showing, as
/// milliseconds since 1970 (UTC) - what the client script compares with
/// Date.now(). A time that cannot be read counts as no limit.
/// </summary>
public static class InfoBarSchedule
{
    /// <summary>The start, or null for "at once".</summary>
    public static long? From(InfoBarSettings i) => Read(i.ShowFrom);

    /// <summary>The end, or null for "no end".</summary>
    public static long? Until(InfoBarSettings i) => Read(i.ShowUntil);

    /// <summary>Whether the strip shows in a window only (the theme then leaves the showing to the script).</summary>
    public static bool Scheduled(InfoBarSettings i) => From(i) is not null || Until(i) is not null;

    private static long? Read(string? text)
        => DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t)
            ? t.ToUnixTimeMilliseconds()
            : null;
}
