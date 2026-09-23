using System;
using Jellyfin.Plugin.Jellycanvas.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Branding;

namespace Jellyfin.Plugin.Jellycanvas.Theme;

/// <summary>
/// Writes the generated CSS where an admin would paste it by hand:
/// Dashboard → General → Branding → Custom CSS. Jellyfin keeps it in
/// <c>config/branding.xml</c> and the web client fetches it from
/// <c>/Branding/Css</c>.
///
/// Our block is fenced by markers, so:
///  - whatever the admin wrote into Custom CSS themselves stays untouched,
///  - saving again replaces the block, never adds a second one,
///  - disabling/uninstalling can cut it out cleanly.
///
/// The block goes first because it may contain @import (fonts), which CSS
/// only allows before any other rule.
/// </summary>
public static class BrandingWriter
{
    private const string Key = "branding";

    /// <summary>Returns the current Custom CSS (all of it, foreign parts included).</summary>
    public static string Read(IServerConfigurationManager config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.GetConfiguration<BrandingOptions>(Key).CustomCss ?? string.Empty;
    }

    /// <summary>Inserts or replaces our block and saves.</summary>
    public static void Write(IServerConfigurationManager config, string block)
    {
        ArgumentNullException.ThrowIfNull(config);
        var options = config.GetConfiguration<BrandingOptions>(Key);
        var foreign = StripBlock(options.CustomCss ?? string.Empty);
        options.CustomCss = foreign.Length == 0
            ? block.TrimEnd() + Environment.NewLine
            : block.TrimEnd() + Environment.NewLine + Environment.NewLine + foreign;
        config.SaveConfiguration(Key, options);
    }

    /// <summary>Removes our block and saves; foreign CSS stays.</summary>
    public static void Remove(IServerConfigurationManager config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var options = config.GetConfiguration<BrandingOptions>(Key);
        var foreign = StripBlock(options.CustomCss ?? string.Empty);
        options.CustomCss = foreign.Length == 0 ? null : foreign;
        config.SaveConfiguration(Key, options);
    }

    /// <summary>Is our block in Branding right now?</summary>
    public static bool IsPresent(IServerConfigurationManager config)
        => Read(config).Contains(CssBuilder.StartMarker, StringComparison.Ordinal);

    /// <summary>Our block as it stands in Branding (empty when there is none).</summary>
    public static string CurrentBlock(IServerConfigurationManager config) => BlockIn(Read(config));

    /// <summary>Our block inside the given CSS, markers included (empty when there is none).</summary>
    public static string BlockIn(string css)
    {
        ArgumentNullException.ThrowIfNull(css);
        var start = css.IndexOf(CssBuilder.StartMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        var end = css.IndexOf(CssBuilder.EndMarker, start, StringComparison.Ordinal);
        return end < 0 ? css[start..].TrimEnd() : css[start..(end + CssBuilder.EndMarker.Length)].TrimEnd();
    }

    /// <summary>
    /// Brings the block up to date with what this build makes of the saved
    /// settings, and says whether it had to write. The block is only written
    /// when the settings are applied, so after a plugin update it still holds
    /// the CSS the previous version generated - every fix in it would wait
    /// for someone to open the designer and press Apply. Nothing is touched
    /// when the theme is off or when the block is not in Branding at all.
    /// </summary>
    public static bool Refresh(IServerConfigurationManager config, PluginConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.Enabled || !IsPresent(config))
        {
            return false;
        }

        var css = CssBuilder.Build(settings).TrimEnd();
        if (string.Equals(CurrentBlock(config), css, StringComparison.Ordinal))
        {
            return false;
        }

        Write(config, css);
        return true;
    }

    /// <summary>
    /// Returns the text without our block (from the START marker through the
    /// END marker, inclusive). A pure function with no side effects - which is
    /// why it is public and unit-tested. When START has no END (someone broke
    /// it by hand) everything from START to the end is cut - better to lose a
    /// piece of foreign CSS than to leave two broken blocks in Branding.
    /// </summary>
    public static string StripBlock(string css)
    {
        ArgumentNullException.ThrowIfNull(css);
        var start = css.IndexOf(CssBuilder.StartMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            return css.Trim();
        }

        var end = css.IndexOf(CssBuilder.EndMarker, start, StringComparison.Ordinal);
        var after = end < 0 ? string.Empty : css[(end + CssBuilder.EndMarker.Length)..];
        return (css[..start].TrimEnd() + Environment.NewLine + after.TrimStart()).Trim();
    }
}
