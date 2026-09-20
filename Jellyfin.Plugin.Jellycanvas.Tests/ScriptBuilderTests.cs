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
        Assert.Contains("\"subtitleLanguages\":\"Codes\"", js, StringComparison.Ordinal);
    }

    [Fact]
    public void Rotating_backdrop_goes_into_the_script()
    {
        var cfg = new PluginConfiguration
        {
            Scripts = new ScriptSettings { Enabled = true },
            Backdrop = new BackdropSettings { Mode = BackdropMode.RandomLibrary, RotateSeconds = 20 },
        };

        Assert.Contains("\"backdrop\":{\"seconds\":20,\"detail\":false,\"tvStatic\":false}", ScriptBuilder.Build(cfg), StringComparison.Ordinal);
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

        Assert.Contains("\"infoBar\":{\"text\":\"Hello\",\"remember\":true}", js, StringComparison.Ordinal);
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
    public void Button_targets_are_web_addresses_or_client_paths_only()
    {
        // The script runs for every user: a target the browser would execute
        // rather than open is dropped, and a button without a target is no button.
        Assert.Equal(string.Empty, ScriptBuilder.Build(WithButton("javascript:alert(1)")));
        Assert.Equal(string.Empty, ScriptBuilder.Build(WithButton("data:text/html,hi")));
        Assert.Equal(string.Empty, ScriptBuilder.Build(WithButton(" JavaScript:void(0)")));
        Assert.Contains("\"url\":\"https://requests.example/\"", ScriptBuilder.Build(WithButton(" https://requests.example/ ")), StringComparison.Ordinal);
        Assert.Contains("\"url\":\"#/search?query=a:b\"", ScriptBuilder.Build(WithButton("#/search?query=a:b")), StringComparison.Ordinal);
        Assert.Contains("\"url\":\"/web/index.html#/home\"", ScriptBuilder.Build(WithButton("/web/index.html#/home")), StringComparison.Ordinal);
        Assert.Contains("\"url\":\"mailto:admin@example.com\"", ScriptBuilder.Build(WithButton("mailto:admin@example.com")), StringComparison.Ordinal);
    }

    [Fact]
    public void Seerr_links_open_through_a_button_only_when_it_can_show_a_page()
    {
        var cfg = new PluginConfiguration
        {
            Scripts = new ScriptSettings
            {
                Enabled = true,
                ToolbarButtons =
                {
                    new ToolbarButton { Label = "Tab", Url = "https://seerr.example/", Action = ButtonAction.NewTab },
                    new ToolbarButton { Label = "Seerr", Url = "https://seerr.example/", Action = ButtonAction.Overlay },
                },
            },
            Seerr = new SeerrSettings { Url = "https://seerr.example", ApiKey = "k", Rows = { new SeerrRow { Enabled = true } }, OpenWithButton = 2 },
        };
        Assert.Contains("\"seerrOpen\":2", ScriptBuilder.Build(cfg), StringComparison.Ordinal);

        cfg.Seerr.OpenWithButton = 1; // a new-tab button adds nothing
        Assert.Contains("\"seerrOpen\":0", ScriptBuilder.Build(cfg), StringComparison.Ordinal);
        cfg.Seerr.OpenWithButton = 9; // no such button
        Assert.Contains("\"seerrOpen\":0", ScriptBuilder.Build(cfg), StringComparison.Ordinal);
        cfg.Seerr.OpenWithButton = 2;
        cfg.Scripts.ToolbarButtons[1].Enabled = false;
        Assert.Contains("\"seerrOpen\":0", ScriptBuilder.Build(cfg), StringComparison.Ordinal);
    }

    [Fact]
    public void Script_stamp_follows_the_settings()
    {
        var a = new PluginConfiguration();
        var b = new PluginConfiguration { Scripts = new ScriptSettings { Enabled = true, Slideshow = new SlideshowSettings { Enabled = true } } };
        Assert.Equal(ScriptBuilder.Stamp(a), ScriptBuilder.Stamp(new PluginConfiguration()));
        Assert.NotEqual(ScriptBuilder.Stamp(a), ScriptBuilder.Stamp(b));
        Assert.Equal(10, ScriptBuilder.Stamp(b).Length);
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
        // the button's look and text travel with it; an empty icon stays empty (no icon), a bad one falls back
        Assert.Contains("\"buttonLabel\":\"\",\"buttonIcon\":\"info\",\"buttonStyle\":\"Accent\",\"buttonRadius\":-1,\"buttonScale\":100", js, StringComparison.Ordinal);
        cfg.Scripts.Slideshow.ButtonIcon = " ";
        cfg.Scripts.Slideshow.ButtonLabel = " More info ";
        cfg.Scripts.Slideshow.ButtonStyle = SlideshowButtonStyle.Glass;
        cfg.Scripts.Slideshow.Source = SlideshowSource.TopRated;
        var js2 = ScriptBuilder.Build(cfg);
        Assert.Contains("\"source\":\"TopRated\"", js2, StringComparison.Ordinal);
        Assert.Contains("\"buttonLabel\":\"More info\",\"buttonIcon\":\"\",\"buttonStyle\":\"Glass\"", js2, StringComparison.Ordinal);
    }
}
