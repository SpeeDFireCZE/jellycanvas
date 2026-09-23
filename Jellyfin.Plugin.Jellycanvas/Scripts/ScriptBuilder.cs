using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Jellyfin.Plugin.Jellycanvas.Configuration;
using Jellyfin.Plugin.Jellycanvas.Theme;

namespace Jellyfin.Plugin.Jellycanvas.Scripts;

/// <summary>
/// Builds the client script: takes the embedded <c>inject.js</c> template
/// and bakes the relevant part of the configuration into it as JSON. Like
/// CssBuilder it is pure - no server access - so the settings page can ask
/// for a preview of the script before anything is saved.
/// </summary>
public static class ScriptBuilder
{
    private const string Placeholder = "/*JELLYCANVAS_CONFIG*/{ \"buttons\": [], \"slideshow\": null, \"infoBar\": null, \"badges\": null, \"backdrop\": null, \"banner\": null, \"dashboard\": false, \"rows\": [], \"seerrOpen\": 0, \"devices\": {} }";

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
        var hasButtons = s.ToolbarButtons.Any(b => b.Enabled && CleanUrl(b.Url).Length > 0);
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
        // Rotating random backdrops: the script does it with preloaded images
        // and a real cross-fade; the CSS version stays as the fallback. The
        // background is a per-device setting, so the TV and the phone get
        // their own copy when they have changes of their own.
        // The Dashboard: Jellyfin renders the branding CSS on the
        // user-facing pages only, so the script puts it on the admin pages.
        var dashboard = c.Misc.ThemeDashboard && c.Enabled;
        var backdrop = BackdropFor(c);
        var banner = c.Detail.Banner ? new { image = c.Detail.BannerImage.ToString() } : null;
        var devices = new Dictionary<string, object?>();
        var hasBackdrop = backdrop is not null || banner is not null || dashboard;
        foreach (var (name, _) in DeviceOverrides.Devices)
        {
            var overrides = DeviceOverrides.For(c, name);
            if (name != "Web" && DeviceOverrides.HasContent(overrides))
            {
                var own = BackdropFor(DeviceOverrides.Merge(c, overrides));
                devices[name.ToLowerInvariant()] = new { backdrop = own };
                hasBackdrop |= own is not null;
            }
        }
        // Seerr rows: only with an address and a key, one entry per row turned on.
        var se = c.Seerr;
        var rows = string.IsNullOrWhiteSpace(se.Url) || string.IsNullOrWhiteSpace(se.ApiKey)
            ? Array.Empty<object>()
            : se.Rows.Select((r, i) => (r, i)).Where(t => t.r.Enabled)
                .Select(t => (object)new
                {
                    id = t.i + 1,
                    kind = t.r.Kind.ToString(),
                    media = t.r.Media.ToString(),
                    title = (t.r.Title ?? string.Empty).Trim(),
                    position = t.r.Position.ToString(),
                    limit = Math.Clamp(t.r.Limit, 1, 60),
                    showTitle = t.r.ShowTitle,
                    showSubtitle = t.r.ShowSubtitle,
                    showDate = t.r.ShowDate,
                    showState = t.r.ShowState,
                    showType = t.r.ShowType,
                    showRequester = t.r.ShowRequester,
                })
                .ToArray();
        var hasRows = rows.Length > 0;
        if (!s.Enabled || (!hasButtons && !hasSlideshow && !hasInfoBar && !hasBadges && !hasBackdrop && !hasRows))
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
            url = CleanUrl(b.Url),
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
                source = ss.Source == SlideshowSource.ContinueWatching ? "Random" : ss.Source.ToString(), // a value from before it was removed
                filter = (ss.Filter ?? string.Empty).Trim(),
                types = ss.Types.ToString(),
                count = Math.Clamp(ss.Count, 1, 30),
                interval = Math.Clamp(ss.IntervalSeconds, 3, 120),
                height = Math.Clamp(ss.Height, 25, 90),
                showLogo = ss.ShowLogo,
                showOverview = ss.ShowOverview,
                showButton = ss.ShowButton,
                buttonLabel = (ss.ButtonLabel ?? string.Empty).Trim(),
                buttonIcon = string.IsNullOrWhiteSpace(ss.ButtonIcon) ? string.Empty : CleanIcon(ss.ButtonIcon), // empty = no icon
                buttonStyle = ss.ButtonStyle.ToString(),
                buttonRadius = Math.Clamp(ss.ButtonRadius, -1, 999),
                buttonScale = Math.Clamp(ss.ButtonScale, 60, 160),
                hideOnTv = ss.HideOnTv,
                hideOnMobile = ss.HideOnMobile,
            }
            : null;

        // The text identifies a dismissal: a new announcement shows again
        // (when the dismissal is remembered at all).
        var infoBar = hasInfoBar ? new { text = ib.Text.Trim(), remember = ib.RememberClose } : null;

        var badges = hasBadges
            ? new
            {
                corners,
                style = cb.Style.ToString(),
                palette = cb.Palette.ToString(),
                scale = Math.Clamp(cb.Scale, 50, 200),
                hideOnMobile = cb.HideOnMobile,
                languages = cb.Languages.ToString(),
                subtitleLanguages = cb.SubtitleLanguages.ToString(),
                stacked = cb.Stacked,
                hideOnTv = cb.HideOnTv,
                audioMax = Math.Clamp(cb.AudioMax, 1, 4),
                audioPreferred = LanguageList(cb.AudioPreferred),
                subtitleMax = Math.Clamp(cb.SubtitleMax, 1, 4),
                subtitlePreferred = LanguageList(cb.SubtitlePreferred),
            }
            : null;

        // Seerr links through a custom button: only a button that exists,
        // is on and opens in an overlay or in place (a new-tab button adds nothing).
        var openWith = se.OpenWithButton;
        var viaButton = hasRows && openWith > 0 && openWith <= s.ToolbarButtons.Count
            && s.ToolbarButtons[openWith - 1].Enabled && s.ToolbarButtons[openWith - 1].Action != ButtonAction.NewTab
            && CleanUrl(s.ToolbarButtons[openWith - 1].Url).Length > 0;
        var seerrOpen = viaButton ? openWith : 0;

        var json = JsonSerializer.Serialize(new { buttons, slideshow, infoBar, badges, backdrop, banner, dashboard, rows, seerrOpen, devices }, JsonOptions);

        // "</script>" inside a string would end the <script> element early if
        // the script were ever inlined; harmless to neutralise it always.
        json = json.Replace("</", "<\\/", StringComparison.Ordinal);

        return Template.Value.Replace(Placeholder, json, StringComparison.Ordinal);
    }

    /// <summary>The script's backdrop part for one configuration: rotation and / or the item page's own backdrop; null when the script has nothing to do.</summary>
    private static object? BackdropFor(PluginConfiguration c)
    {
        var bd = c.Backdrop;
        var rotation = bd.Mode == BackdropMode.RandomLibrary && bd.RotateSeconds > 0;
        // The item page's banner is the same layer with the item's picture,
        // so it carries the script even where the background itself is Jellyfin's.
        var detail = bd.ItemDetail || c.Detail.Banner;
        var has = rotation || (detail && (bd.Mode != BackdropMode.Default || c.Detail.Banner));
        return has ? new { seconds = rotation ? Math.Max(3, bd.RotateSeconds) : 0, detail, tvStatic = c.Tv.StaticBackdrop } : null;
    }

    /// <summary>A short stamp of the script these settings produce - the same settings, the same stamp.</summary>
    public static string Stamp(PluginConfiguration c)
    {
        var bytes = System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(Build(c)));
        return Convert.ToHexString(bytes, 0, 5).ToLowerInvariant();
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

    /// <summary>"cs, EN;de" → ["CS", "EN", "DE"]: the codes the badges use.</summary>
    private static string[] LanguageList(string text)
        => (text ?? string.Empty).Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(c => c.ToUpperInvariant()).Distinct().ToArray();

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

    /// <summary>
    /// A button target: a web address (http, https, mailto) or a path or
    /// hash inside this client. Anything else - javascript:, data:, a
    /// scheme the browser would run rather than open - is dropped, and a
    /// button without a target is not shown.
    /// </summary>
    private static string CleanUrl(string? value)
    {
        var v = (value ?? string.Empty).Trim();
        if (v.Length == 0)
        {
            return string.Empty;
        }

        var colon = v.IndexOf(':', StringComparison.Ordinal);
        var slash = v.IndexOfAny(['/', '#', '?']);
        var hasScheme = colon > 0 && (slash < 0 || colon < slash);
        if (!hasScheme)
        {
            return v;
        }

        var scheme = v[..colon].ToLowerInvariant();
        return scheme is "http" or "https" or "mailto" ? v : string.Empty;
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
            .Where(b => b.Enabled && CleanUrl(b.Url).Length > 0)
            .Select(b => $"{Clean(b.Label, "Button")} -> {CleanUrl(b.Url)} ({b.Action}, {b.Placement})")
            .ToList();
    }
}
