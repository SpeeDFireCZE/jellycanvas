using System;
using System.Linq;
using Jellyfin.Plugin.Jellycanvas.Configuration;
using Jellyfin.Plugin.Jellycanvas.Scripts;
using Xunit;

namespace Jellyfin.Plugin.Jellycanvas.Tests;

/// <summary>
/// The client script is a template with the configuration baked in; the
/// tests check the baking, not the browser behaviour.
/// </summary>
public class ScriptBuilderTests
{
    private static PluginConfiguration WithButton(string url, string icon = "playlist_add") => new()
    {
        Scripts = new ScriptSettings
        {
            Enabled = true,
            ToolbarButtons = { new ToolbarButton { Label = "Requests", Icon = icon, Url = url } },
        },
    };

    [Fact]
    public void Card_badges_go_into_the_script_with_known_ids_only()
    {
        var cfg = new PluginConfiguration
        {
            Scripts = new ScriptSettings { Enabled = true, CardBadges = new CardBadgeSettings { Enabled = true, TopLeft = "resolution, bogus, HDR", BottomLeft = "", BottomRight = "audio,subtitles,audio" } },
        };

        var js = ScriptBuilder.Build(cfg);

        Assert.Contains("\"corners\":{\"tl\":[\"resolution\",\"hdr\"],\"tr\":[],\"bl\":[],\"br\":[\"audio\",\"subtitles\"]}", js, StringComparison.Ordinal);
        Assert.Contains("\"style\":\"Dark\"", js, StringComparison.Ordinal);
        Assert.Contains("\"languages\":\"Flags\"", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Closable_info_bar_goes_into_the_script()
    {
        var cfg = new PluginConfiguration
        {
            Scripts = new ScriptSettings { Enabled = true },
            InfoBar = new InfoBarSettings { Enabled = true, Text = "Hello", Closable = true },
        };

        var js = ScriptBuilder.Build(cfg);

        Assert.Contains("\"infoBar\":{\"text\":\"Hello\"}", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Disabled_or_empty_gives_an_empty_script()
    {
        Assert.Equal(string.Empty, ScriptBuilder.Build(new PluginConfiguration()));
        Assert.Equal(string.Empty, ScriptBuilder.Build(WithButton(string.Empty)));
        var off = WithButton("https://x/");
        off.Scripts.Enabled = false;
        Assert.Equal(string.Empty, ScriptBuilder.Build(off));
    }

    [Fact]
    public void Configuration_is_baked_into_the_template()
    {
        var js = ScriptBuilder.Build(WithButton("https://requests.example/"));

        Assert.Contains("var CONFIG = {\"buttons\":[{\"id\":1,\"enabled\":true,\"label\":\"Requests\",\"icon\":\"playlist_add\",\"url\":\"https://requests.example/\"", js, StringComparison.Ordinal);
        Assert.Contains("\"showBeforeLogin\":false", js, StringComparison.Ordinal);
        Assert.Contains("window.__jellycanvasScript", js, StringComparison.Ordinal);
        Assert.DoesNotContain("JELLYCANVAS_CONFIG", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_end_tag_inside_a_value_cannot_close_the_script_element()
    {
        var js = ScriptBuilder.Build(WithButton("https://x/</script><script>alert(1)"));

        Assert.DoesNotContain("</script>", js, StringComparison.Ordinal);
        Assert.Contains("<\\/script>", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Invalid_icon_name_falls_back()
    {
        var js = ScriptBuilder.Build(WithButton("https://x/", "<img onerror=1>"));

        Assert.Contains("\"icon\":\"open_in_new\"", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Index_html_gets_the_script_tag_once()
    {
        var html = "<html><body><div></div></body></html>";

        var once = FileTransformation.IndexHtml(new PatchRequestPayload { Contents = html });
        var twice = FileTransformation.IndexHtml(new PatchRequestPayload { Contents = once });

        Assert.Contains("<script data-jellycanvas-script=\"1\" src=\"/Jellycanvas/Script.js?v=", once, StringComparison.Ordinal);
        Assert.Equal(once, twice);
        Assert.Single(twice.Split("data-jellycanvas-script").Skip(1));
    }

    [Fact]
    public void Slideshow_alone_produces_a_script_with_its_settings()
    {
        var cfg = new PluginConfiguration { Scripts = new ScriptSettings { Enabled = true, Slideshow = new SlideshowSettings { Enabled = true, Source = SlideshowSource.Genre, Filter = "Action", Count = 5 } } };

        var js = ScriptBuilder.Build(cfg);

        Assert.Contains("\"slideshow\":{\"source\":\"Genre\",\"filter\":\"Action\",\"types\":\"MoviesAndSeries\",\"count\":5", js, StringComparison.Ordinal);
        Assert.Contains("jellycanvasSlideshow", js, StringComparison.Ordinal);
    }
}
