using System;
using Jellyfin.Plugin.Jellycanvas.Theme;
using Xunit;

namespace Jellyfin.Plugin.Jellycanvas.Tests;

/// <summary>
/// StripBlock is the only part of BrandingWriter with no server dependency,
/// and also the one where a bug would cost the admin their own CSS.
/// </summary>
public class BrandingWriterTests
{
    private static string Block(string body) => CssBuilder.StartMarker + "\n" + body + "\n" + CssBuilder.EndMarker;

    [Fact]
    public void Foreign_css_without_our_block_is_untouched()
    {
        Assert.Equal(".mine { color: red; }", BrandingWriter.StripBlock("  .mine { color: red; }\n"));
    }

    [Fact]
    public void Our_block_is_removed_and_the_rest_kept()
    {
        var css = Block(".ours { }") + "\n\n.theirs { }";

        Assert.Equal(".theirs { }", BrandingWriter.StripBlock(css));
    }

    [Fact]
    public void Css_before_and_after_the_block_survives()
    {
        var css = ".before { }\n" + Block(".ours { }") + "\n.after { }";

        Assert.Equal(".before { }" + Environment.NewLine + ".after { }", BrandingWriter.StripBlock(css));
    }

    [Fact]
    public void Missing_end_marker_cuts_to_the_end()
    {
        var css = ".before { }\n" + CssBuilder.StartMarker + "\n.broken {";

        Assert.Equal(".before { }", BrandingWriter.StripBlock(css));
    }

    [Fact]
    public void Empty_input_gives_empty_output()
    {
        Assert.Equal(string.Empty, BrandingWriter.StripBlock(string.Empty));
    }
}
