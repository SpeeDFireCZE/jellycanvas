using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Jellyfin.Plugin.Jellycanvas.Configuration;

namespace Jellyfin.Plugin.Jellycanvas.Scripts;

/// <summary>
/// Builds the client script: takes the embedded <c>inject.js</c> template
/// and bakes the relevant part of the configuration into it as JSON. Like
/// CssBuilder it is pure - no server access - so the settings page can ask
/// for a preview of the script before anything is saved.
/// </summary>
public static class ScriptBuilder
{
    private const string Placeholder = "/*JELLYCANVAS_CONFIG*/{ \"buttons\": [], \"slideshow\": null, \"infoBar\": null, \"badges\": null, \"backdrop\": null }";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly Lazy<string> Template = new(LoadTemplate);

    /// <summary>The script for these settings; an empty string when there is nothing to inject.</summary>
    public static string Build(PluginConfiguration c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var s = c.Scripts;
        var hasButtons = s.ToolbarButtons.Any(b => b.Enabled && !string.IsNullOrWhiteSpace(b.Url));
        var hasSlideshow = s.Slideshow.Enabled;
        var ib = c.InfoBar;
        var hasInfoBar = ib.Enabled && ib.Closable && !string.IsNullOrWhiteSpace(ib.Text);
        var cb = s.CardBadges;
        var corners = new Dictionary<string, string[]>
        {
            ["tl"] = BadgeList(cb.TopLeft),
            ["tr"] = BadgeList(cb.TopRight),
            ["bl"] = BadgeList(cb.BottomLeft),
            ["br"] = BadgeList(cb.BottomRight),
        };
        var hasBadges = cb.Enabled && corners.Values.Any(v => v.Length > 0);
        var bd = c.Backdrop;
        // Rotating random backdrops: the script does it with preloaded images
        // and a real cross-fade; the CSS version stays as the fallback.
        var hasBackdrop = bd.Mode == BackdropMode.RandomLibrary && bd.RotateSeconds > 0;
        if (!s.Enabled || (!hasButtons && !hasSlideshow && !hasInfoBar && !hasBadges && !hasBackdrop))
        {
            return string.Empty;
        }

        // Buttons get stable ids from their position so the DOM ids do not
        // change between page loads (the script uses them to find its nodes).
        var buttons = s.ToolbarButtons.Select((b, i) => new
        {
            id = i + 1,
            enabled = b.Enabled,
            label = Clean(b.Label, "Button"),
            icon = CleanIcon(b.Icon),
            url = b.Url.Trim(),
            action = b.Action.ToString(),
            placement = b.Placement.ToString(),
            hideOnTv = b.HideOnTv,
            keepAlive = b.KeepAlive,
            showBeforeLogin = b.ShowBeforeLogin,
        });

        var ss = s.Slideshow;
        var slideshow = hasSlideshow
            ? new
            {
                source = ss.Source.ToString(),
                filter = (ss.Filter ?? string.Empty).Trim(),
                types = ss.Types.ToString(),
                count = Math.Clamp(ss.Count, 1, 30),
                interval = Math.Clamp(ss.IntervalSeconds, 3, 120),
                height = Math.Clamp(ss.Height, 25, 90),
                showLogo = ss.ShowLogo,
                showOverview = ss.ShowOverview,
                showButton = ss.ShowButton,
                hideOnTv = ss.HideOnTv,
                hideOnMobile = ss.HideOnMobile,
            }
            : null;

        // The text identifies a dismissal: a new announcement shows again.
        var infoBar = hasInfoBar ? new { text = ib.Text.Trim() } : null;

        var badges = hasBadges
            ? new
            {
                corners,
                style = cb.Style.ToString(),
                scale = Math.Clamp(cb.Scale, 50, 200),
                hideOnMobile = cb.HideOnMobile,
                languages = cb.Languages.ToString(),
                subtitleLanguages = cb.SubtitleLanguages.ToString(),
                stacked = cb.Stacked,
            }
            : null;

        var backdrop = hasBackdrop ? new { seconds = Math.Max(3, bd.RotateSeconds) } : null;

        var json = JsonSerializer.Serialize(new { buttons, slideshow, infoBar, badges, backdrop }, JsonOptions);

        // "</script>" inside a string would end the <script> element early if
        // the script were ever inlined; harmless to neutralise it always.
        json = json.Replace("</", "<\\/", StringComparison.Ordinal);

        return Template.Value.Replace(Placeholder, json, StringComparison.Ordinal);
    }

    private static string LoadTemplate()
    {
        var name = typeof(ScriptBuilder).Namespace + ".inject.js";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Embedded resource missing: " + name);
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        if (!text.Contains(Placeholder, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("inject.js has no configuration placeholder");
        }

        return text;
    }

    private static readonly string[] BadgeIds = { "resolution", "hdr", "codec", "sound", "audio", "subtitles" };

    /// <summary>The known badge ids in a corner's list, in the order given.</summary>
    private static string[] BadgeList(string? value)
        => (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => v.ToLowerInvariant())
            .Where(v => BadgeIds.Contains(v))
            .Distinct()
            .ToArray();

    private static string Clean(string? value, string fallback)
    {
        var v = (value ?? string.Empty).Trim();
        return v.Length == 0 ? fallback : v;
    }

    /// <summary>Material Icons names are lowercase letters, digits and underscores; anything else is not an icon.</summary>
    private static string CleanIcon(string? value)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        return v.Length > 0 && v.All(ch => char.IsAsciiLetterLower(ch) || char.IsAsciiDigit(ch) || ch == '_') ? v : "open_in_new";
    }

    /// <summary>Selected properties that the settings page needs for the "what will be injected" summary.</summary>
    public static IReadOnlyList<string> Describe(PluginConfiguration c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return c.Scripts.ToolbarButtons
            .Where(b => b.Enabled && !string.IsNullOrWhiteSpace(b.Url))
            .Select(b => $"{Clean(b.Label, "Button")} -> {b.Url.Trim()} ({b.Action}, {b.Placement})")
            .ToList();
    }
}
