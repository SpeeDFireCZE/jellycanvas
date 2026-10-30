using System;
using System.Linq;
using Jellyfin.Plugin.Jellycanvas.Scripts;
using Xunit;

namespace Jellyfin.Plugin.Jellycanvas.Tests;

/// <summary>
/// The preview guard: a script that has to run in the settings page's
/// preview frame before any of the client's own code (which reads the stored
/// sign-in as it starts). The browser side is checked by
/// test/tools/check_preview_signin.py.
/// </summary>
public class PreviewGuardTests
{
    private const string Index = "<!doctype html><html><head><meta charset=\"utf-8\"><script defer=\"defer\" src=\"main.jellyfin.bundle.js\"></script></head><body><div id=\"reactRoot\"></div></body></html>";

    [Fact]
    public void The_guard_comes_right_after_the_charset_before_any_script()
    {
        // The charset has to stay within the first 1024 bytes the browser
        // looks at; the guard has to run before the client's scripts.
        var html = FileTransformation.IndexHtml(new PatchRequestPayload { Contents = Index });

        Assert.StartsWith("<!doctype html><html><head><meta charset=\"utf-8\"><script data-jellycanvas-preview-guard=\"1\">", html, StringComparison.Ordinal);
        Assert.True(html.IndexOf("data-jellycanvas-preview-guard", StringComparison.Ordinal) < html.IndexOf("main.jellyfin.bundle.js", StringComparison.Ordinal));
    }

    [Fact]
    public void Without_a_charset_the_guard_opens_the_head()
    {
        var html = FileTransformation.IndexHtml(new PatchRequestPayload { Contents = "<html><head><title>J</title></head><body></body></html>" });

        Assert.StartsWith("<html><head><script data-jellycanvas-preview-guard=\"1\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_the_documents_head_gets_the_guard()
    {
        // Another plugin's inline script may hold "<head>" in a string.
        var html = FileTransformation.IndexHtml(new PatchRequestPayload { Contents = "<html><head></head><body><script>var t = '<head>';</script></body></html>" });

        Assert.Single(html.Split("data-jellycanvas-preview-guard").Skip(1));
        Assert.Contains("var t = '<head>';", html, StringComparison.Ordinal);
    }

    [Fact]
    public void A_page_without_a_body_end_gets_the_guard_once_too()
    {
        // (no </body>: the script tag cannot go in, so its marker does not stop a second pass)
        var once = FileTransformation.IndexHtml(new PatchRequestPayload { Contents = "<html><head></head>" });
        var twice = FileTransformation.IndexHtml(new PatchRequestPayload { Contents = once });

        Assert.Single(twice.Split("data-jellycanvas-preview-guard").Skip(1));
    }

    [Fact]
    public void The_guard_goes_in_once()
    {
        var once = FileTransformation.IndexHtml(new PatchRequestPayload { Contents = Index });
        var twice = FileTransformation.IndexHtml(new PatchRequestPayload { Contents = once });

        Assert.Equal(once, twice);
        Assert.Single(twice.Split("data-jellycanvas-preview-guard").Skip(1));
    }

    [Fact]
    public void A_header_element_is_no_head()
    {
        var html = FileTransformation.IndexHtml(new PatchRequestPayload { Contents = "<html><body><header>x</header></body></html>" });

        Assert.DoesNotContain("<header><script", html, StringComparison.Ordinal);
    }

    [Fact]
    public void The_guard_acts_only_in_the_preview_and_cannot_end_its_tag()
    {
        // Inlined into every client's index.html: it has to recognise the
        // preview frame itself, and "</script" inside would cut it short.
        Assert.Contains("data-jellycanvas-preview", PreviewGuard.Source, StringComparison.Ordinal);
        Assert.Contains("frameElement", PreviewGuard.Source, StringComparison.Ordinal);
        Assert.DoesNotContain("</script", PreviewGuard.Source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Every_client_gets_the_guard_without_its_comments()
    {
        // It travels in every index.html; the comments stay in the source.
        var lines = PreviewGuard.Tag.Split('\n');
        Assert.DoesNotContain(lines, l => l.TrimStart().StartsWith("//", StringComparison.Ordinal) || l != l.Trim());
        Assert.Contains("window.__jellycanvasPreviewGuard = true;", PreviewGuard.Tag, StringComparison.Ordinal);
        Assert.True(PreviewGuard.Tag.Length < PreviewGuard.Source.Length);
    }
}
