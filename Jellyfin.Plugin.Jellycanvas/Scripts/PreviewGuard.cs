using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.Jellycanvas.Scripts;

/// <summary>
/// The preview guard (preview-guard.js): keeps the client in the settings
/// page's preview frame from changing the browser's stored sign-in and from
/// ending the admin's session. It has to run before the client's own code,
/// so File Transformation puts it at the top of index.html's head; the
/// settings page fetches it (GET /Jellycanvas/PreviewGuard.js) for when File
/// Transformation is not there.
/// </summary>
public static class PreviewGuard
{
    /// <summary>The attribute that marks the inline tag (and makes adding it idempotent).</summary>
    public const string Marker = "data-jellycanvas-preview-guard";

    private static readonly Regex HeadOpen = new(@"<head(?:\s[^>]*)?>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    // Anchored (\G) where the head's tag ends: only a charset that comes first.
    private static readonly Regex Charset = new(@"\G\s*<meta\s+charset\s*=\s*[""']?[\w-]+[""']?\s*/?>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Lazy<string> SourceText = new(() => EmbeddedText.Read("preview-guard.js"));

    private static readonly Lazy<string> TagText = new(() => "<script " + Marker + "=\"1\">" + Compact(SourceText.Value) + "</script>");

    /// <summary>Gets the script itself.</summary>
    public static string Source => SourceText.Value;

    /// <summary>Gets the inline tag for index.html: the script without its comment lines and indentation (it travels in every index.html).</summary>
    public static string Tag => TagText.Value;

    /// <summary>
    /// Puts the tag into the document's head - after a leading
    /// &lt;meta charset&gt; (which has to stay within the first 1024 bytes),
    /// before anything else; once.
    /// </summary>
    /// <param name="html">index.html.</param>
    /// <returns>index.html with the guard.</returns>
    public static string InsertInto(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        if (html.Contains(Marker, StringComparison.Ordinal))
        {
            return html;
        }

        var head = HeadOpen.Match(html);
        if (!head.Success)
        {
            return html;
        }

        var at = head.Index + head.Length;
        var charset = Charset.Match(html, at);
        if (charset.Success)
        {
            at = charset.Index + charset.Length;
        }

        return html.Insert(at, Tag);
    }

    private static string Compact(string source)
        => string.Join(
            "\n",
            source.Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith("//", StringComparison.Ordinal)));
}
