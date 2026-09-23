using System;
using Jellyfin.Plugin.Jellycanvas.Configuration;
using Jellyfin.Plugin.Jellycanvas.Theme;
using Xunit;

namespace Jellyfin.Plugin.Jellycanvas.Tests;

/// <summary>
/// CssBuilder is a pure function: settings in, text out. The tests ask about
/// what would break most easily during edits - the markers, the @import
/// order, derived colors, and that every option really shows up in the CSS.
/// </summary>
public class CssBuilderTests
{
    [Fact]
    public void Default_settings_produce_a_marked_block()
    {
        var css = CssBuilder.Build(new PluginConfiguration());

        Assert.StartsWith(CssBuilder.StartMarker, css, StringComparison.Ordinal);
        Assert.EndsWith(CssBuilder.EndMarker + Environment.NewLine, css, StringComparison.Ordinal);
        Assert.Contains("--jf-palette-primary-main: #00a4dc !important", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Font_import_comes_before_any_rule()
    {
        var cfg = new PluginConfiguration { Typography = new TypographySettings { Family = FontFamily.Inter } };

        var css = CssBuilder.Build(cfg);

        var import = css.IndexOf("@import", StringComparison.Ordinal);
        var firstRule = css.IndexOf('{', StringComparison.Ordinal);
        Assert.True(import >= 0, "expected an @import for Inter");
        Assert.True(import < firstRule, "@import must precede the first rule, otherwise browsers ignore it");
        Assert.Contains("font-family: 'Inter', sans-serif !important", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Google_import_is_skipped_when_disabled()
    {
        var cfg = new PluginConfiguration { Typography = new TypographySettings { Family = FontFamily.Inter, LoadFromGoogle = false } };

        Assert.DoesNotContain("@import", CssBuilder.Build(cfg), StringComparison.Ordinal);
    }

    [Fact]
    public void Light_accent_gets_dark_contrast_text()
    {
        var cfg = new PluginConfiguration { Colors = new ColorSettings { Accent = "#ffee58" } };

        var css = CssBuilder.Build(cfg);

        Assert.Contains("--jf-palette-primary-contrastText: rgba(0, 0, 0, 0.87)", css, StringComparison.Ordinal);
        Assert.Contains("--jf-palette-primary-mainChannel: 255 238 88", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Invalid_color_falls_back_to_default_instead_of_throwing()
    {
        var cfg = new PluginConfiguration { Colors = new ColorSettings { Accent = "not a color" } };

        var css = CssBuilder.Build(cfg);

        Assert.Contains("--jf-palette-primary-main: #00a4dc", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Glass_header_uses_backdrop_filter_with_chosen_blur()
    {
        var cfg = new PluginConfiguration { Header = new HeaderSettings { Style = SurfaceStyle.Glass, Blur = 22, Opacity = 60 } };

        var css = CssBuilder.Build(cfg);

        Assert.Contains("backdrop-filter: blur(22px)", css, StringComparison.Ordinal);
        Assert.Contains("rgba(32, 32, 32, 0.6)", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Dark_only_scopes_every_selector()
    {
        var cfg = new PluginConfiguration { ApplyTo = ApplyTo.DarkOnly, Cards = new CardSettings { Hover = CardHover.Lift } };

        var css = CssBuilder.Build(cfg);

        Assert.Contains("html[data-theme]:not([data-theme=\"light\"]) {", css, StringComparison.Ordinal);
        Assert.Contains("html:not([data-theme=\"light\"]):not(.layout-tv) .card-hoverable:hover", css, StringComparison.Ordinal);
        Assert.Contains("html:not([data-theme=\"light\"]) header.MuiAppBar-root", css, StringComparison.Ordinal);
        // "html:not(...) html" is html under html - it can never match.
        Assert.DoesNotContain("html:not([data-theme=\"light\"]) html", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Decimal_separator_is_a_dot_regardless_of_culture()
    {
        // A Czech locale writes 0,5 - in CSS that would be an error.
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("cs-CZ");
            var cfg = new PluginConfiguration { Backdrop = new BackdropSettings { Dim = 35 }, Cards = new CardSettings { Spacing = 150 } };

            var css = CssBuilder.Build(cfg);

            Assert.Contains("opacity: 0.35 !important", css, StringComparison.Ordinal);
            Assert.Contains("margin: 0.9em !important", css, StringComparison.Ordinal);
            Assert.DoesNotMatch(@"\d,\d", css);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Extra_css_is_appended_inside_the_block()
    {
        var cfg = new PluginConfiguration { ExtraCss = ".foo { color: red; }" };

        var css = CssBuilder.Build(cfg);

        var extra = css.IndexOf(".foo { color: red; }", StringComparison.Ordinal);
        var end = css.IndexOf(CssBuilder.EndMarker, StringComparison.Ordinal);
        Assert.True(extra > 0 && extra < end);
    }

    [Fact]
    public void Every_preset_builds()
    {
        foreach (var preset in Presets.All)
        {
            var css = CssBuilder.Build(preset.Settings);
            Assert.Contains(CssBuilder.EndMarker, css, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Sections_layout_makes_the_bar_transparent_and_styles_the_groups()
    {
        var cfg = new PluginConfiguration { Header = new HeaderSettings { Layout = HeaderLayout.Sections, Style = SurfaceStyle.Glass, Opacity = 60, SectionRadius = 999 } };

        var css = CssBuilder.Build(cfg);

        Assert.Contains("html header.MuiAppBar-root, html .skinHeader-withBackground, html .skinHeader.semiTransparent { background: transparent !important;", css, StringComparison.Ordinal);
        Assert.Contains(".MuiToolbar-root:first-child > .MuiStack-root, html header.MuiAppBar-root .MuiToolbar-root:first-child > .MuiBox-root { background-color: rgba(32, 32, 32, 0.6) !important;", css, StringComparison.Ordinal);
        Assert.Contains("border-radius: 999px !important", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Sidebar_layout_moves_the_bar_left_and_offsets_main()
    {
        var cfg = new PluginConfiguration { Header = new HeaderSettings { Layout = HeaderLayout.Sidebar, SidebarWidth = 240 } };

        var css = CssBuilder.Build(cfg);

        Assert.Contains("header.MuiAppBar-root { top: 0px !important; left: 0px !important; bottom: 0px !important; right: auto !important; width: 240px !important;", css, StringComparison.Ordinal);
        Assert.Contains("header.MuiAppBar-root + div { display: none !important; }", css, StringComparison.Ordinal);
        Assert.Contains("header.MuiAppBar-root ~ main { margin-left: calc(240px + 0px + 0px) !important; width: calc(100% - 240px - 0px - 0px) !important;", css, StringComparison.Ordinal);
        Assert.Contains("html:not(.layout-mobile):not(.layout-tv):not(:has(#loginPage:not(.hide)))", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Info_bar_text_is_escaped_and_pushes_pages_down()
    {
        var cfg = new PluginConfiguration { InfoBar = new InfoBarSettings { Enabled = true, Text = "Say \"hi\" \\ now", Height = 40 } };

        var css = CssBuilder.Build(cfg);

        Assert.Contains("header.MuiAppBar-root::after { content: \"Say \\\"hi\\\" \\\\ now\";", css, StringComparison.Ordinal);
        Assert.Contains("{ --jellycanvas-info: 40px; }", css, StringComparison.Ordinal);
        Assert.Contains("main .mainAnimatedPage { top: var(--jellycanvas-info, 0px) !important; }", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Info_bar_with_sidebar_sits_above_the_content_and_shifts_the_second_toolbar()
    {
        var cfg = new PluginConfiguration { Header = new HeaderSettings { Layout = HeaderLayout.Sidebar }, InfoBar = new InfoBarSettings { Enabled = true, Text = "Hello", Height = 30 } };

        var css = CssBuilder.Build(cfg);

        Assert.Contains("main::before { content: \"Hello\";", css, StringComparison.Ordinal);
        Assert.Contains("{ --jellycanvas-info: 30px; }", css, StringComparison.Ordinal);
        Assert.Contains(".MuiToolbar-root:nth-child(2) { position: fixed; top: var(--jellycanvas-info, 0px);", css, StringComparison.Ordinal);
        Assert.Contains("~ main .mainAnimatedPage { top: calc(56px + var(--jellycanvas-info, 0px)) !important; }", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Blur_applies_to_any_translucent_bar_not_only_glass()
    {
        var solid = new PluginConfiguration { Header = new HeaderSettings { Style = SurfaceStyle.Solid, Opacity = 70, Blur = 12 } };
        var opaque = new PluginConfiguration { Header = new HeaderSettings { Style = SurfaceStyle.Solid, Opacity = 100, Blur = 12 } };

        Assert.Contains("backdrop-filter: blur(12px)", CssBuilder.Build(solid), StringComparison.Ordinal);
        Assert.DoesNotContain("backdrop-filter: blur(12px)", CssBuilder.Build(opaque), StringComparison.Ordinal);
    }

    [Fact]
    public void Custom_logo_url_is_quoted_and_escaped()
    {
        var cfg = new PluginConfiguration { Header = new HeaderSettings { Logo = LogoImage.Custom, LogoUrl = "https://x/y.png\") } body { display:none" } };

        var css = CssBuilder.Build(cfg);

        Assert.Contains("url(\"https://x/y.png\\\") } body { display:none\")", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Custom_google_font_is_imported_by_name()
    {
        var cfg = new PluginConfiguration { Typography = new TypographySettings { Family = FontFamily.Custom, CustomFamily = "Caacupé One" } };

        var css = CssBuilder.Build(cfg);

        Assert.Contains("family=Caacup%C3%A9+One&display=swap", css, StringComparison.Ordinal);
        Assert.Contains("font-family: \"Caacupé One\", sans-serif !important", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Random_backdrop_sets_the_page_background_variable()
    {
        var cfg = new PluginConfiguration { Backdrop = new BackdropSettings { Mode = BackdropMode.RandomLibrary, Blur = 20, Dim = 50 } };

        var css = CssBuilder.Build(cfg);

        Assert.Contains("html .backgroundContainer { background-image: linear-gradient(rgba(16, 16, 16, 0.5), rgba(16, 16, 16, 0.5)), url(\"../Jellycanvas/Backdrop\") !important", css, StringComparison.Ordinal);
        Assert.Contains("html .backgroundContainer { filter: blur(20px)", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Played_dimmed_hides_the_badge_and_fades_the_poster()
    {
        var cfg = new PluginConfiguration { Cards = new CardSettings { Played = PlayedStyle.Dimmed } };

        var css = CssBuilder.Build(cfg);

        Assert.Contains("html .playedIndicator { display: none !important; }", css, StringComparison.Ordinal);
        Assert.Contains(".card:has(.playedIndicator) .cardImageContainer { opacity: 0.45 !important;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Backdrop_rotation_builds_two_alternating_layers()
    {
        var cfg = new PluginConfiguration { Backdrop = new BackdropSettings { Mode = BackdropMode.RandomLibrary, RotateSeconds = 10 } };

        var css = CssBuilder.Build(cfg);

        Assert.Contains("html .backgroundContainer::before {", css, StringComparison.Ordinal);
        Assert.Contains("html .backgroundContainer::after {", css, StringComparison.Ordinal);
        Assert.Contains("jellycanvas-fade-a 20s linear infinite, jellycanvas-images-a 60s step-end infinite", css, StringComparison.Ordinal);
        Assert.Contains("url(\"../Jellycanvas/Backdrop?n=6\")", css, StringComparison.Ordinal);
        Assert.Contains("25% { background-image", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Play_button_options_produce_their_rules()
    {
        var cfg = new PluginConfiguration { Buttons = new ButtonSettings { Play = PlayStyle.Outline, PlayColor = "#ff0000", PlayRadius = 999, PlayLabel = true, DetailScale = 120, IconRadius = 8 } };

        var css = CssBuilder.Build(cfg);

        Assert.Contains("html .detailButton.btnPlay { background: transparent !important; box-shadow: inset 0 0 0 2px #ff0000 !important; color: #ff0000 !important;", css, StringComparison.Ordinal);
        Assert.Contains("border-radius: 999px !important; }", css, StringComparison.Ordinal);
        Assert.Contains("html .detailButton.btnPlay::after { content: attr(title);", css, StringComparison.Ordinal);
        Assert.Contains("html .mainDetailButtons { font-size: 120% !important; }", css, StringComparison.Ordinal);
        Assert.Contains("html .paper-icon-button-light, html .MuiIconButton-root, html .cardOverlayButton { border-radius: 8px !important; }", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Top_bar_slots_order_the_groups_with_auto_margins()
    {
        var cfg = new PluginConfiguration { Header = new HeaderSettings { SlotLeft = "user", SlotCenter = "nav", SlotRight = "icons" } };

        var css = CssBuilder.Build(cfg);

        Assert.Contains(":has([aria-controls=\"app-user-menu\"]) { order: 1; }", css, StringComparison.Ordinal);
        Assert.Contains("> .MuiStack-root { order: 10; margin-left: auto !important; margin-right: auto !important; }", css, StringComparison.Ordinal);
        Assert.Contains(":not(:has([aria-controls=\"app-user-menu\"])) { order: 20; margin-left: auto !important; }", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Sidebar_slots_use_vertical_margins_and_collapsible_narrows_the_bar()
    {
        var cfg = new PluginConfiguration { Header = new HeaderSettings { Layout = HeaderLayout.Sidebar, SidebarCollapsible = true, SidebarCollapsedWidth = 60, SlotLeft = "nav", SlotCenter = "user", SlotRight = "icons" } };

        var css = CssBuilder.Build(cfg);

        Assert.Contains("width: 60px !important;", css, StringComparison.Ordinal);
        Assert.Contains(":has(.MuiToolbar-root:first-child :focus-visible), html:not(.layout-mobile):not(.layout-tv):not(:has(#loginPage:not(.hide))):has(#app-user-menu", css, StringComparison.Ordinal);
        // an open header menu keeps the bar out
        Assert.Contains(":has(#app-user-menu:not(.MuiModal-hidden), #app-sync-play-menu:not(.MuiModal-hidden), #app-remote-play-menu:not(.MuiModal-hidden)) header.MuiAppBar-root { width: 220px !important; transition-delay: 0s; }", css, StringComparison.Ordinal);
        // the library row is clipped under the slid-out bar
        Assert.Contains(".MuiToolbar-root:nth-child(2) { clip-path: inset(0 0 0 calc(220px - 60px)); }", css, StringComparison.Ordinal);
        Assert.Contains(":has([aria-controls=\"app-user-menu\"]) { order: 10; margin-top: auto !important; margin-bottom: auto !important; }", css, StringComparison.Ordinal);
        Assert.Contains(":not(:has([aria-controls=\"app-user-menu\"])) { order: 20; margin-top: auto !important; }", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Login_gradient_uses_chosen_colors_angle_and_opacity()
    {
        var cfg = new PluginConfiguration { Login = new LoginSettings { GradientBackground = true, GradientFrom = "#102030", GradientTo = "#ff0000", GradientAngle = 45, GradientOpacity = 60 } };

        var css = CssBuilder.Build(cfg);

        Assert.Contains("html #loginPage::before { content: ''; position: fixed; top: 0; right: 0; bottom: 0; left: 0; z-index: -1; background: linear-gradient(45deg, rgba(16, 32, 48, 0.6) 0%, rgba(255, 0, 0, 0.6) 100%); }", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Login_fields_options_produce_their_rules()
    {
        var cfg = new PluginConfiguration { Login = new LoginSettings { FormWidth = 420, Inputs = LoginInputStyle.Glass, InputRadius = 20, InputScale = 130, HideQuickConnect = true, HideTitle = true } };

        var css = CssBuilder.Build(cfg);

        Assert.Contains("#loginPage .manualLoginForm, html #loginPage .readOnlyContent { max-width: 420px !important;", css, StringComparison.Ordinal);
        Assert.Contains("html #loginPage .emby-input { background: rgba(255, 255, 255, 0.1) !important;", css, StringComparison.Ordinal);
        Assert.Contains("#loginPage .emby-input, #loginPage .emby-button { border-radius: 20px !important; }", css, StringComparison.Ordinal);
        Assert.Contains("padding-top: 0.52em !important;", css, StringComparison.Ordinal);
        Assert.Contains("html #loginPage .btnQuick { display: none !important; }", css, StringComparison.Ordinal);
        Assert.Contains("html #loginPage h1.sectionTitle", css, StringComparison.Ordinal);
        Assert.Contains("html:has(#loginPage:not(.hide)) header.MuiAppBar-root { background: transparent !important;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Login_heading_can_be_replaced()
    {
        var cfg = new PluginConfiguration { Login = new LoginSettings { Title = "Welcome \"home\"" } };

        var css = CssBuilder.Build(cfg);

        Assert.Contains("h1.sectionTitle, html #loginPage .visualLoginForm > h1 { font-size: 0 !important; }", css, StringComparison.Ordinal);
        Assert.Contains("h1.sectionTitle::after, html #loginPage .visualLoginForm > h1::after { content: \"Welcome \\\"home\\\"\"; font-size: 1.6rem;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Brutalist_bar_has_a_thick_border_and_a_hard_shadow()
    {
        var cfg = new PluginConfiguration { Header = new HeaderSettings { Style = SurfaceStyle.NeoBrutalism, Shadow = true } };

        var css = CssBuilder.Build(cfg);

        Assert.Contains("background-color: #00a4dc !important; background-image: none !important; box-shadow: 4px 4px 0 #000000 !important; border: 3px solid #000000 !important;", css, StringComparison.Ordinal);
        Assert.Contains(".MuiButton-colorPrimary { text-decoration: underline !important;", css, StringComparison.Ordinal);
        // The style's own shadow wins over the generic drop shadow.
        Assert.DoesNotContain("0 6px 24px rgba(0, 0, 0, 0.35)", css, StringComparison.Ordinal);
        Assert.Contains("border-bottom: 3px solid #000000;", css, StringComparison.Ordinal);
        // The content moves down by the inset and the shadow.
        Assert.Contains("header.MuiAppBar-root + div { padding-bottom: 22px !important; }", css, StringComparison.Ordinal);
        // Brutalism detaches the bar from the edges so the frame and shadow are visible.
        Assert.Contains("left: 12px !important; right: 12px !important; top: 8px !important;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Sidebar_publishes_its_resting_edge_for_the_script()
    {
        var cfg = new PluginConfiguration { Header = new HeaderSettings { Layout = HeaderLayout.Sidebar, SidebarCollapsible = true, SidebarCollapsedWidth = 64, Floating = true } };

        var css = CssBuilder.Build(cfg);

        Assert.Contains("{ --jellycanvas-sidebar-edge: 76px; }", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Info_bar_next_to_a_collapsible_sidebar_starts_at_its_resting_width()
    {
        var cfg = new PluginConfiguration
        {
            Header = new HeaderSettings { Layout = HeaderLayout.Sidebar, SidebarCollapsible = true, SidebarCollapsedWidth = 64 },
            InfoBar = new InfoBarSettings { Enabled = true, Text = "Hello", Position = InfoBarPosition.Top, Height = 36, Closable = true },
        };

        var css = CssBuilder.Build(cfg);

        Assert.Contains("main::before { content: \"Hello\";", css, StringComparison.Ordinal);
        Assert.Contains("left: calc(64px + 0px + 0px + 0px); right: 0px; z-index: 3; }", css, StringComparison.Ordinal);
        Assert.Contains("{ --jellycanvas-info: 36px; }", css, StringComparison.Ordinal);
        Assert.Contains("html.jellycanvas-infobar-closed { --jellycanvas-info: 0px; }", css, StringComparison.Ordinal);
        Assert.Contains("padding: 0.3em 2.6em 0.3em 1em;", css, StringComparison.Ordinal);
        // the library row and the pages read the variable, not a literal
        Assert.Contains(".MuiToolbar-root:nth-child(2) { position: fixed; top: var(--jellycanvas-info, 0px);", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Library_row_can_be_styled_and_hidden()
    {
        var css = CssBuilder.Build(new PluginConfiguration { Header = new HeaderSettings { Layout = HeaderLayout.Sidebar, LibraryRow = LibraryRowStyle.Glass, LibraryRowColor = "#123456", LibraryRowOpacity = 60, LibraryRowHeight = 40, LibraryRowRadius = 12 } });

        Assert.Contains(".MuiToolbar-root:nth-child(2) { background-color: rgba(18, 52, 86, 0.6) !important;", css, StringComparison.Ordinal);
        // 40px asked, 44px is the least the controls fit in
        Assert.Contains(".MuiToolbar-root:nth-child(2) { min-height: 44px !important; height: 44px !important; padding-top: 0 !important;", css, StringComparison.Ordinal);
        Assert.Contains("left: calc(220px + 0px + 10px) !important; right: 10px !important;", css, StringComparison.Ordinal);
        // under a top bar the row simply shares the bar's look
        // Under a top bar the row is styled too (it used to be sidebar-only).
        Assert.Contains("html header.MuiAppBar-root .MuiToolbar-root:nth-child(2) { background-color", CssBuilder.Build(new PluginConfiguration { Header = new HeaderSettings { LibraryRow = LibraryRowStyle.Glass } }), StringComparison.Ordinal);

        // Islands bar + "same as bar": the surface goes around the groups, the row stays clear.
        var islands = CssBuilder.Build(new PluginConfiguration { Header = new HeaderSettings { Layout = HeaderLayout.Sections, Style = SurfaceStyle.Solid } });
        Assert.Contains(".MuiToolbar-root:nth-child(2) { background: transparent !important;", islands, StringComparison.Ordinal);
        Assert.Contains(".MuiToolbar-root:nth-child(2) > .MuiButton-root, html header.MuiAppBar-root .MuiToolbar-root:nth-child(2) > .MuiBox-root:has(.MuiChip-root), html header.MuiAppBar-root .MuiToolbar-root:nth-child(2) .MuiStack-root > .MuiBox-root, html header.MuiAppBar-root .MuiToolbar-root:nth-child(2) .MuiStack-root > .MuiButtonGroup-root {", islands, StringComparison.Ordinal);

        var parts = CssBuilder.Build(new PluginConfiguration { Header = new HeaderSettings { LibraryRowHideSort = true, LibraryRowHidePaging = true } });
        Assert.Contains("button:has(svg[data-testid=\"SortByAlphaIcon\"]), html header.MuiAppBar-root .MuiToolbar-root:nth-child(2) .MuiButtonGroup-root:has(svg[data-testid=\"NavigateNextIcon\"]) { display: none !important; }", parts, StringComparison.Ordinal);

        var hidden = CssBuilder.Build(new PluginConfiguration { Header = new HeaderSettings { Layout = HeaderLayout.Sidebar, LibraryRow = LibraryRowStyle.Hidden } });

        Assert.Contains(".MuiToolbar-root:nth-child(2) { display: none !important; }", hidden, StringComparison.Ordinal);
        // with the sidebar the hidden row gives its 52px back to the page
        Assert.Contains(":has(.MuiToolbar-root:nth-child(2)) ~ main .mainAnimatedPage { top: var(--jellycanvas-info, 0px) !important; }", hidden, StringComparison.Ordinal);
    }

    [Fact]
    public void Ribbon_and_up_next_take_their_own_surfaces()
    {
        var cfg = new PluginConfiguration
        {
            Detail = new DetailSettings { Ribbon = RibbonStyle.Glass, RibbonColor = "#123456", RibbonOpacity = 50, RibbonBlur = 8 },
            Dialogs = new DialogSettings { Style = SurfaceStyle.Glass, Opacity = 70, Blur = 10, Radius = 12, UpNext = true },
        };

        var css = CssBuilder.Build(cfg);

        Assert.Contains("html .detailRibbon::before { content: ''; position: absolute; top: 0; right: 0; bottom: 0; left: 0; z-index: -1; box-sizing: border-box; pointer-events: none; background-color: rgba(18, 52, 86, 0.5) !important;", css, StringComparison.Ordinal);
        Assert.Contains("backdrop-filter: blur(8px)", css, StringComparison.Ordinal);
        Assert.Contains("html .upNextContainer { background-color: rgba(32, 32, 32, 0.7) !important;", css, StringComparison.Ordinal);
        Assert.Contains(".upNextDialog-button.btnStartNow { background: #00a4dc !important;", css, StringComparison.Ordinal);

        // the old checkbox still means "transparent"
        var legacy = CssBuilder.Build(new PluginConfiguration { Detail = new DetailSettings { TransparentRibbon = true } });
        Assert.Contains("html .detailRibbon { background: transparent !important;", legacy, StringComparison.Ordinal);
    }

    [Fact]
    public void Cast_cards_can_be_circles()
    {
        var css = CssBuilder.Build(new PluginConfiguration { Detail = new DetailSettings { People = PeopleShape.Circle, PeopleScale = 80, PeopleRing = true } });

        Assert.Contains("html .personCard .cardPadder-overflowPortrait { padding-bottom: 100% !important; }", css, StringComparison.Ordinal);
        Assert.Contains("html .personCard .cardOverlayContainer { border-radius: 50% !important; }", css, StringComparison.Ordinal);
        Assert.Contains("html .personCard { font-size: 80% !important; }", css, StringComparison.Ordinal);
        Assert.Contains("inset 0 0 0 3px #00a4dc", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Sidebar_keeps_the_backdrop_filter_off_the_header()
    {
        var css = CssBuilder.Build(new PluginConfiguration { Header = new HeaderSettings { Layout = HeaderLayout.Sidebar, Style = SurfaceStyle.Glass, Opacity = 60, Blur = 18 } });

        // the header itself: no fill and no filter (it would swallow the fixed library row)
        Assert.Contains("header.MuiAppBar-root { background-color: transparent !important; background-image: none !important; backdrop-filter: none !important;", css, StringComparison.Ordinal);
        // the column inside carries the glass
        Assert.Contains(".MuiToolbar-root:first-child { background-color: rgba(32, 32, 32, 0.6) !important; background-image: none !important; box-shadow: none !important; backdrop-filter: blur(18px)", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Fill_progress_tints_the_poster()
    {
        var css = CssBuilder.Build(new PluginConfiguration { Cards = new CardSettings { Progress = ProgressStyle.Fill, ProgressFillOpacity = 40 } });

        Assert.Contains("html .itemProgressBarForeground { height: 100% !important; background: rgba(0, 164, 220, 0.4) !important;", css, StringComparison.Ordinal);
        Assert.Contains("html .itemLinearProgress { position: absolute !important; top: 0 !important;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Detail_blocks_become_chips_and_sections_get_titles()
    {
        var css = CssBuilder.Build(new PluginConfiguration { Detail = new DetailSettings { Tags = DetailBlockStyle.Chips, Genres = DetailBlockStyle.Hidden, TrackSelections = DetailBlockStyle.AccentChips, SectionTitles = SectionTitleStyle.AccentLine, HideSimilar = true } });

        Assert.Contains("html #itemDetailPage .itemTags a { font-size: 0.85rem !important; background: rgba(255, 255, 255, 0.1) !important;", css, StringComparison.Ordinal);
        Assert.Contains("html #itemDetailPage .itemGenres { display: none !important; }", css, StringComparison.Ordinal);
        Assert.Contains(".trackSelections .emby-select-withcolor { background: rgba(0, 164, 220, 0.22) !important;", css, StringComparison.Ordinal);
        Assert.Contains(".detailVerticalSection .sectionTitle::after { content: \"\";", css, StringComparison.Ordinal);
        Assert.Contains("html #similarCollapsible { display: none !important; }", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Detail_blocks_get_their_own_surfaces_and_chip_color()
    {
        var css = CssBuilder.Build(new PluginConfiguration { Detail = new DetailSettings { SelectorsBlock = DetailBlockSurface.Glass, SelectorsBlockColor = "#123456", BlockOpacity = 60, OverviewBlock = DetailBlockSurface.Solid, Tags = DetailBlockStyle.Chips, ChipColor = "#ff0000" } });

        Assert.Contains("html #itemDetailPage .trackSelections { background-color: rgba(18, 52, 86, 0.6) !important;", css, StringComparison.Ordinal);
        Assert.Contains("html #itemDetailPage .tagline, html #itemDetailPage .overview { background-color: rgba(32, 32, 32, 0.6) !important;", css, StringComparison.Ordinal);
        Assert.Contains("html #itemDetailPage .itemTags a { font-size: 0.85rem !important; background: #ff0000 !important; color: #ffffff !important;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Tv_layout_gets_the_bar_look_on_the_legacy_header()
    {
        var css = CssBuilder.Build(new PluginConfiguration { Header = new HeaderSettings { Style = SurfaceStyle.Glass, Opacity = 60, Blur = 12, Radius = 14, Nav = NavStyle.Pill, HideSearch = true }, InfoBar = new InfoBarSettings { Enabled = true, Text = "Hi" } });

        Assert.Contains("html.layout-tv .skinHeader { background-color: rgba(32, 32, 32, 0.6) !important;", css, StringComparison.Ordinal);
        Assert.Contains("html.layout-tv .skinHeader { border-radius: 0 0 14px 14px !important;", css, StringComparison.Ordinal);
        Assert.Contains("html.layout-tv .skinHeader .emby-tab-button.emby-tab-button-active { background: #00a4dc !important;", css, StringComparison.Ordinal);
        Assert.Contains("html.layout-tv .skinHeader .headerSearchButton { display: none !important; }", css, StringComparison.Ordinal);
        Assert.Contains("html.layout-tv .skinHeader .headerTop::after { content: \"Hi\";", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Login_buttons_get_their_own_style()
    {
        var cfg = new PluginConfiguration { Login = new LoginSettings { Buttons = LoginInputStyle.Outline } };

        var css = CssBuilder.Build(cfg);

        Assert.Contains("html #loginPage .readOnlyContent .emby-button { background: transparent !important; border: 2px solid rgba(255, 255, 255, 0.6) !important;", css, StringComparison.Ordinal);
        Assert.Contains("html #loginPage .manualLoginForm .emby-button.button-submit { border-color: #00a4dc !important; color: #00a4dc !important; }", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Neumorphic_dialogs_and_glow_login_form_get_their_shadows()
    {
        var cfg = new PluginConfiguration
        {
            Dialogs = new DialogSettings { Style = SurfaceStyle.Neumorphism },
            Login = new LoginSettings { Form = LoginFormStyle.Glowmorphism, Inputs = LoginInputStyle.NeoBrutalism },
        };

        var css = CssBuilder.Build(cfg);

        // Neumorphic dialogs take the page color: the look needs the same fill as the surroundings.
        Assert.Contains("html .MuiDialog-paper { background-color: #101010 !important; background-image: none !important; box-shadow: 8px 8px 18px #060606, -8px -8px 18px #3b3b3b !important;", css, StringComparison.Ordinal);
        Assert.Contains("#loginPage .visualLoginForm:not(.hide) { background-color: rgba(32, 32, 32, 0.55) !important;", css, StringComparison.Ordinal);
        Assert.Contains("0 0 24px rgba(0, 164, 220, 0.45)", css, StringComparison.Ordinal);
        Assert.Contains("html #loginPage .emby-input { background: #202020 !important; border: 3px solid #000000 !important; box-shadow: 3px 3px 0 #000000 !important; }", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Rounded_info_bar_is_inset_from_the_edges()
    {
        var cfg = new PluginConfiguration { InfoBar = new InfoBarSettings { Enabled = true, Text = "Hi", Radius = 12, Position = InfoBarPosition.Bottom } };

        var css = CssBuilder.Build(cfg);

        Assert.Contains("border-radius: 12px; position: fixed; left: 10px; right: 10px; bottom: 10px;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Flat_backdrops_paint_the_container_and_keep_the_item_layer()
    {
        var solid = CssBuilder.Build(new PluginConfiguration { Backdrop = new BackdropSettings { Mode = BackdropMode.Solid, Color = "#112233", ItemDetail = true } });
        Assert.Contains("html .backgroundContainer, html .backgroundContainer.withBackdrop { opacity: 1 !important; background: #112233 !important; }", solid, StringComparison.Ordinal);
        Assert.Contains("html .backgroundContainer > .jellycanvas-backdrop {", solid, StringComparison.Ordinal);
        Assert.DoesNotContain("jellycanvas-images-a", solid, StringComparison.Ordinal);

        var gradient = CssBuilder.Build(new PluginConfiguration { Backdrop = new BackdropSettings { Mode = BackdropMode.Gradient, GradientFrom = "#000000", GradientTo = "#ff0000", GradientAngle = 90, ItemDetail = false } });
        Assert.Contains("background: linear-gradient(90deg, #000000 0%, #ff0000 100%) !important; }", gradient, StringComparison.Ordinal);
        Assert.DoesNotContain(".jellycanvas-backdrop {", gradient, StringComparison.Ordinal);

        // the login page: a flat color between the image and the gradient
        var login = CssBuilder.Build(new PluginConfiguration { Login = new LoginSettings { SolidBackground = true, SolidColor = "#223344", GradientBackground = true } });
        Assert.Contains("html #loginPage::before { content: ''; position: fixed; top: 0; right: 0; bottom: 0; left: 0; z-index: -1; background: #223344; }", login, StringComparison.Ordinal);
        Assert.DoesNotContain("linear-gradient(160deg", login, StringComparison.Ordinal);
    }

    [Fact]
    public void Hidden_scrollbars_include_the_page_itself()
    {
        var css = CssBuilder.Build(new PluginConfiguration { Misc = new MiscSettings { HideScrollbars = true } });
        Assert.Contains("html, html * { scrollbar-width: none !important; }", css, StringComparison.Ordinal);
        Assert.Contains("html::-webkit-scrollbar, html *::-webkit-scrollbar { display: none !important;", css, StringComparison.Ordinal);
        // and with a device scope the root keeps its class, "html:not(...) *" alone would miss it
        var cfg = new PluginConfiguration { Misc = new MiscSettings { HideScrollbars = true } };
        cfg.Overrides.Tv = "{\"Cards\":{\"Radius\":9}}";
        Assert.Contains("html:not(.layout-tv), html:not(.layout-tv) * { scrollbar-width: none !important; }", CssBuilder.Build(cfg), StringComparison.Ordinal);
    }

    [Fact]
    public void Tv_still_backdrop_goes_while_a_video_plays()
    {
        var css = CssBuilder.Build(new PluginConfiguration { Backdrop = new BackdropSettings { Mode = BackdropMode.RandomLibrary, RotateSeconds = 30 }, Tv = new TvSettings { StaticBackdrop = true } });
        Assert.Contains("html.layout-tv .backgroundContainer { background-image: linear-gradient(", css, StringComparison.Ordinal);
        Assert.Contains("html.layout-tv.transparentDocument .backgroundContainer, html.layout-tv .backgroundContainer.backgroundContainer-transparent { background-image: none !important; }", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Detail_button_labels_stay_on_the_play_button_only_on_phones()
    {
        var css = CssBuilder.Build(new PluginConfiguration { Buttons = new ButtonSettings { DetailLabels = true } });
        Assert.Contains("html .detailButton::after { content: attr(title);", css, StringComparison.Ordinal);
        Assert.Contains("html.layout-mobile .detailButton:not(.btnPlay)::after { content: none; }", css, StringComparison.Ordinal);
        Assert.Contains("html.layout-mobile .mainDetailButtons { flex-wrap: wrap; row-gap: 0.4em; }", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Poster_play_button_gets_its_look_place_and_size()
    {
        // Nothing set: nothing written.
        Assert.DoesNotContain("the play button on posters", CssBuilder.Build(new PluginConfiguration()), StringComparison.Ordinal);

        var css = CssBuilder.Build(new PluginConfiguration { Cards = new CardSettings { PlayStyle = PlayButtonStyle.Accent, PlayRadius = 999, PlayPosition = PlayButtonPosition.BottomLeft, PlayScale = 120, PlayHideOnMobile = true } });
        Assert.Contains("> .cardOverlayButtonIcon { background: rgba(0, 164, 220, 1) !important; color: #ffffff !important; }", css, StringComparison.Ordinal);
        // every selector of the list gets :hover - a bare first selector would apply the hover look at rest
        Assert.Contains("button[data-action=\"play\"]:hover, .card .cardOverlayContainer > button[data-action=\"resume\"]:hover { transform: scale(1.68); }", css, StringComparison.Ordinal);
        Assert.DoesNotContain("button[data-action=\"play\"], .card .cardOverlayContainer > button[data-action=\"resume\"]:hover", css, StringComparison.Ordinal);
        Assert.Contains("border-radius: 999px !important;", css, StringComparison.Ordinal);
        Assert.Contains("button[data-action=\"resume\"] { bottom: 10px; left: 10px; top: auto; right: auto; margin: 0; }", css, StringComparison.Ordinal);
        Assert.Contains(".MuiButtonGroup-root { bottom: 10px; left: 10px; top: auto; right: auto; }", css, StringComparison.Ordinal);
        Assert.Contains("transform: scale(1.2); transform-origin: center;", css, StringComparison.Ordinal);
        Assert.Contains("transform: scale(1.2); transform-origin: bottom left;", css, StringComparison.Ordinal);
        Assert.Contains("html.layout-mobile .card .cardScalable > button.cardOverlayButton-br { display: none !important; }", css, StringComparison.Ordinal);
        // the legacy (home page) button carries the disc on its icon span
        Assert.Contains("button.cardOverlayButton-br[data-action=\"resume\"] > .cardOverlayButtonIcon { background: rgba(0, 164, 220, 1) !important;", css, StringComparison.Ordinal);
        Assert.Contains("button.cardOverlayButton-br[data-action=\"resume\"] { bottom: 0px; left: 0px; top: auto; right: auto; }", css, StringComparison.Ordinal);

        // A custom color wins over the style and brings its own icon color.
        var custom = CssBuilder.Build(new PluginConfiguration { Cards = new CardSettings { PlayStyle = PlayButtonStyle.Dark, PlayColor = "#ffffff", PlayOpacity = 50, PlayHoverColor = "#ff0000" } });
        Assert.Contains("{ background: rgba(255, 255, 255, 0.5) !important; color: rgba(0, 0, 0, 0.87) !important; }", custom, StringComparison.Ordinal);
        Assert.Contains(":hover > .cardOverlayButtonIcon { background: rgba(255, 0, 0, 0.5) !important; color: #ffffff !important; }", custom, StringComparison.Ordinal);
    }

    [Fact]
    public void Folder_overlay_buttons_hide_on_library_cards_only()
    {
        // A Seerr row card is typed "Series" too, but its button opens the title.
        var css = CssBuilder.Build(new PluginConfiguration { Cards = new CardSettings { HideOverlayButtonsOnFolders = true } });
        Assert.Contains(".card:not([data-jellycanvas])[data-type=\"Series\"] .cardOverlayContainer .cardOverlayButton", css, StringComparison.Ordinal);
        Assert.DoesNotContain(" .card[data-type=\"Series\"] .cardOverlayContainer .cardOverlayButton", css, StringComparison.Ordinal);

        // the same for "hide the hover buttons" altogether
        var all = CssBuilder.Build(new PluginConfiguration { Cards = new CardSettings { HideOverlayButtons = true } });
        Assert.Contains(".card:not([data-jellycanvas]) .cardOverlayContainer .cardOverlayButton", all, StringComparison.Ordinal);
        Assert.DoesNotContain(" .cardOverlayContainer .cardOverlayButton, html .cardOverlayContainer", all, StringComparison.Ordinal);
    }

    [Fact]
    public void Card_radius_is_set_directly_as_well_as_through_the_variable()
    {
        var css = CssBuilder.Build(new PluginConfiguration { Cards = new CardSettings { Radius = 18 } });

        Assert.Contains("--jf-card-borderRadius: 18px !important;", css, StringComparison.Ordinal);
        Assert.Contains(".cardImageContainer, .cardOverlayContainer, .visualCardBox, .card:focus .cardBox:not(.visualCardBox) .cardScalable { border-radius: 18px !important; }", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Theme_bridge_repeats_the_theme_stylesheets_variable_rules()
    {
        var css = CssBuilder.Build(new PluginConfiguration());
        Assert.Contains(".cardImageContainer, .cardOverlayContainer, .itemDetailImage, .paperList, .visualCardBox { border-radius: var(--jf-card-borderRadius, .2em); }", css, StringComparison.Ordinal);
        Assert.Contains(".backgroundContainer, .nowPlayingPlaylist, html { background-color: var(--jf-palette-background-default, #101010);", css, StringComparison.Ordinal);

        var dark = CssBuilder.Build(new PluginConfiguration { ApplyTo = ApplyTo.DarkOnly });
        Assert.Contains("html:not([data-theme=\"light\"]) .backgroundContainer, html:not([data-theme=\"light\"]) .nowPlayingPlaylist, html:not([data-theme=\"light\"]) {", dark, StringComparison.Ordinal);
        Assert.DoesNotContain("html:not([data-theme=\"light\"]) html", dark, StringComparison.Ordinal);
    }

    [Fact]
    public void Theme_bridge_repair_block_is_marked_and_unscoped()
    {
        var block = ThemeBridge.RepairBlock();
        Assert.StartsWith(Environment.NewLine + ThemeBridge.RepairMarker, block, StringComparison.Ordinal);
        Assert.EndsWith(ThemeBridge.RepairMarker + Environment.NewLine, block, StringComparison.Ordinal);
        Assert.Contains(Environment.NewLine + ".dialog { background-color: var(--jf-palette-background-default, #101010); }", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Corner_badge_stays_in_the_corner_on_rounded_cards()
    {
        var css = CssBuilder.Build(new PluginConfiguration { Cards = new CardSettings { Radius = 20, Played = PlayedStyle.CornerBadge } });
        Assert.DoesNotContain(".card .cardIndicators { top: calc(", css, StringComparison.Ordinal);
        Assert.Contains("html .cardIndicators, html .indicators, html .listItemIndicators { top: 0 !important; right: 0 !important; }", css, StringComparison.Ordinal);

        var badge = CssBuilder.Build(new PluginConfiguration { Cards = new CardSettings { Radius = 20, Played = PlayedStyle.Badge } });
        Assert.Contains(".card .cardIndicators { top: calc(0.225em + 6px)", badge, StringComparison.Ordinal);
    }

    [Fact]
    public void Device_copy_replaces_the_defaults_and_keeps_the_sidebar_off_the_tv()
    {
        // Islands on the web; the TV asks for a full bar - the web's island
        // surfaces must not reach the TV, whose copy has nothing to undo them with.
        var cfg = new PluginConfiguration { Header = new HeaderSettings { Layout = HeaderLayout.Sections } };
        cfg.Overrides.Tv = "{\"Header\":{\"Layout\":\"Full\"}}";
        var css = CssBuilder.Build(cfg);
        var tvPart = css.Substring(css.IndexOf("===== Tv:", StringComparison.Ordinal));
        Assert.Contains("html:not(.layout-tv) header.MuiAppBar-root .MuiToolbar-root:first-child > .MuiStack-root", css, StringComparison.Ordinal);
        Assert.Contains("> .MuiBox-root { background", css, StringComparison.Ordinal);
        Assert.DoesNotContain("> .MuiBox-root { background", tvPart, StringComparison.Ordinal);

        // The sidebar is a desktop layout: a TV with changes of its own gets the plain bar (with the slots) instead.
        var side = new PluginConfiguration { Header = new HeaderSettings { Layout = HeaderLayout.Sidebar } };
        side.Overrides.Tv = "{\"Cards\":{\"Radius\":30}}";
        var sideCss = CssBuilder.Build(side);
        var sideTv = sideCss.Substring(sideCss.IndexOf("===== Tv:", StringComparison.Ordinal));
        Assert.DoesNotContain("/* --- sidebar layout --- */", sideTv, StringComparison.Ordinal);
        Assert.DoesNotContain(":not(.layout-tv)", sideTv, StringComparison.Ordinal);
    }

    [Fact]
    public void Dashboard_panels_never_reach_the_rest_of_the_client()
    {
        var css = CssBuilder.Build(new PluginConfiguration { Dashboard = new DashboardSettings { PanelOpacity = 60, PanelRadius = 12, PanelBorder = true } });
        Assert.Contains("html body.dashboardDocument .MuiPaper-root:not(.MuiDrawer-paper)", css, StringComparison.Ordinal);
        Assert.Contains("border-radius: 12px !important;", css, StringComparison.Ordinal);
        // every rule of the section names the admin pages
        foreach (var line in css.Split(Environment.NewLine))
        {
            if (line.Contains(".MuiPaper-root:not(.MuiDrawer-paper)", StringComparison.Ordinal))
            {
                Assert.Contains("body.dashboardDocument", line, StringComparison.Ordinal);
            }
        }

        // even with nothing set, the admin pages are put back on the theme
        var plain = CssBuilder.Build(new PluginConfiguration());
        Assert.Contains("html body.dashboardDocument .MuiTableCell-root { border-color:", plain, StringComparison.Ordinal);
        Assert.DoesNotContain(".MuiPaper-root:not(.MuiDrawer-paper)", plain, StringComparison.Ordinal);
    }

    [Fact]
    public void The_dashboard_is_a_scope_of_its_own()
    {
        var cfg = new PluginConfiguration { Colors = new ColorSettings { Accent = "#00a4dc" } };
        cfg.Overrides.Dashboard = "{\"Colors\":{\"Accent\":\"#ff0000\"}}";
        var css = CssBuilder.Build(cfg);
        Assert.Contains("/* ===== Dashboard: the defaults with this device's changes ===== */", css, StringComparison.Ordinal);
        Assert.Contains("html:has(body.dashboardDocument), html[data-theme]:has(body.dashboardDocument) {", css, StringComparison.Ordinal);
        // and the defaults stay off the admin pages
        Assert.Contains("html:not(:has(body.dashboardDocument)) .cardImageContainer", css, StringComparison.Ordinal);
        Assert.DoesNotContain("html:has(body.dashboardDocument) html", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Device_overrides_add_a_scoped_copy_of_the_theme()
    {
        var cfg = new PluginConfiguration { Colors = new ColorSettings { Accent = "#00a4dc" } };
        cfg.Overrides.Tv = "{\"Colors\":{\"Accent\":\"#ff0000\"},\"Cards\":{\"Radius\":30}}";
        cfg.Overrides.Mobile = "{}";

        var css = CssBuilder.Build(cfg);

        // the defaults, kept off the TV (the TV has its own copy)
        Assert.Contains("--jf-palette-primary-main: #00a4dc !important;", css, StringComparison.Ordinal);
        Assert.Contains("html:not(.layout-tv) .cardImageContainer", css, StringComparison.Ordinal);
        Assert.DoesNotContain("html:not(.layout-tv):not(.layout-mobile)", css, StringComparison.Ordinal);
        // the TV copy, every selector under html.layout-tv, with the TV's own accent and radius
        Assert.Contains("/* ===== Tv: the defaults with this device's changes ===== */", css, StringComparison.Ordinal);
        Assert.Contains("html.layout-tv, html[data-theme].layout-tv {", css, StringComparison.Ordinal);
        Assert.Contains("--jf-palette-primary-main: #ff0000 !important;", css, StringComparison.Ordinal);
        Assert.Contains("html.layout-tv .cardImageContainer, html.layout-tv .cardOverlayContainer", css, StringComparison.Ordinal);
        Assert.Contains("border-radius: 30px !important;", css, StringComparison.Ordinal);
        // "html.layout-tv html ..." never appears (the prefix folds into html selectors)
        Assert.DoesNotContain("html.layout-tv html", css, StringComparison.Ordinal);
        // an empty document adds nothing
        Assert.DoesNotContain("===== Mobile", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Buttons_wear_the_looks_the_bars_have()
    {
        var css = CssBuilder.Build(new PluginConfiguration { Buttons = new ButtonSettings { Style = ButtonStyle.NeoBrutalism } });

        // Every kind of button the client has, including the rows the user
        // settings menu is made of.
        Assert.Contains("html .userPreferencesPage a.emby-button", css, StringComparison.Ordinal);
        Assert.Contains("border: 3px solid #000000 !important; box-shadow: 3px 3px 0 #000000 !important;", css, StringComparison.Ordinal);
        // The accented ones (submit, MUI contained) are built from the accent.
        Assert.Contains(".button-submit, html .MuiButton-contained { background: #00a4dc !important;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void The_shared_looks_reach_the_play_and_skip_buttons()
    {
        var cfg = new PluginConfiguration();
        cfg.Buttons.Play = PlayStyle.Glow;
        cfg.Cards.PlayStyle = PlayButtonStyle.NeoBrutalism;
        cfg.Player.Skip = SkipStyle.Claymorphism;

        var css = CssBuilder.Build(cfg);

        Assert.Contains("html .detailButton.btnPlay { background: #00a4dc !important;", css, StringComparison.Ordinal);
        Assert.Contains("box-shadow: 0 0 18px rgba(0, 164, 220, 0.55) !important;", css, StringComparison.Ordinal);
        Assert.Contains("box-shadow: 3px 3px 0 #000000 !important;", css, StringComparison.Ordinal);
        Assert.Contains(".skip-button { background: ", css, StringComparison.Ordinal);
        Assert.Contains("inset 3px 3px 7px", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Device_override_merge_keeps_the_server_side_parts()
    {
        var cfg = new PluginConfiguration();
        cfg.Scripts.Enabled = true;
        var merged = DeviceOverrides.Merge(cfg, "{\"Cards\":{\"Radius\":9},\"Scripts\":{\"Enabled\":false},\"Header\":{\"Style\":\"Glass\"}}");

        Assert.Equal(9, merged.Cards.Radius);
        Assert.Equal(SurfaceStyle.Glass, merged.Header.Style);
        Assert.True(merged.Scripts.Enabled);
        Assert.Same(cfg, DeviceOverrides.Merge(cfg, "not json"));
    }
}
