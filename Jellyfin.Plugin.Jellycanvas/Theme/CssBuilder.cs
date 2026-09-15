using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Jellyfin.Plugin.Jellycanvas.Configuration;

namespace Jellyfin.Plugin.Jellycanvas.Theme;

/// <summary>
/// The heart of the plugin: turns settings (<see cref="PluginConfiguration"/>)
/// into CSS text. It does nothing else - no saving, no touching the server.
/// That is what lets the same class serve the live preview and the final
/// write to Branding, and what makes it easy to unit test.
///
/// How it works inside jellyfin-web 12: almost every color is a CSS variable
/// <c>--jf-palette-*</c> that MUI sets on <c>html[data-theme="dark"]</c>.
/// Overriding those with higher specificity and <c>!important</c> recolors the
/// whole UI without knowing hundreds of selectors. The rest (glass, rounding,
/// card effects) are targeted rules on a few classes: the top bar is a MUI
/// component since version 12 (<c>header.MuiAppBar-root</c>); the side menu,
/// cards and buttons are still the long-lived <c>.mainDrawer</c>,
/// <c>.card*</c> and <c>.emby-button</c>.
///
/// When something stops matching in a newer Jellyfin, the first place to
/// look is the DOM of the real page (not the docs) - see the comments on the
/// individual sections.
/// </summary>
public static class CssBuilder
{
    /// <summary>Markers the block is written between in Branding. They tell our CSS apart from the admin's own.</summary>
    public const string StartMarker = "/* === JELLYCANVAS START - generated, do not edit; use the Jellycanvas plugin page === */";

    public const string EndMarker = "/* === JELLYCANVAS END === */";

    /// <summary>
    /// Relative URLs of the plugin's own endpoints. The CSS is embedded in the
    /// page at /web/, so "../Jellycanvas/..." resolves to the server root and
    /// keeps working behind a reverse proxy with a base path
    /// (/jellyfin/web/ → /jellyfin/Jellycanvas/...).
    /// </summary>
    public const string LogoUrl = "../Jellycanvas/Logo";

    public const string RandomBackdropUrl = "../Jellycanvas/Backdrop";

    /// <summary>How many random backdrops a rotation cycles through (must be even - two alternating layers).</summary>
    public const int RotationImages = 6;

    // Selectors shared across sections. One place = one fix when Jellyfin
    // renames something again.
    private const string AppBar = "html header.MuiAppBar-root";
    private const string Toolbar = AppBar + " .MuiToolbar-root";
    private const string LogoLink = Toolbar + " a[href=\"#/\"]";
    private const string NavLink = Toolbar + " .MuiStack-root > a.MuiButton-sizeMedium";

    public static string Build(PluginConfiguration c)
    {
        ArgumentNullException.ThrowIfNull(c);

        var sb = new StringBuilder();
        var ctx = new Context(c);

        sb.AppendLine(StartMarker);
        sb.AppendLine("/* Jellycanvas theme " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC */");

        // @import has to be the very first thing in a stylesheet, hence fonts first.
        AppendFontImport(sb, c.Typography);

        AppendPalette(sb, ctx);
        AppendTypography(sb, ctx);
        AppendHeader(sb, ctx);
        AppendLibraryRow(sb, ctx);
        AppendInfoBar(sb, ctx);
        AppendLogo(sb, ctx);
        AppendDrawer(sb, ctx);
        AppendCards(sb, ctx);
        AppendButtons(sb, ctx);
        AppendDialogs(sb, ctx);
        AppendBackdrop(sb, ctx);
        AppendDetail(sb, ctx);
        AppendLogin(sb, ctx);
        AppendMisc(sb, ctx);
        AppendTv(sb, ctx);
        AppendMobile(sb, ctx);

        if (!string.IsNullOrWhiteSpace(c.ExtraCss))
        {
            sb.AppendLine("/* --- extra CSS from the plugin page --- */");
            sb.AppendLine(c.ExtraCss.Trim());
        }

        sb.AppendLine(EndMarker);
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Sections. Each gets the StringBuilder and a context with the colors
    // already computed, and appends its lines. Their order is the CSS order.
    // ------------------------------------------------------------------

    private static void AppendFontImport(StringBuilder sb, TypographySettings t)
    {
        if (!t.LoadFromGoogle)
        {
            return;
        }

        // For the known fonts ask for the bold weights too; for a custom name
        // only the regular one - asking for a weight the font does not have
        // makes Google return an error and nothing loads at all.
        var family = t.Family switch
        {
            FontFamily.Inter => "Inter:wght@400;600;700",
            FontFamily.Roboto => "Roboto:wght@400;500;700",
            FontFamily.Poppins => "Poppins:wght@400;500;600;700",
            FontFamily.Nunito => "Nunito:wght@400;600;700",
            FontFamily.Custom when !string.IsNullOrWhiteSpace(t.CustomFamily) => Uri.EscapeDataString(t.CustomFamily.Trim()).Replace("%20", "+", StringComparison.Ordinal),
            _ => null,
        };

        if (family is not null)
        {
            sb.AppendLine($"@import url('https://fonts.googleapis.com/css2?family={family}&display=swap');");
        }
    }

    private static void AppendPalette(StringBuilder sb, Context x)
    {
        var accent = x.Accent;
        var text = x.Text;
        var surface = x.Surface;

        sb.AppendLine("/* --- palette: the CSS variables the whole UI is built from --- */");
        sb.AppendLine($"{x.Root} {{");
        Var(sb, "--jf-palette-primary-main", accent.Hex);
        Var(sb, "--jf-palette-primary-mainChannel", accent.Channel);
        Var(sb, "--jf-palette-primary-dark", accent.Darken(0.3).Hex);
        Var(sb, "--jf-palette-primary-darkChannel", accent.Darken(0.3).Channel);
        Var(sb, "--jf-palette-primary-light", accent.Lighten(0.2).Hex);
        Var(sb, "--jf-palette-primary-lightChannel", accent.Lighten(0.2).Channel);
        Var(sb, "--jf-palette-primary-contrastText", accent.ContrastText);
        Var(sb, "--jf-palette-secondary-main", x.Focus.Hex);
        Var(sb, "--jf-palette-secondary-mainChannel", x.Focus.Channel);
        Var(sb, "--jf-palette-secondary-contrastText", x.Focus.ContrastText);
        Var(sb, "--jf-palette-background-default", x.Background.Hex);
        Var(sb, "--jf-palette-background-defaultChannel", x.Background.Channel);
        Var(sb, "--jf-palette-background-paper", surface.Hex);
        Var(sb, "--jf-palette-background-paperChannel", surface.Channel);
        Var(sb, "--jf-palette-text-primary", text.Hex);
        Var(sb, "--jf-palette-text-primaryChannel", text.Channel);
        Var(sb, "--jf-palette-text-secondary", text.RgbaPercent(x.Config.Colors.SecondaryTextOpacity));
        Var(sb, "--jf-palette-divider", text.Rgba(0.12));
        Var(sb, "--jf-palette-action-hover", text.Rgba(0.08));
        Var(sb, "--jf-palette-action-focus", text.Rgba(0.12));
        Var(sb, "--jf-palette-FilledInput-bg", text.Rgba(0.09));
        Var(sb, "--jf-palette-FilledInput-borderColor", text.Rgba(0.09));
        Var(sb, "--jf-palette-Button-inheritContainedBg", surface.Mix(text, 0.13).Hex);
        Var(sb, "--jf-palette-Button-inheritContainedHoverBg", surface.Mix(text, 0.25).Hex);
        Var(sb, "--jf-palette-AppBar-defaultBg", x.HeaderColor.Hex);
        Var(sb, "--jf-palette-AppBar-transparentBg", x.Background.Rgba(0.4));
        Var(sb, "--jf-palette-SnackbarContent-bg", surface.Mix(text, 0.08).Hex);
        Var(sb, "--jf-palette-SnackbarContent-color", text.Hex);
        Var(sb, "--jf-card-borderRadius", Px(x.Config.Cards.Radius));
        sb.AppendLine("}");

        // A few places hard-code a color instead of using a variable - align them by hand.
        sb.AppendLine($"{x.P}.cardBox:not(.visualCardBox) .cardPadder {{ background-color: {surface.Hex} !important; }}");
        sb.AppendLine($"{x.P}.cardPadder .cardImageIcon {{ color: {surface.Darken(0.4).Hex} !important; }}");
        sb.AppendLine($"{x.P}* {{ scrollbar-color: {surface.Mix(text, 0.2).Hex} {x.Background.Hex}; }}");
        sb.AppendLine($"{x.P}::-webkit-scrollbar-track-piece {{ background-color: {x.Background.Hex}; }}");
        sb.AppendLine($"{x.P}::-webkit-scrollbar-thumb:horizontal, {x.P}::-webkit-scrollbar-thumb:vertical {{ background-color: {surface.Mix(text, 0.2).Hex}; }}");
    }

    private static void AppendTypography(StringBuilder sb, Context x)
    {
        var t = x.Config.Typography;
        var family = t.Family switch
        {
            FontFamily.Default => null,
            FontFamily.System => "system-ui, -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif",
            FontFamily.Custom => string.IsNullOrWhiteSpace(t.CustomFamily) ? null : $"'{t.CustomFamily.Trim().Replace("'", string.Empty, StringComparison.Ordinal)}', sans-serif",
            _ => $"'{t.Family}', sans-serif",
        };

        if (family is null && t.Scale == 100)
        {
            return;
        }

        sb.AppendLine("/* --- typography --- */");
        if (family is not null)
        {
            // html gets a font-family per language (the CJK Noto variants) and
            // MUI carries its own in the theme. Override both: html and every
            // component that sets font-family explicitly.
            sb.AppendLine($"{x.P}html, {x.P}body, {x.P}.MuiTypography-root, {x.P}.MuiButtonBase-root, {x.P}.MuiInputBase-root {{ font-family: {family} !important; }}");
        }

        if (t.Scale != 100)
        {
            // Scale body, not html: the TV/mobile sizes sit on html
            // (.layout-tv { font-size: 125% }) and those must survive.
            sb.AppendLine($"{x.P}body {{ font-size: {t.Scale}% !important; }}");
        }
    }

    private static void AppendHeader(StringBuilder sb, Context x)
    {
        var h = x.Config.Header;
        var color = x.HeaderColor;

        sb.AppendLine("/* --- top bar --- */");

        // Jellyfin 12 renders the visible top bar with MUI: <header class="MuiAppBar-root">.
        // At the top of a page it is "colorTransparent", after scrolling
        // "colorDefault" (AppBar-defaultBg plus a light "elevation" gradient).
        // The old .skinHeader is still in the DOM with zero height - kept in the
        // list for pages that still use it (the player and such).
        // "html" in front = higher specificity than the web client's !important rules.
        var bars = $"{x.P}{AppBar}, {x.P}html .skinHeader-withBackground, {x.P}html .skinHeader.semiTransparent";
        var surface = Surface(h.Style, color, h.Opacity, h.Blur, x, "90deg");

        var radius = Px(h.Radius);
        // A style with a shadow or border of its own (brutalism, glow...)
        // keeps it; the drop shadow and the thin line are for the others.
        var shadow = StyleShadow(h.Style, color, x) ?? (h.Shadow || x.HeaderFloating ? "0 6px 24px rgba(0, 0, 0, 0.35)" : "none");
        var border = StyleBorder(h.Style, x) ?? (h.BottomBorder ? $"1px solid {x.Text.Rgba(0.1)}" : "0");

        if (h.Layout == HeaderLayout.Sections)
        {
            // The bar itself goes transparent; its three groups get the look:
            // logo + links (.MuiStack-root), icons (.MuiBox-root) and the user
            // (.MuiBox-root). The second toolbar (library title, filters) has
            // the same kind of children, so it gets islands too.
            sb.AppendLine($"{bars} {{ background: transparent !important; box-shadow: none !important; backdrop-filter: none !important; -webkit-backdrop-filter: none !important; }}");
            sb.AppendLine($"{x.P}{Toolbar} {{ gap: 8px; }}");
            sb.AppendLine($"{x.P}{Toolbar} > .MuiStack-root, {x.P}{Toolbar} > .MuiBox-root {{ {surface} border-radius: {Px(h.SectionRadius)} !important; padding: 2px 6px !important; box-shadow: {shadow} !important; border: {border}; }}");
            sb.AppendLine($"{x.P}{Toolbar} > .MuiBox-root:empty {{ display: none; }}");
            // The icon group has flex-grow: 1 (it stretches across the gap);
            // with its own background it would become a band across half the
            // bar. Stop the stretching and push it right with a margin instead;
            // :has() tells the icon group apart from the item-count group in
            // the second row.
            sb.AppendLine($"{x.P}{Toolbar} > .MuiStack-root:last-child:not(:first-child) {{ margin-left: auto !important; }}");
        }
        else
        {
            sb.AppendLine($"{bars} {{ {surface} }}");
        }

        if (h.Style == SurfaceStyle.NeoBrutalism)
        {
            // Everything on the (accent-colored by default) bar in the
            // contrasting color; the active link, normally accent-colored
            // text, would vanish - underline it instead.
            var contrast = color.ContrastText;
            sb.AppendLine($"{x.P}{Toolbar}, {x.P}{Toolbar} .MuiButton-root, {x.P}{Toolbar} .MuiIconButton-root, {x.P}{Toolbar} .MuiTypography-root, {x.P}{Toolbar} .MuiSvgIcon-root {{ color: {contrast} !important; }}");
            sb.AppendLine($"{x.P}{NavLink}.MuiButton-colorPrimary {{ text-decoration: underline !important; text-decoration-thickness: 3px !important; text-underline-offset: 4px !important; font-weight: 700 !important; }}");
            // MUI's hover is white at 8% - invisible on a bright fill.
            var hover = color.IsLight ? "rgba(0, 0, 0, 0.14)" : "rgba(255, 255, 255, 0.2)";
            sb.AppendLine($"{x.P}{Toolbar} .MuiIconButton-root:hover, {x.P}{Toolbar} .MuiButton-root:hover {{ background-color: {hover} !important; }}");
        }

        if (h.Layout != HeaderLayout.Sidebar)
        {
            AppendSlots(sb, x);
        }

        // The detail page has a "ribbon" (.detailRibbon) under the bar in the
        // same color - keep it in step with the bar.
        if (h.Style != SurfaceStyle.Transparent && x.RibbonStyle == RibbonStyle.SameAsBar)
        {
            sb.AppendLine($"{x.P}html .detailRibbon {{ background: {color.Rgba(0.8)} !important; }}");
            if (h.Style == SurfaceStyle.NeoBrutalism)
            {
                // The ribbon follows the (accent-filled) bar, so its text follows the bar's contrast color too.
                sb.AppendLine($"{x.P}html .detailRibbon, {x.P}html .detailRibbon .detailButton, {x.P}html .detailRibbon h1, {x.P}html .detailRibbon .mediaInfoItem {{ color: {color.ContrastText} !important; }}");
            }
        }

        var headers = $"{x.P}{AppBar}, {x.P}html .skinHeader";
        if (h.Layout == HeaderLayout.Sidebar)
        {
            AppendSidebar(sb, x, radius, shadow, border);
        }
        else if (h.Layout == HeaderLayout.Sections)
        {
            // With islands the bar has nothing to round; only floating applies.
            if (x.HeaderFloating)
            {
                sb.AppendLine($"{headers} {{ left: 12px !important; right: 12px !important; top: 8px !important; width: auto !important; }}");
            }
        }
        else if (x.HeaderFloating)
        {
            // Floating bar: inset from the edges, rounded on all sides. On TV
            // the old .skinHeader is in the page flow (position: relative), so
            // margins are enough there.
            sb.AppendLine($"{headers} {{ left: 12px !important; right: 12px !important; top: 8px !important; width: auto !important; border-radius: {radius} !important; box-shadow: {shadow} !important; border-bottom: {border}; overflow: hidden; }}");
            // (overflow: visible again - the TV info strip hangs below the bar.)
            sb.AppendLine($"{x.P}html.layout-tv .skinHeader {{ left: auto !important; right: auto !important; top: auto !important; margin: 8px 12px 0; overflow: visible; }}");
        }
        else
        {
            sb.AppendLine($"{headers} {{ border-radius: 0 0 {radius} {radius} !important; box-shadow: {shadow} !important; border-bottom: {border}; }}");
        }

        if (h.Height > 0)
        {
            sb.AppendLine($"{x.P}{Toolbar}:first-child {{ min-height: {Px(h.Height)} !important; }}");
        }

        // Phones: everything has to fit one row - tighter icons, no wrapping
        // (with islands and a couple of custom buttons the user icon used to
        // drop to a second line).
        sb.AppendLine($"{x.P}html.layout-mobile header.MuiAppBar-root .MuiToolbar-root:first-child {{ flex-wrap: nowrap !important; gap: 4px !important; padding-left: 8px !important; padding-right: 8px !important; }}");
        sb.AppendLine($"{x.P}html.layout-mobile header.MuiAppBar-root .MuiToolbar-root:first-child .MuiIconButton-root {{ padding: 6px !important; }}");
        if (h.Layout == HeaderLayout.Sections)
        {
            sb.AppendLine($"{x.P}html.layout-mobile header.MuiAppBar-root .MuiToolbar-root:first-child > .MuiStack-root, {x.P}html.layout-mobile header.MuiAppBar-root .MuiToolbar-root:first-child > .MuiBox-root {{ padding: 2px 3px !important; }}");
        }

        if (x.HeaderFloating && h.Layout != HeaderLayout.Sidebar)
        {
            // A floating bar starts 8px lower than the spacer <div> that keeps
            // the content below the bar accounts for, and a brutalist shadow
            // adds to that - grow the spacer so nothing hides under the bar.
            // Brutalism: 6px of border, 4px of shadow and a little air.
            var overlap = 8 + (h.Style == SurfaceStyle.NeoBrutalism ? 14 : 0);
            sb.AppendLine($"{x.P}{AppBar} + div {{ padding-bottom: {Px(overlap)} !important; }}");
        }

        // Navigation links (Favorites, Movies...). The active one carries MuiButton-colorPrimary.
        switch (h.Nav)
        {
            case NavStyle.Pill:
                sb.AppendLine($"{x.P}{NavLink} {{ border-radius: 999px !important; padding: 4px 14px !important; min-width: 0 !important; }}");
                sb.AppendLine($"{x.P}{NavLink}.MuiButton-colorPrimary {{ background: {x.Accent.Hex} !important; color: {x.Accent.ContrastText} !important; }}");
                sb.AppendLine($"{x.P}{NavLink}:not(.MuiButton-colorPrimary):hover {{ background: {x.Text.Rgba(0.1)} !important; }}");
                break;
            case NavStyle.Underline:
                sb.AppendLine($"{x.P}{NavLink} {{ border-radius: 0 !important; border-bottom: 2px solid transparent !important; }}");
                sb.AppendLine($"{x.P}{NavLink}.MuiButton-colorPrimary {{ border-bottom-color: {x.Accent.Hex} !important; color: {x.Text.Hex} !important; }}");
                break;
        }

        // Icons on the right. They have stable aria-controls values; the
        // localized labels cannot be relied on.
        if (h.HideSyncPlay)
        {
            sb.AppendLine($"{x.P}{Toolbar} [aria-controls=\"app-sync-play-menu\"] {{ display: none !important; }}");
        }

        if (h.HideCast)
        {
            sb.AppendLine($"{x.P}{Toolbar} [aria-controls=\"app-remote-play-menu\"] {{ display: none !important; }}");
        }

        if (h.HideSearch)
        {
            sb.AppendLine($"{x.P}{Toolbar} a[href^=\"#/search\"] {{ display: none !important; }}");
        }
    }

    /// <summary>
    /// The Sidebar layout. The header's parent is a flex column holding
    /// &lt;header&gt; (fixed), an empty &lt;div&gt; that pushes the content
    /// down by the bar's height, and &lt;main&gt;. The bar becomes a fixed
    /// column on the left, the spacer is hidden and main gets a margin
    /// instead. The second toolbar row (library title, filters) stays
    /// horizontal above the content. Phones keep the top bar - a 220px
    /// panel would eat the screen - and so does the login page.
    /// </summary>
    private static void AppendSidebar(StringBuilder sb, Context x, string radius, string shadow, string border)
    {
        var h = x.Config.Header;
        var fullWidth = Px(Math.Max(140, h.SidebarWidth));
        // Collapsible: the panel rests at the narrow width and the content is
        // laid out for that; hovering slides it out over the content.
        var width = h.SidebarCollapsible ? Px(Math.Clamp(h.SidebarCollapsedWidth, 48, 120)) : fullWidth;
        var inset = x.HeaderFloating ? "12px" : "0px"; // a unitless 0 is invalid inside calc()
        var scope = SidebarScope(x);
        var bar = $"{scope} header.MuiAppBar-root";
        var toolbar = $"{bar} .MuiToolbar-root:first-child";

        sb.AppendLine("/* --- sidebar layout --- */");
        // The client script reads this to place overlays next to the bar at
        // its resting width - a collapsible bar is wider while hovered, which
        // is exactly when a button in it gets clicked. A plain number: calc()
        // inside a custom property is not evaluated.
        var edge = (h.SidebarCollapsible ? Math.Clamp(h.SidebarCollapsedWidth, 48, 120) : Math.Max(140, h.SidebarWidth)) + (x.HeaderFloating ? 12 : 0);
        sb.AppendLine($"{scope} {{ --jellycanvas-sidebar-edge: {Px(edge)}; }}");
        sb.AppendLine($"{bar} {{ top: {inset} !important; left: {inset} !important; bottom: {inset} !important; right: auto !important; width: {width} !important; height: auto !important; flex-direction: column !important; align-items: stretch !important; border-radius: {(x.HeaderFloating ? radius : $"0 {radius} {radius} 0")} !important; box-shadow: {shadow} !important; border-right: {border}; border-bottom: {(x.HeaderFloating ? border : "0")} !important; overflow: hidden; }}");
        sb.AppendLine($"{toolbar} {{ flex: 1 1 auto; flex-direction: column !important; align-items: stretch !important; justify-content: flex-start !important; min-height: 0 !important; padding: 14px 10px !important; gap: 4px; overflow-y: auto; }}");
        // The fill (and any backdrop blur) moves from the <header> to the
        // column inside it: a backdrop-filter on the header would make it the
        // containing block of the fixed library row, which would then be
        // clipped away inside the 64px sidebar. Frame and shadow stay on the header.
        sb.AppendLine($"{bar} {{ background-color: transparent !important; background-image: none !important; backdrop-filter: none !important; -webkit-backdrop-filter: none !important; }}");
        sb.AppendLine($"{toolbar} {{ {Surface(h.Style, x.HeaderColor, h.Opacity, h.Blur, x, "180deg")} border: 0 !important; box-shadow: none !important; }}");
        sb.AppendLine($"{toolbar} > .MuiStack-root {{ flex-direction: column !important; align-items: stretch !important; width: 100%; flex-grow: 0 !important; }}");
        sb.AppendLine($"{toolbar} > .MuiStack-root > a {{ justify-content: flex-start !important; width: 100%; margin: 2px 0 !important; }}");
        sb.AppendLine($"{toolbar} > .MuiStack-root > a[href=\"#/\"] {{ margin-bottom: 14px !important; }}");
        sb.AppendLine($"{toolbar} > .MuiBox-root {{ flex-grow: 0 !important; margin-left: 0 !important; display: flex; flex-wrap: wrap; justify-content: center; }}");
        sb.AppendLine($"{toolbar} > .MuiBox-root:last-child {{ padding-top: 6px; }}");
        AppendSidebarSlots(sb, x, toolbar);

        if (h.SidebarCollapsible)
        {
            // Collapsed: icons only. Link text and the server name are hidden
            // with font-size: 0 (the icons keep their own size); the bar
            // widens on hover or keyboard focus and overlaps the content.
            // Folding waits 150ms: when one of the bar's menus opens, the
            // backdrop takes the hover a frame before the menu is in the
            // DOM, and without the delay the bar would twitch narrower.
            sb.AppendLine($"{bar} {{ transition: width 0.2s ease 0.15s; z-index: 1200 !important; }}");
            // :focus-within would keep it open after a click (the clicked link
            // keeps focus); :focus-visible only fires for keyboard focus.
            // Only the column itself counts as hovered - the library row
            // (title, filters) lives in the same <header> and must not slide
            // the bar out. While it is out, the part of that row under it is
            // clipped away so the links are not covered.
            // The bar's own menus (user, SyncPlay, Cast) open in a portal
            // with a backdrop that takes the hover away; the bar must stay
            // out while one of them is open, or the menu would float next
            // to a folded bar. Closed menus keep their element with
            // MuiModal-hidden (or none at all).
            var menuOpen = ":has(#app-user-menu:not(.MuiModal-hidden), #app-sync-play-menu:not(.MuiModal-hidden), #app-remote-play-menu:not(.MuiModal-hidden))";
            var open = $"{bar}:has(.MuiToolbar-root:first-child:hover), {bar}:has(.MuiToolbar-root:first-child :focus-visible), {scope}{menuOpen} header.MuiAppBar-root";
            sb.AppendLine($"{open} {{ width: {fullWidth} !important; transition-delay: 0s; }}");
            sb.AppendLine($"{bar} .MuiToolbar-root:nth-child(2) {{ transition: clip-path 0.2s ease; }}");
            sb.AppendLine($"{open.Replace(", ", " .MuiToolbar-root:nth-child(2), ", StringComparison.Ordinal)} .MuiToolbar-root:nth-child(2) {{ clip-path: inset(0 0 0 calc({fullWidth} - {width})); }}");
            sb.AppendLine($"{toolbar} {{ padding-left: 8px !important; padding-right: 8px !important; }}");
            sb.AppendLine($"{toolbar} > .MuiStack-root > a {{ white-space: nowrap; overflow: hidden; padding-left: 10px !important; padding-right: 10px !important; }}");
            var collapsed = $"{scope}:not({menuOpen}) header.MuiAppBar-root:not(:has(.MuiToolbar-root:first-child:hover)):not(:has(.MuiToolbar-root:first-child :focus-visible))";
            sb.AppendLine($"{collapsed} .MuiToolbar-root:first-child > .MuiStack-root > a {{ font-size: 0 !important; }}");
            sb.AppendLine($"{collapsed} .MuiToolbar-root:first-child > .MuiStack-root > a .MuiButton-startIcon {{ margin: 0 !important; font-size: 16px; }}");
            sb.AppendLine($"{collapsed} .MuiToolbar-root:first-child > .MuiStack-root > a .MuiButton-startIcon > * {{ font-size: 1.5rem !important; }}");
            sb.AppendLine($"{collapsed} .MuiToolbar-root:first-child > .MuiBox-root {{ flex-direction: column; align-items: center; }}");
        }

        // Second row: pinned to the top of the content area, in the bar's own surface.
        sb.AppendLine($"{bar} .MuiToolbar-root:nth-child(2) {{ position: fixed; top: var(--jellycanvas-info, 0px); left: calc({width} + {inset}); right: 0; z-index: 2; {Surface(h.Style == SurfaceStyle.Transparent ? SurfaceStyle.Glass : h.Style, x.HeaderColor, Math.Min(h.Opacity, 92), h.Blur, x, "90deg")} }}");
        sb.AppendLine($"{scope} header.MuiAppBar-root + div {{ display: none !important; }}");
        // main keeps the full viewport width (React sizes it to 100%), so
        // without a matching width the right edge would run off the screen.
        sb.AppendLine($"{scope} header.MuiAppBar-root ~ main {{ margin-left: calc({width} + {inset} + {inset}) !important; width: calc(100% - {width} - {inset} - {inset}) !important; max-width: none !important; }}");
        // Pages are absolutely positioned inside <main>, so padding on main
        // would not move them - offset the pages themselves (info bar, and the
        // second toolbar row where there is one).
        sb.AppendLine($"{scope} header.MuiAppBar-root ~ main .mainAnimatedPage {{ top: var(--jellycanvas-info, 0px) !important; left: 0 !important; right: 0 !important; width: auto !important; }}");
        sb.AppendLine($"{scope} header.MuiAppBar-root:has(.MuiToolbar-root:nth-child(2)) ~ main .mainAnimatedPage {{ top: calc({Px(x.LibraryRowHeight)} + var(--jellycanvas-info, 0px)) !important; }}");
    }

    /// <summary>
    /// The second row of the bar on library pages: title, sort, filter and
    /// view buttons (.MuiToolbar-root:nth-child(2) inside the header). It
    /// shares the bar's surface unless told otherwise here. With the
    /// sidebar layout it is a fixed strip above the content (see
    /// AppendSidebar), so a hidden row also takes its space back there.
    /// </summary>
    private static void AppendLibraryRow(StringBuilder sb, Context x)
    {
        var h = x.Config.Header;
        var sidebar = h.Layout == HeaderLayout.Sidebar;
        if (!sidebar)
        {
            // Under a top bar the row is part of the bar and simply shares
            // its look; only next to a sidebar is it a strip of its own.
            return;
        }

        // With the sidebar the row is styled under the sidebar's scope (which
        // carries an id in :has() and so outranks a plain selector) - the
        // rules here must be written under the same scope to win, plus the
        // phone variant where the sidebar falls back to a top bar.
        var row = sidebar
            ? $"{SidebarScope(x)} header.MuiAppBar-root .MuiToolbar-root:nth-child(2), {x.P}html.layout-mobile header.MuiAppBar-root .MuiToolbar-root:nth-child(2)"
            : $"{x.P}{AppBar} .MuiToolbar-root:nth-child(2)";

        sb.AppendLine("/* --- library row --- */");
        switch (h.LibraryRow)
        {
            case LibraryRowStyle.SameAsBar:
                break;
            case LibraryRowStyle.Hidden:
                sb.AppendLine($"{row} {{ display: none !important; }}");
                if (sidebar)
                {
                    sb.AppendLine($"{SidebarScope(x)} header.MuiAppBar-root:has(.MuiToolbar-root:nth-child(2)) ~ main .mainAnimatedPage {{ top: var(--jellycanvas-info, 0px) !important; }}");
                }

                return;
            default:
                var rowStyle = Enum.Parse<SurfaceStyle>(h.LibraryRow.ToString());
                var rowColor = Color.Parse(h.LibraryRowColor, rowStyle switch
                {
                    SurfaceStyle.Neumorphism => x.Background,
                    SurfaceStyle.NeoBrutalism => x.Accent,
                    _ => x.Surface,
                });
                sb.AppendLine($"{row} {{ {Surface(rowStyle, rowColor, h.LibraryRowOpacity, h.LibraryRowBlur, x, "90deg")} }}");
                if (rowStyle == SurfaceStyle.NeoBrutalism)
                {
                    sb.AppendLine($"{row.Replace(", ", " *, ", StringComparison.Ordinal)} * {{ color: {rowColor.ContrastText} !important; }}");
                }

                break;
        }

        if (x.LibraryRowSetHeight > 0)
        {
            // The toolbar's own vertical padding would keep it taller than
            // asked; the controls themselves need about 44px.
            sb.AppendLine($"{row} {{ min-height: {Px(x.LibraryRowSetHeight)} !important; height: {Px(x.LibraryRowSetHeight)} !important; padding-top: 0 !important; padding-bottom: 0 !important; }}");
        }

        if (h.LibraryRowRadius > 0)
        {
            // Rounded corners need air around them; under a top bar the row is
            // in the flow (margins), next to a sidebar it is fixed (insets).
            var r = Px(h.LibraryRowRadius);
            sb.AppendLine($"{row} {{ border-radius: {r} !important; }}");
            if (sidebar)
            {
                var inset = x.HeaderFloating ? "12px" : "0px";
                var width = Px(h.SidebarCollapsible ? Math.Clamp(h.SidebarCollapsedWidth, 48, 120) : Math.Max(140, h.SidebarWidth));
                sb.AppendLine($"{SidebarScope(x)} header.MuiAppBar-root .MuiToolbar-root:nth-child(2) {{ left: calc({width} + {inset} + 10px) !important; right: 10px !important; margin-top: 6px !important; }}");
            }
            else
            {
                sb.AppendLine($"{row} {{ margin: 0 10px 6px !important; }}");
            }
        }
    }

    /// <summary>
    /// Arranges the three groups of the first toolbar row (logo + links,
    /// icons, user menu) into the left / center / right slots with flex
    /// "order" and auto margins. The stock arrangement is nav left, icons and
    /// user right; anything else is the admin's choice on the plugin page.
    /// </summary>
    private static void AppendSlots(StringBuilder sb, Context x)
    {
        var h = x.Config.Header;
        var groups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["nav"] = $"{x.P}{Toolbar}:first-child > .MuiStack-root",
            ["icons"] = $"{x.P}{Toolbar}:first-child > .MuiBox-root:has(.MuiIconButton-root):not(:has([aria-controls=\"app-user-menu\"]))",
            ["user"] = $"{x.P}{Toolbar}:first-child > .MuiBox-root:has([aria-controls=\"app-user-menu\"])",
        };

        static string[] Parse(string? slot) => (slot ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var left = Parse(h.SlotLeft);
        var center = Parse(h.SlotCenter);
        var right = Parse(h.SlotRight);

        sb.AppendLine("/* --- top bar slots --- */");
        // The icon group stretches by default (flex-grow: 1); with explicit
        // slots the auto margins do the spacing instead.
        sb.AppendLine($"{x.P}{Toolbar}:first-child > .MuiStack-root, {x.P}{Toolbar}:first-child > .MuiBox-root {{ flex-grow: 0 !important; margin-left: 0 !important; margin-right: 0 !important; }}");

        void Place(string[] slot, int baseOrder, bool firstAuto, bool lastAuto)
        {
            for (var i = 0; i < slot.Length; i++)
            {
                if (!groups.TryGetValue(slot[i], out var selector))
                {
                    continue;
                }

                var margins = string.Empty;
                if (firstAuto && i == 0)
                {
                    margins += " margin-left: auto !important;";
                }

                if (lastAuto && i == slot.Length - 1)
                {
                    margins += " margin-right: auto !important;";
                }

                sb.AppendLine($"{selector} {{ order: {baseOrder + i};{margins} }}");
            }
        }

        Place(left, 1, false, false);
        Place(center, 10, true, true);
        Place(right, 20, true, false);
    }

    /// <summary>
    /// The three slots in the sidebar: top / middle / bottom of the column.
    /// Same data as the horizontal slots, with auto margins on the vertical axis.
    /// </summary>
    private static void AppendSidebarSlots(StringBuilder sb, Context x, string toolbar)
    {
        var h = x.Config.Header;
        var groups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["nav"] = $"{toolbar} > .MuiStack-root",
            ["icons"] = $"{toolbar} > .MuiBox-root:has(.MuiIconButton-root):not(:has([aria-controls=\"app-user-menu\"]))",
            ["user"] = $"{toolbar} > .MuiBox-root:has([aria-controls=\"app-user-menu\"])",
        };

        static string[] Parse(string? slot) => (slot ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var top = Parse(h.SlotLeft);
        var middle = Parse(h.SlotCenter);
        var bottom = Parse(h.SlotRight);

        void Place(string[] slot, int baseOrder, bool firstAuto, bool lastAuto)
        {
            for (var i = 0; i < slot.Length; i++)
            {
                if (!groups.TryGetValue(slot[i], out var selector))
                {
                    continue;
                }

                var margins = string.Empty;
                if (firstAuto && i == 0)
                {
                    margins += " margin-top: auto !important;";
                }

                if (lastAuto && i == slot.Length - 1)
                {
                    margins += " margin-bottom: auto !important;";
                }

                sb.AppendLine($"{selector} {{ order: {baseOrder + i};{margins} }}");
            }
        }

        Place(top, 1, false, false);
        Place(middle, 10, true, true);
        Place(bottom, 20, true, false);
    }

    // Phones and TV keep a top bar (TV has no MUI header at all), so nothing of the sidebar applies there.
    private static string SidebarScope(Context x) => $"{x.P}html:not(.layout-mobile):not(.layout-tv):not(:has(#loginPage:not(.hide)))";

    /// <summary>
    /// The announcement strip. Rendered with a pseudo-element, so the text
    /// goes into CSS content (escaped). In the normal layouts it is the
    /// header's ::after - the header is a flex column, so it lands under the
    /// toolbars and the pages are pushed down by its height. In the Sidebar
    /// layout it is a fixed strip along the top of the content area; at the
    /// bottom it is simply a fixed strip along the bottom edge.
    /// </summary>
    private static void AppendInfoBar(StringBuilder sb, Context x)
    {
        var i = x.Config.InfoBar;
        if (!i.Enabled || string.IsNullOrWhiteSpace(i.Text))
        {
            return;
        }

        var bg = Color.Parse(i.Color, x.Accent);
        var fg = string.IsNullOrWhiteSpace(i.TextColor) ? bg.ContrastText : Color.Parse(i.TextColor, x.Text).Hex;
        var height = Px(Math.Max(20, i.Height));
        // With rounding the strip is inset from the edges, otherwise the
        // rounded corners would just be cut off by the window edge.
        var rounded = i.Radius > 0;
        var edge = rounded ? "10px" : "0px"; // a unitless 0 is invalid inside calc()
        // Room for the close button the script adds at the right end.
        var padding = i.Closable ? "0.3em 2.6em 0.3em 1em" : "0.3em 1em";
        // Long text wraps onto more lines instead of running off the screen;
        // the strip grows with it (min-height keeps short texts at the set height).
        var look = $"content: {CssString(i.Text)}; display: flex; align-items: center; justify-content: center; box-sizing: border-box; min-height: {height}; padding: {padding}; background: {bg.Hex}; color: {fg}; font-size: 0.92em; font-weight: 600; letter-spacing: 0.01em; line-height: 1.3; text-align: center; white-space: normal; overflow-wrap: anywhere; border-radius: {Px(i.Radius)};";
        // The space reserved for the strip cannot be measured from CSS, so it
        // is estimated from the text length: ~140 characters per line on a
        // desktop, ~48 on a phone (smaller font there).
        static int Reserve(int chars, int perLine, int lineHeight, int minimum)
            => Math.Max(minimum, (int)Math.Ceiling(chars / (double)perLine) * lineHeight + 10);
        var reserveDesktop = Px(Reserve(i.Text.Trim().Length, 140, 19, Math.Max(20, i.Height)));
        var reserveMobile = Px(Reserve(i.Text.Trim().Length, 48, 16, Math.Max(20, i.Height)));
        var scope = i.HideOnMobile ? $"{x.P}html:not(.layout-mobile)" : $"{x.P}html";

        sb.AppendLine("/* --- info bar --- */");
        // The strip's height as a variable: the sidebar and the pages read
        // it, and the client script zeroes it when the user closes the strip.
        if (i.Position == InfoBarPosition.Top)
        {
            // Next to a floating sidebar the strip starts at the bar's inset,
            // so the space it takes is that much taller.
            var floatingSidebar = x.Config.Header.Layout == HeaderLayout.Sidebar && x.HeaderFloating;
            sb.AppendLine($"{scope} {{ --jellycanvas-info: {(floatingSidebar ? $"calc({reserveDesktop} + 12px)" : reserveDesktop)}; }}");
            sb.AppendLine($"{scope}.layout-mobile {{ --jellycanvas-info: {reserveMobile}; }}");
        }

        sb.AppendLine($"{x.P}html.jellycanvas-infobar-closed {{ --jellycanvas-info: 0px; }}");
        sb.AppendLine($"{x.P}html.jellycanvas-infobar-closed header.MuiAppBar-root::after, {x.P}html.jellycanvas-infobar-closed main::before, {x.P}html.jellycanvas-infobar-closed main::after {{ display: none !important; }}");

        if (i.Position == InfoBarPosition.Bottom)
        {
            sb.AppendLine($"{scope} header.MuiAppBar-root ~ main::after {{ {look} position: fixed; left: {edge}; right: {edge}; bottom: {edge}; z-index: 1098; }}");
            return;
        }

        if (x.Config.Header.Layout == HeaderLayout.Sidebar)
        {
            var h = x.Config.Header;
            var inset = x.HeaderFloating ? "12px" : "0px"; // a unitless 0 is invalid inside calc()
            var sidebar = SidebarScope(x);
            // Next to the bar at its resting width - a collapsible one is narrow most of the time.
            var resting = h.SidebarCollapsible ? Math.Clamp(h.SidebarCollapsedWidth, 48, 120) : Math.Max(140, h.SidebarWidth);
            sb.AppendLine($"{sidebar} header.MuiAppBar-root ~ main::before {{ {look} position: fixed; top: {inset}; left: calc({Px(resting)} + {inset} + {inset} + {edge}); right: {edge}; z-index: 3; }}");
            // Phones keep the top bar even in the Sidebar layout - give them the normal variant.
            if (!i.HideOnMobile)
            {
                sb.AppendLine($"{x.P}html.layout-mobile header.MuiAppBar-root::after {{ {look} }}");
                sb.AppendLine($"{x.P}html.layout-mobile header.MuiAppBar-root ~ main .mainAnimatedPage {{ top: var(--jellycanvas-info, 0px) !important; }}");
            }

            return;
        }

        sb.AppendLine($"{scope} header.MuiAppBar-root::after {{ {look} {(rounded ? "margin: 0 10px 6px;" : string.Empty)} }}");
        // Pages are absolutely positioned inside <main>; padding would not move them.
        sb.AppendLine($"{scope} header.MuiAppBar-root ~ main .mainAnimatedPage {{ top: var(--jellycanvas-info, 0px) !important; }}");
        // Phones: smaller text, cut with an ellipsis rather than squeezed
        // into two overlapping lines.
        sb.AppendLine($"{x.P}html.layout-mobile header.MuiAppBar-root::after {{ font-size: 0.78em; padding-left: 0.7em; padding-right: {(i.Closable ? "2.6em" : "0.7em")}; }}");
    }

    /// <summary>A CSS string literal for content: - quoted, with backslashes, quotes and line breaks escaped.</summary>
    private static string CssString(string text)
        => "\"" + text.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal) + "\"";

    private static void AppendLogo(StringBuilder sb, Context x)
    {
        var h = x.Config.Header;
        sb.AppendLine("/* --- logo --- */");

        // The logo is the first link in the bar: <a href="#/"><span class="MuiButton-startIcon"><img></span>NAME</a>.
        // The login page has the same element, so this applies everywhere.
        if (h.HideLogo)
        {
            sb.AppendLine($"{x.P}{LogoLink} {{ display: none !important; }}");
            sb.AppendLine($"{x.P}.pageTitleWithDefaultLogo {{ background-image: none !important; width: 0 !important; min-width: 0 !important; margin: 0 !important; }}");
            return;
        }

        if (!h.ShowServerName)
        {
            // font-size: 0 hides the text; the image inside has its own size.
            sb.AppendLine($"{x.P}{LogoLink} {{ font-size: 0 !important; min-width: 0 !important; padding-left: 8px !important; padding-right: 8px !important; }}");
            sb.AppendLine($"{x.P}{LogoLink} .MuiButton-startIcon {{ margin: 0 !important; }}");
        }

        var icon = $"{x.P}{LogoLink} .MuiButton-startIcon";
        switch (h.Logo)
        {
            case LogoImage.Hidden:
                sb.AppendLine($"{icon} {{ display: none !important; }}");
                break;
            case LogoImage.Custom when !string.IsNullOrWhiteSpace(h.LogoUrl):
                // Keep the original <img> in place but invisible (it holds the
                // layout) and lay our image over it with ::before.
                var height = Math.Max(12, h.LogoHeight);
                var width = h.LogoWidth > 0 ? h.LogoWidth : height * 3;
                sb.AppendLine($"{icon} {{ position: relative !important; width: {Px(width)} !important; height: {Px(height)} !important; font-size: 0 !important; }}");
                sb.AppendLine($"{icon} img {{ visibility: hidden !important; width: 100% !important; height: 100% !important; }}");
                sb.AppendLine($"{icon}::before {{ content: ''; position: absolute; inset: 0; background: url({CssUrl(h.LogoUrl)}) center / contain no-repeat; }}");
                sb.AppendLine($"{x.P}.pageTitleWithDefaultLogo {{ background-image: url({CssUrl(h.LogoUrl)}) !important; background-size: contain !important; }}");
                break;
            default:
                if (h.LogoHeight != 28)
                {
                    sb.AppendLine($"{icon} img {{ height: {Px(Math.Max(12, h.LogoHeight))} !important; width: auto !important; }}");
                }

                break;
        }
    }

    private static void AppendDrawer(StringBuilder sb, Context x)
    {
        var d = x.Config.Drawer;
        // The menu normally matches the page; brutalism needs a fill that stands out from it.
        var color = Color.Parse(d.Color, d.Style == SurfaceStyle.NeoBrutalism ? x.Surface : x.Background);

        sb.AppendLine("/* --- side menu (.mainDrawer) --- */");
        // .mainDrawer = the menu on user pages, .MuiDrawer-paper = the dashboard's.
        var drawer = $"{x.P}html .mainDrawer, {x.P}html .drawer-open, {x.P}html .MuiDrawer-paper";
        sb.AppendLine($"{drawer} {{ {Surface(d.Style, color, d.Opacity, d.Blur, x, "180deg")} }}");

        if (d.ItemRadius > 0)
        {
            // Menu items run edge to edge; rounding without an inset would
            // look cut off, hence the margin too.
            sb.AppendLine($"{x.P}html .navMenuOption {{ border-radius: {Px(d.ItemRadius)} !important; margin: 2px 10px !important; padding-left: 1.6em !important; }}");
        }

        if (d.Width > 0)
        {
            sb.AppendLine($"{x.P}html .mainDrawer, {x.P}html .MuiDrawer-paper {{ width: {Px(d.Width)} !important; }}");
        }

        if (d.Radius > 0)
        {
            sb.AppendLine($"{x.P}html .mainDrawer, {x.P}html .MuiDrawer-paper {{ border-radius: 0 {Px(d.Radius)} {Px(d.Radius)} 0 !important; overflow: hidden; }}");
        }
    }

    private static void AppendCards(StringBuilder sb, Context x)
    {
        var k = x.Config.Cards;
        sb.AppendLine("/* --- cards (.card) --- */");

        // Rounding goes through the --jf-card-borderRadius variable in the
        // palette; the rest are extra effects.
        var image = $"{x.P}.cardBox:not(.visualCardBox) .cardScalable";
        var effects = new StringBuilder();
        if (k.Shadow)
        {
            effects.Append("0 8px 20px rgba(0, 0, 0, 0.45)");
        }

        if (k.Border)
        {
            if (effects.Length > 0)
            {
                effects.Append(", ");
            }

            // Outside the box: an inset ring would be painted under the
            // image, which sits on top of its container.
            effects.Append($"0 0 0 1px {x.Text.Rgba(0.22)}");
        }

        if (effects.Length > 0)
        {
            sb.AppendLine($"{image} {{ box-shadow: {effects} !important; border-radius: {Px(k.Radius)} !important; }}");
        }

        if (k.Radius > 10)
        {
            // Jellyfin parks its indicators (unplayed count, played tick) in
            // the very corner; with a large radius they would sit on the
            // curve, half outside the card. The curve gives up about 0.3 r at
            // 45 degrees, so that is how far they move in. Both card
            // generations position .cardIndicators absolutely in the image
            // container (the React cards' .indicators wrapper is static and
            // does not take part).
            var inset = Px((int)Math.Round(k.Radius * 0.3));
            sb.AppendLine($"{x.P}html .card .cardIndicators {{ top: calc(0.225em + {inset}) !important; right: calc(0.225em + {inset}) !important; }}");
        }

        if (k.Hover != CardHover.None)
        {
            // The effect must stay off on TV - there the card already grows
            // on focus and two effects at once jump around. Hence :not(.layout-tv).
            var hovered = $"{x.P}html:not(.layout-tv) .card-hoverable:hover .cardBox:not(.visualCardBox) .cardScalable, {x.P}html:not(.layout-tv) .card-hoverable:focus-within .cardBox:not(.visualCardBox) .cardScalable";
            sb.AppendLine($"{image} {{ transition: transform 0.18s ease, box-shadow 0.18s ease; }}");
            var rule = k.Hover switch
            {
                CardHover.Lift => "transform: translateY(-6px); box-shadow: 0 14px 28px rgba(0, 0, 0, 0.5) !important;",
                CardHover.Zoom => "transform: scale(1.05); z-index: 2;",
                CardHover.Glow => $"box-shadow: 0 0 0 2px {x.Accent.Hex}, 0 8px 28px {x.Accent.Rgba(0.45)} !important;",
                _ => string.Empty,
            };
            sb.AppendLine($"{hovered} {{ {rule} }}");
            sb.AppendLine($"{x.P}.card {{ contain: none !important; }}");
        }

        switch (k.Text)
        {
            case CardText.Overlay:
                // In Jellyfin 12 the .cardText elements sit directly in .cardBox
                // (no footer wrapper). Position them absolutely over the bottom
                // of the image and put a dark gradient under them (::after on
                // .cardScalable) so they stay readable.
                sb.AppendLine($"{x.P}.cardBox:not(.visualCardBox) {{ position: relative; }}");
                sb.AppendLine($"{x.P}.cardBox:not(.visualCardBox) > .cardScalable::after {{ content: ''; position: absolute; left: 0; right: 0; bottom: 0; height: 45%; background: linear-gradient(180deg, rgba(0, 0, 0, 0), rgba(0, 0, 0, 0.85)); border-radius: 0 0 {Px(k.Radius)} {Px(k.Radius)}; pointer-events: none; z-index: 1; }}");
                sb.AppendLine($"{x.P}.cardBox:not(.visualCardBox) > .cardText {{ position: absolute; left: 0; right: 0; z-index: 2; padding: 0 0.6em !important; color: #fff; pointer-events: none; }}");
                sb.AppendLine($"{x.P}.cardBox:not(.visualCardBox) > .cardText > * {{ pointer-events: auto; }}");
                sb.AppendLine($"{x.P}.cardBox:not(.visualCardBox) > .cardText-first {{ bottom: 1.5em; }}");
                sb.AppendLine($"{x.P}.cardBox:not(.visualCardBox) > .cardText-first:last-child {{ bottom: 0.4em; }}");
                sb.AppendLine($"{x.P}.cardBox:not(.visualCardBox) > .cardText-secondary {{ bottom: 0.4em; color: rgba(255, 255, 255, 0.7) !important; }}");
                sb.AppendLine($"{x.P}.cardBox-bottompadded {{ margin-bottom: 0.6em !important; }}");
                // The script's card badges would sit on the title - lift the bottom corners above it.
                sb.AppendLine($"{x.P}.cardBox:not(.visualCardBox) .jellycanvas-badges-bl, {x.P}.cardBox:not(.visualCardBox) .jellycanvas-badges-br {{ bottom: 2.6em !important; }}");
                break;
            case CardText.Hidden:
                sb.AppendLine($"{x.P}.cardBox:not(.visualCardBox) > .cardText {{ display: none !important; }}");
                sb.AppendLine($"{x.P}.cardBox-bottompadded {{ margin-bottom: 0.6em !important; }}");
                break;
        }

        if (k.HideOverlayButtons)
        {
            // Only the buttons: in Jellyfin 12 the overlay container also holds
            // the link that opens the item, so hiding the whole container
            // would kill the click-through.
            sb.AppendLine($"{x.P}.cardOverlayContainer .cardOverlayButton, {x.P}.cardOverlayContainer .MuiButtonGroup-root, {x.P}.cardOverlayContainer > button {{ display: none !important; }}");
            sb.AppendLine($"{x.P}.cardOverlayContainer {{ background: transparent !important; }}");
        }

        if (k.HideOverlayButtonsOnFolders && !k.HideOverlayButtons)
        {
            // Cards carry the item type in data-type; series, seasons and
            // collections open a list rather than play, so their hover
            // buttons are mostly noise. Movies and episodes keep theirs.
            var folders = new[] { "Series", "Season", "BoxSet", "CollectionFolder", "Folder" };
            var buttons = string.Join(", ", folders.Select(f => $"{x.P}.card[data-type=\"{f}\"] .cardOverlayContainer .cardOverlayButton, {x.P}.card[data-type=\"{f}\"] .cardOverlayContainer .MuiButtonGroup-root, {x.P}.card[data-type=\"{f}\"] .cardOverlayContainer > button"));
            var containers = string.Join(", ", folders.Select(f => $"{x.P}.card[data-type=\"{f}\"] .cardOverlayContainer"));
            sb.AppendLine($"{buttons} {{ display: none !important; }}");
            sb.AppendLine($"{containers} {{ background: transparent !important; }}");
        }

        if (k.Spacing != 100)
        {
            var f = k.Spacing / 100.0;
            sb.AppendLine($"{x.P}.cardBox {{ margin: {Em(0.6 * f)} !important; }}");
            sb.AppendLine($"{x.P}[dir=ltr] .itemsContainer > .card > .cardBox {{ margin-right: {Em(1.2 * f)} !important; }}");
            sb.AppendLine($"{x.P}.cardBox-bottompadded {{ margin-bottom: {Em(1.8 * f)} !important; }}");
        }

        AppendPlayed(sb, x);
        AppendProgress(sb, x);
    }

    private static void AppendPlayed(StringBuilder sb, Context x)
    {
        var k = x.Config.Cards;
        var played = Color.Parse(k.PlayedColor, x.Accent);
        sb.AppendLine("/* --- played indicator --- */");

        // Two generations of indicators: .cardIndicators > .playedIndicator
        // (home page, legacy cards) and .indicators > .cardIndicators >
        // .playedIndicator.indicator (library, React cards). The color covers both.
        var badge = $"{x.P}html .playedIndicator";
        sb.AppendLine($"{badge} {{ background: {played.Hex} !important; color: {played.ContrastText} !important; }}");

        // A card with a badge is found through :has() - the image is a
        // sibling of the indicator, not its parent, otherwise a plain
        // descendant selector would do.
        var playedCard = $"{x.P}html .card:has(.playedIndicator) .cardImageContainer";
        switch (k.Played)
        {
            case PlayedStyle.CornerBadge:
                var r = Px(k.Radius);
                sb.AppendLine($"{x.P}html .cardIndicators, {x.P}html .indicators, {x.P}html .listItemIndicators {{ top: 0 !important; right: 0 !important; }}");
                sb.AppendLine($"{x.P}html .playedIndicator, {x.P}html .countIndicator, {x.P}html .indicator {{ border-radius: 0 {r} 0 8px !important; box-shadow: -2px 1px 4px rgba(0, 0, 0, 0.6); }}");
                sb.AppendLine($"{x.P}html .mediaSourceIndicator {{ left: 0 !important; top: 0 !important; border-radius: {r} 0 8px 0 !important; }}");
                break;
            case PlayedStyle.Dimmed:
                sb.AppendLine($"{badge} {{ display: none !important; }}");
                sb.AppendLine($"{playedCard} {{ opacity: 0.45 !important; transition: opacity 0.2s; }}");
                sb.AppendLine($"{x.P}html .card:has(.playedIndicator):hover .cardImageContainer {{ opacity: 1 !important; }}");
                break;
            case PlayedStyle.Grayscale:
                sb.AppendLine($"{badge} {{ display: none !important; }}");
                sb.AppendLine($"{playedCard} {{ filter: grayscale(1) brightness(0.7) !important; transition: filter 0.2s; }}");
                sb.AppendLine($"{x.P}html .card:has(.playedIndicator):hover .cardImageContainer {{ filter: none !important; }}");
                break;
            case PlayedStyle.Hidden:
                sb.AppendLine($"{badge} {{ display: none !important; }}");
                break;
        }
    }

    private static void AppendProgress(StringBuilder sb, Context x)
    {
        var k = x.Config.Cards;
        var color = Color.Parse(k.ProgressColor, x.Accent);
        sb.AppendLine("/* --- progress bar on cards --- */");

        // Again two generations: .itemProgressBar/.itemProgressBarForeground
        // (legacy cards) and MUI .itemLinearProgress/.MuiLinearProgress-bar
        // (library). Every style sets both.
        var legacyTrack = $"{x.P}html .itemProgressBar";
        var legacyBar = $"{x.P}html .itemProgressBarForeground";
        var muiTrack = $"{x.P}html .itemLinearProgress";
        var muiBar = $"{x.P}html .itemLinearProgress .MuiLinearProgress-bar";

        sb.AppendLine($"{legacyBar}, {muiBar} {{ background: {color.Hex} !important; }}");
        switch (k.Progress)
        {
            case ProgressStyle.Floating:
                sb.AppendLine($"{x.P}html .innerCardFooterClear {{ background: transparent !important; box-shadow: none !important; }}");
                sb.AppendLine($"{legacyTrack} {{ height: 8px !important; margin: 0.5em 0.8em !important; border-radius: 8px !important; background: rgba(0, 0, 0, 0.5) !important; backdrop-filter: blur(4px); overflow: hidden; }}");
                sb.AppendLine($"{legacyBar} {{ border-radius: 8px !important; }}");
                sb.AppendLine($"{muiTrack} {{ height: 8px !important; margin: 0 0.8em 0.6em !important; border-radius: 8px !important; background: rgba(0, 0, 0, 0.5) !important; }}");
                sb.AppendLine($"{muiBar} {{ border-radius: 8px !important; }}");
                break;
            case ProgressStyle.Bold:
                sb.AppendLine($"{legacyTrack}, {muiTrack} {{ height: 10px !important; background: rgba(0, 0, 0, 0.55) !important; }}");
                sb.AppendLine($"{legacyBar}, {muiBar} {{ border-radius: 0 6px 6px 0 !important; }}");
                break;
            case ProgressStyle.Top:
                sb.AppendLine($"{x.P}html .innerCardFooterClear {{ top: 0 !important; bottom: auto !important; background: transparent !important; box-shadow: none !important; }}");
                sb.AppendLine($"{legacyTrack}, {muiTrack} {{ height: 4px !important; background: rgba(0, 0, 0, 0.4) !important; }}");
                sb.AppendLine($"{muiTrack} {{ top: 0 !important; bottom: auto !important; }}");
                break;
            case ProgressStyle.Hidden:
                sb.AppendLine($"{x.P}html .innerCardFooterClear, {legacyTrack}, {muiTrack} {{ display: none !important; }}");
                break;
            case ProgressStyle.Fill:
                // The track grows to the whole image and the bar, at the
                // watched percentage, becomes a translucent tint over it.
                var tint = color.RgbaPercent(Math.Clamp(k.ProgressFillOpacity, 0, 100));
                sb.AppendLine($"{x.P}html .innerCardFooterClear:has(.itemProgressBar) {{ top: 0 !important; bottom: 0 !important; left: 0 !important; right: 0 !important; padding: 0 !important; background: transparent !important; box-shadow: none !important; }}");
                sb.AppendLine($"{legacyTrack} {{ height: 100% !important; margin: 0 !important; border-radius: 0 !important; background: transparent !important; }}");
                sb.AppendLine($"{legacyBar} {{ height: 100% !important; background: {tint} !important; border-radius: 0 !important; }}");
                sb.AppendLine($"{muiTrack} {{ position: absolute !important; top: 0 !important; bottom: 0 !important; left: 0 !important; right: 0 !important; height: auto !important; margin: 0 !important; border-radius: 0 !important; background: transparent !important; }}");
                sb.AppendLine($"{muiBar} {{ background: {tint} !important; border-radius: 0 !important; }}");
                break;
        }
    }

    private static void AppendButtons(StringBuilder sb, Context x)
    {
        var b = x.Config.Buttons;
        sb.AppendLine("/* --- buttons & inputs --- */");

        // Three kinds of buttons: the legacy .emby-button (.raised/.button-submit
        // in dialogs and settings), .detailButton (the flat buttons on the
        // detail page: Play, Favorite...) and MUI .MuiButton-root (library:
        // Play all, filters).
        sb.AppendLine($"{x.P}.emby-button, {x.P}.raised, {x.P}.fab, {x.P}.button-submit, {x.P}.emby-input, {x.P}.emby-textarea, {x.P}.emby-select-withcolor, {x.P}html .MuiButton-root, {x.P}html .MuiButtonGroup-root, {x.P}html .detailButton {{ border-radius: {Px(b.Radius)} !important; }}");
        sb.AppendLine($"{x.P}html .MuiButtonGroup-root .MuiButton-root {{ border-radius: 0 !important; }}");
        sb.AppendLine($"{x.P}html .MuiButtonGroup-root .MuiButton-root:first-child {{ border-radius: {Px(b.Radius)} 0 0 {Px(b.Radius)} !important; }}");
        sb.AppendLine($"{x.P}html .MuiButtonGroup-root .MuiButton-root:last-child {{ border-radius: 0 {Px(b.Radius)} {Px(b.Radius)} 0 !important; }}");

        var detail = $"{x.P}html .detailButton";
        switch (b.Style)
        {
            case ButtonStyle.Filled:
                sb.AppendLine($"{detail} {{ background: {x.Text.Rgba(0.1)} !important; margin: 0 0.25em !important; }}");
                sb.AppendLine($"{detail}:hover {{ background: {x.Text.Rgba(0.18)} !important; }}");
                break;
            case ButtonStyle.Outline:
                sb.AppendLine($"{x.P}.raised, {x.P}a[data-role=button], {detail} {{ background: transparent !important; box-shadow: inset 0 0 0 2px {x.Text.Rgba(0.35)} !important; }}");
                sb.AppendLine($"{detail} {{ margin: 0 0.25em !important; }}");
                sb.AppendLine($"{x.P}.button-submit, {x.P}html .MuiButton-contained {{ background: transparent !important; box-shadow: inset 0 0 0 2px {x.Accent.Hex} !important; color: {x.Accent.Hex} !important; }}");
                sb.AppendLine($"{x.P}.button-submit:hover, {x.P}.button-submit:focus, {x.P}html .MuiButton-contained:hover {{ background: {x.Accent.Rgba(0.18)} !important; }}");
                break;
            case ButtonStyle.Soft:
                sb.AppendLine($"{x.P}.raised, {x.P}a[data-role=button], {detail} {{ background: {x.Text.Rgba(0.08)} !important; }}");
                sb.AppendLine($"{x.P}.raised:hover, {detail}:hover {{ background: {x.Text.Rgba(0.14)} !important; }}");
                sb.AppendLine($"{detail} {{ margin: 0 0.25em !important; }}");
                sb.AppendLine($"{x.P}.button-submit, {x.P}html .MuiButton-contained {{ background: {x.Accent.Rgba(0.2)} !important; color: {x.Accent.Lighten(0.25).Hex} !important; box-shadow: none !important; }}");
                sb.AppendLine($"{x.P}.button-submit:hover, {x.P}.button-submit:focus, {x.P}html .MuiButton-contained:hover {{ background: {x.Accent.Rgba(0.32)} !important; }}");
                break;
        }

        if (b.DetailScale != 100)
        {
            sb.AppendLine($"{x.P}html .mainDetailButtons {{ font-size: {b.DetailScale}% !important; }}");
        }

        // Labels: the detail buttons carry their (localized) name in the
        // title attribute; attr() in a pseudo-element turns it into visible text.
        if (b.DetailLabels)
        {
            sb.AppendLine($"{detail}::after {{ content: attr(title); margin-left: 0.45em; font-weight: 600; white-space: nowrap; }}");
        }
        else if (b.PlayLabel)
        {
            sb.AppendLine($"{detail}.btnPlay::after {{ content: attr(title); margin-left: 0.45em; font-weight: 600; white-space: nowrap; }}");
        }

        if (b.HoverLift)
        {
            sb.AppendLine($"{detail}, {x.P}html .raised, {x.P}html .button-submit, {x.P}html .MuiButton-root {{ transition: transform 0.15s ease, background 0.15s ease !important; }}");
            sb.AppendLine($"{detail}:hover, {x.P}html .raised:hover, {x.P}html .button-submit:hover, {x.P}html .MuiButton-root:hover {{ transform: translateY(-2px); }}");
        }

        if (b.Uppercase)
        {
            sb.AppendLine($"{x.P}html .emby-button, {x.P}html .MuiButton-root {{ text-transform: uppercase !important; letter-spacing: 0.04em; }}");
        }

        if (b.IconRadius >= 0)
        {
            sb.AppendLine($"{x.P}html .paper-icon-button-light, {x.P}html .MuiIconButton-root, {x.P}html .cardOverlayButton {{ border-radius: {Px(b.IconRadius)} !important; }}");
        }

        // The main Play button gets its own treatment on top of the shared style.
        var play = $"{detail}.btnPlay";
        var playColor = Color.Parse(b.PlayColor, x.Accent);
        var playRadius = b.PlayRadius >= 0 ? $"border-radius: {Px(b.PlayRadius)} !important;" : string.Empty;
        switch (b.Play)
        {
            case PlayStyle.Accent:
                sb.AppendLine($"{play} {{ background: {playColor.Hex} !important; color: {playColor.ContrastText} !important; box-shadow: none !important; padding-left: 1.2em !important; padding-right: 1.2em !important; {playRadius} }}");
                sb.AppendLine($"{play}:hover {{ background: {playColor.Darken(0.15).Hex} !important; }}");
                break;
            case PlayStyle.Outline:
                sb.AppendLine($"{play} {{ background: transparent !important; box-shadow: inset 0 0 0 2px {playColor.Hex} !important; color: {playColor.Hex} !important; padding-left: 1.2em !important; padding-right: 1.2em !important; {playRadius} }}");
                sb.AppendLine($"{play}:hover {{ background: {playColor.Rgba(0.18)} !important; }}");
                break;
            case PlayStyle.Soft:
                sb.AppendLine($"{play} {{ background: {playColor.Rgba(0.2)} !important; color: {playColor.Lighten(0.25).Hex} !important; box-shadow: none !important; padding-left: 1.2em !important; padding-right: 1.2em !important; {playRadius} }}");
                sb.AppendLine($"{play}:hover {{ background: {playColor.Rgba(0.32)} !important; }}");
                break;
            default:
                if (playRadius.Length > 0)
                {
                    sb.AppendLine($"{play} {{ {playRadius} }}");
                }

                break;
        }
    }

    private static void AppendDialogs(StringBuilder sb, Context x)
    {
        var d = x.Config.Dialogs;
        sb.AppendLine("/* --- dialogs, menus, popovers --- */");

        // .dialog = legacy dialogs (settings, action sheet), MUI Paper = menus
        // and dialogs of the new pages. Full-screen dialogs on mobile are not
        // rounded - it would look like a bug.
        var boxes = $"{x.P}html .dialog:not(.dialog-fullscreen), {x.P}html .MuiMenu-paper, {x.P}html .MuiPopover-paper, {x.P}html .MuiDialog-paper";
        var color = d.Style == SurfaceStyle.Neumorphism ? x.Background : x.Surface;
        sb.AppendLine($"{boxes} {{ {Surface(d.Style, color, d.Opacity, d.Blur, x, "160deg")} border-radius: {Px(d.Radius)} !important; }}");
        sb.AppendLine($"{x.P}html .dialog:not(.dialog-fullscreen) {{ overflow: hidden; }}");
        sb.AppendLine($"{x.P}html .toast {{ border-radius: {Px(d.Radius)} !important; }}");
        if (d.UpNext)
        {
            // The "Up next" prompt of the video player: a fixed box in the
            // bottom right corner (.upNextContainer) with two grey buttons.
            var upNext = $"{x.P}html .upNextContainer";
            sb.AppendLine($"{upNext} {{ {Surface(d.Style, x.Surface, d.Opacity, d.Blur, x, "160deg")} border-radius: {Px(d.Radius)} !important; color: {x.Text.Hex} !important; }}");
            sb.AppendLine($"{upNext} .upNextDialog-button {{ background: {x.Text.Rgba(0.12)} !important; color: {x.Text.Hex} !important; }}");
            sb.AppendLine($"{upNext} .upNextDialog-button.btnStartNow {{ background: {x.Accent.Hex} !important; color: {x.Accent.ContrastText} !important; }}");
        }
        if (IsTranslucent(d.Style, d.Opacity))
        {
            // The insides of dialogs have their own solid backgrounds (header,
            // footer) - for glass to make sense they must be transparent too.
            sb.AppendLine($"{x.P}html .dialog .formDialogHeader, {x.P}html .dialog .formDialogFooter, {x.P}html .dialog .actionSheetContent {{ background: transparent !important; }}");
        }
    }

    private static void AppendBackdrop(StringBuilder sb, Context x)
    {
        var bd = x.Config.Backdrop;
        sb.AppendLine("/* --- backdrop image --- */");

        // Two different things share the name:
        //  1. Jellyfin's backdrop = the item image (.backdropImage) under a
        //     translucent layer in the background color
        //     (.backgroundContainer.withBackdrop, default opacity 0.86). Shown
        //     according to the user's settings.
        //  2. Our image = painted onto .backgroundContainer itself (a fixed,
        //     full-viewport layer behind the content), so it is on every
        //     page, always. It goes directly on the element rather than into
        //     --jf-palette-background-defaultImage: Chrome resolves a relative
        //     url() inside a variable against the file where the variable is
        //     *used* (themes/dark/theme.css), so "../Jellycanvas" would point
        //     into /web/themes/. In our own stylesheet it resolves to the root.
        sb.AppendLine($"{x.P}html .backgroundContainer.withBackdrop {{ opacity: {Dec(bd.Dim / 100.0)} !important; }}");

        var url = bd.Mode switch
        {
            BackdropMode.RandomLibrary => RandomBackdropUrl,
            BackdropMode.Custom when !string.IsNullOrWhiteSpace(bd.Url) => bd.Url,
            _ => null,
        };

        if (url is null)
        {
            var filter = bd.Blur > 0 ? $"filter: blur({Px(bd.Blur)});" : string.Empty;
            if (bd.Animate)
            {
                // The image is a bit larger than the screen and drifts slowly
                // from side to side. Blur composes with the same rule.
                sb.AppendLine($"{x.P}.backdropImage {{ background-size: 115% !important; animation: jellycanvas-pan 60s ease-in-out infinite alternate; {filter} }}");
                sb.AppendLine("@keyframes jellycanvas-pan { from { background-position: 0% 50%; } to { background-position: 100% 50%; } }");
            }
            else if (bd.Blur > 0)
            {
                // scale() hides the blurred edges, which would otherwise be see-through.
                sb.AppendLine($"{x.P}.backdropImage {{ {filter} transform: scale(1.06); }}");
            }

            return;
        }

        // Dimming = a translucent layer of the background color laid over the
        // image (two images in one property: the gradient on top, the photo below).
        var dim = x.Background.Rgba(bd.Dim / 100.0);
        var container = $"{x.P}html .backgroundContainer";

        // Our image must not have Jellyfin's own on top of it (two at once),
        // and the blur goes on the whole background layer - the content sits
        // above it separately.
        sb.AppendLine($"{x.P}html .backdropContainer {{ display: none !important; }}");
        sb.AppendLine($"{x.P}html {{ background-image: none !important; }}");
        sb.AppendLine($"{container} {{ opacity: 1 !important; background-position: center !important; }}");
        if (bd.Blur > 0)
        {
            sb.AppendLine($"{container} {{ filter: blur({Px(bd.Blur)}); transform: scale(1.06); }}");
        }

        var pan = bd.Animate ? "background-size: 115% auto !important; animation: jellycanvas-pan 60s ease-in-out infinite alternate;" : "background-size: cover !important;";
        if (bd.Animate)
        {
            sb.AppendLine("@keyframes jellycanvas-pan { from { background-position: 0% 50%; } to { background-position: 100% 50%; } }");
        }

        if (bd.Mode == BackdropMode.RandomLibrary && bd.RotateSeconds > 0)
        {
            AppendBackdropRotation(sb, container, dim, bd.RotateSeconds, bd.Animate);
            return;
        }

        sb.AppendLine($"{container} {{ background-image: linear-gradient({dim}, {dim}), url({CssUrl(url)}) !important; {pan} }}");
    }

    /// <summary>
    /// A random backdrop that changes every few seconds - in pure CSS.
    /// Two layers (::before and ::after on the background container) take
    /// turns: one fades out while the other fades in, and each swaps its
    /// image while it is invisible. Every image is a different URL
    /// (?n=1, ?n=2...), and the server answers each with a random backdrop,
    /// so one cycle shows RotationImages different pictures and then
    /// repeats them (the browser has them cached by then).
    /// </summary>
    private static void AppendBackdropRotation(StringBuilder sb, string container, string dim, int seconds, bool animate)
    {
        var n = RotationImages;
        var total = n * seconds;
        var size = animate ? "background-size: 115% auto;" : "background-size: cover;";
        var pan = animate ? ", jellycanvas-pan 60s ease-in-out infinite alternate" : string.Empty;
        var layer = $"content: ''; position: absolute; inset: 0; background-position: center; background-repeat: no-repeat; {size}";

        sb.AppendLine($"{container} {{ background-image: none !important; }}");
        // Layer A shows images 1, 3, 5...; layer B shows 2, 4, 6... When the
        // client script is present it rotates instead (preloaded images, a
        // real cross-fade) and marks <html>; the CSS layers then stay off.
        sb.AppendLine($"{container}::before {{ {layer} animation: jellycanvas-fade-a {2 * seconds}s linear infinite, jellycanvas-images-a {total}s step-end infinite{pan}; }}");
        sb.AppendLine($"{container}::after {{ {layer} animation: jellycanvas-fade-b {2 * seconds}s linear infinite, jellycanvas-images-b {total}s step-end infinite{pan}; }}");
        sb.AppendLine($"html.jellycanvas-js-backdrop .backgroundContainer::before, html.jellycanvas-js-backdrop .backgroundContainer::after {{ display: none !important; }}");
        sb.AppendLine($"html .backgroundContainer > .jellycanvas-backdrop {{ position: absolute; inset: 0; overflow: hidden; }}");
        sb.AppendLine($"html .backgroundContainer > .jellycanvas-backdrop > div {{ position: absolute; inset: 0; background-position: center; background-repeat: no-repeat; {size} opacity: 0; transition: opacity 1.6s ease-in-out; {(animate ? "animation: jellycanvas-pan 60s ease-in-out infinite alternate;" : string.Empty)} }}");
        sb.AppendLine($"html .backgroundContainer > .jellycanvas-backdrop > div.is-on {{ opacity: 1; }}");
        sb.AppendLine($"html .backgroundContainer > .jellycanvas-backdrop::after {{ content: ''; position: absolute; inset: 0; background: {dim}; }}");

        // Visibility over one two-slot period: A visible in the first half,
        // B in the second, with a cross-fade around the hand-over.
        sb.AppendLine("@keyframes jellycanvas-fade-a { 0% { opacity: 1; } 44% { opacity: 1; } 50% { opacity: 0; } 94% { opacity: 0; } 100% { opacity: 1; } }");
        sb.AppendLine("@keyframes jellycanvas-fade-b { 0% { opacity: 0; } 44% { opacity: 0; } 50% { opacity: 1; } 94% { opacity: 1; } 100% { opacity: 0; } }");

        // Image swaps happen in the middle of each layer's invisible half.
        // Slot i (0-based) lasts one interval; A owns even slots, B odd ones.
        // Percentages are of the whole cycle (n slots).
        var a = new StringBuilder("@keyframes jellycanvas-images-a { 0% { " + Image(dim, 1) + " } ");
        var b = new StringBuilder("@keyframes jellycanvas-images-b { 0% { " + Image(dim, 2) + " } ");
        for (var i = 0; i < n / 2; i++)
        {
            // A is hidden during slot 2i+1 -> swap at its middle: (2i + 1.5) / n.
            var nextA = (2 * i) + 3 > n ? 1 : (2 * i) + 3;
            a.Append(Pct(((2 * i) + 1.5) / n)).Append(" { ").Append(Image(dim, nextA)).Append(" } ");
            // B is hidden during slot 2i+2 -> swap at (2i + 2.5) / n.
            var nextB = (2 * i) + 4 > n ? 2 : (2 * i) + 4;
            b.Append(Pct(((2 * i) + 2.5) / n)).Append(" { ").Append(Image(dim, nextB)).Append(" } ");
        }

        sb.AppendLine(a.Append('}').ToString());
        sb.AppendLine(b.Append('}').ToString());
    }

    private static string Image(string dim, int index)
        => $"background-image: linear-gradient({dim}, {dim}), url(\"{RandomBackdropUrl}?n={index.ToString(CultureInfo.InvariantCulture)}\");";

    private static void AppendDetail(StringBuilder sb, Context x)
    {
        var d = x.Config.Detail;
        sb.AppendLine("/* --- item detail page --- */");
        // The ribbon: SameAsBar is handled with the bar; anything else is a
        // surface of its own in the chosen color.
        if (x.RibbonStyle != RibbonStyle.SameAsBar)
        {
            var ribbonStyle = Enum.Parse<SurfaceStyle>(x.RibbonStyle.ToString());
            var ribbonColor = Color.Parse(d.RibbonColor, ribbonStyle == SurfaceStyle.Neumorphism ? x.Background : x.HeaderColor);
            // The surface goes on a ::before layer behind the content, not on
            // the ribbon itself: a backdrop-filter on the ribbon would make it
            // the containing block of its absolutely positioned title block,
            // which then collapses to nothing (the title, year and rating
            // vanish). The ribbon only isolates the stacking so the layer
            // stays above the page background.
            sb.AppendLine($"{x.P}html .detailRibbon {{ background: transparent !important; position: relative; isolation: isolate; }}");
            sb.AppendLine($"{x.P}html .detailRibbon::before {{ content: ''; position: absolute; inset: 0; z-index: -1; box-sizing: border-box; pointer-events: none; {Surface(ribbonStyle, ribbonColor, d.RibbonOpacity, d.RibbonBlur, x, "90deg")} }}");
            if (ribbonStyle == SurfaceStyle.NeoBrutalism)
            {
                sb.AppendLine($"{x.P}html .detailRibbon::before {{ border-left: 0 !important; border-right: 0 !important; }}");
                sb.AppendLine($"{x.P}html .detailRibbon, {x.P}html .detailRibbon .detailButton, {x.P}html .detailRibbon h1, {x.P}html .detailRibbon .mediaInfoItem {{ color: {ribbonColor.ContrastText} !important; }}");
            }
        }

        var radius = d.PosterRadius >= 0 ? d.PosterRadius : x.Config.Cards.Radius;
        var poster = $"{x.P}html .detailImageContainer .cardScalable, {x.P}html .detailImageContainer .cardImageContainer, {x.P}html .itemDetailImage";
        var shadow = d.PosterShadow ? "box-shadow: 0 12px 32px rgba(0, 0, 0, 0.6) !important;" : string.Empty;
        sb.AppendLine($"{poster} {{ border-radius: {Px(radius)} !important; {shadow} }}");

        if (d.HideTitleLogo)
        {
            sb.AppendLine($"{x.P}html .detailLogo {{ display: none !important; }}");
        }

        AppendPeople(sb, x);
        AppendDetailBlocks(sb, x);
    }

    /// <summary>
    /// The smaller blocks of the item page: the track selectors, genre / tag
    /// / external-link rows, the overview and the section titles below.
    /// </summary>
    private static void AppendDetailBlocks(StringBuilder sb, Context x)
    {
        var d = x.Config.Detail;

        // Chips: every link in the row becomes a small pill; the "Tags:" /
        // "Genres:" label and the commas between the links go.
        void Chips(string row, string link, DetailBlockStyle style)
        {
            switch (style)
            {
                case DetailBlockStyle.Hidden:
                    sb.AppendLine($"{row} {{ display: none !important; }}");
                    break;
                case DetailBlockStyle.Chips:
                case DetailBlockStyle.AccentChips:
                    var chip = Color.Parse(d.ChipColor, style == DetailBlockStyle.AccentChips ? x.Accent : x.Text);
                    var custom = !string.IsNullOrWhiteSpace(d.ChipColor);
                    var bg = custom ? chip.Hex : style == DetailBlockStyle.AccentChips ? x.Accent.Rgba(0.22) : x.Text.Rgba(0.1);
                    var fg = custom ? chip.ContrastText : style == DetailBlockStyle.AccentChips ? x.Accent.Lighten(0.3).Hex : x.Text.Hex;
                    sb.AppendLine($"{row} {{ display: flex !important; flex-wrap: wrap !important; gap: 6px !important; align-items: center !important; font-size: 0 !important; }}");
                    sb.AppendLine($"{link} {{ font-size: 0.85rem !important; background: {bg} !important; color: {fg} !important; padding: 3px 10px !important; border-radius: 999px !important; margin: 0 !important; text-decoration: none !important; line-height: 1.4 !important; }}");
                    sb.AppendLine($"{link}:hover {{ background: {x.Accent.Rgba(0.4)} !important; color: {x.Text.Hex} !important; }}");
                    break;
            }
        }

        sb.AppendLine("/* --- item detail page: blocks --- */");
        Chips($"{x.P}html #itemDetailPage .itemGenres", $"{x.P}html #itemDetailPage .itemGenres a", d.Genres);
        Chips($"{x.P}html #itemDetailPage .itemTags", $"{x.P}html #itemDetailPage .itemTags a", d.Tags);
        Chips($"{x.P}html #itemDetailPage .itemExternalLinks", $"{x.P}html #itemDetailPage .itemExternalLinks a", d.ExternalLinks);

        var selects = $"{x.P}html #itemDetailPage .trackSelections";
        switch (d.TrackSelections)
        {
            case DetailBlockStyle.Hidden:
                sb.AppendLine($"{selects} {{ display: none !important; }}");
                break;
            case DetailBlockStyle.Chips:
            case DetailBlockStyle.AccentChips:
                // The four selectors side by side as compact pills instead of
                // full-width rows.
                var bg = !string.IsNullOrWhiteSpace(d.ChipColor) ? Color.Parse(d.ChipColor, x.Text).Hex : d.TrackSelections == DetailBlockStyle.AccentChips ? x.Accent.Rgba(0.22) : x.Text.Rgba(0.1);
                sb.AppendLine($"{selects} {{ display: flex !important; flex-wrap: wrap !important; gap: 8px 12px !important; margin-bottom: 1em !important; }}");
                sb.AppendLine($"{selects} .selectContainer {{ flex: 0 1 auto !important; width: auto !important; min-width: 12em !important; margin: 0 !important; }}");
                sb.AppendLine($"{selects} .emby-select-withcolor {{ background: {bg} !important; border: 0 !important; border-radius: 999px !important; padding: 0.45em 2.4em 0.45em 1em !important; }}");
                sb.AppendLine($"{selects} .selectLabel {{ font-size: 0.75em !important; opacity: 0.75; margin-left: 1em !important; }}");
                break;
        }

        // Blocks: any part of the page can sit on a surface of its own.
        void Block(string selector, DetailBlockSurface surface, string colorSetting)
        {
            if (surface == DetailBlockSurface.None)
            {
                return;
            }

            var style = Enum.Parse<SurfaceStyle>(surface.ToString());
            var color = Color.Parse(colorSetting, style switch
            {
                SurfaceStyle.Neumorphism => x.Background,
                SurfaceStyle.NeoBrutalism => x.Accent,
                _ => x.Surface,
            });
            sb.AppendLine($"{selector} {{ {Surface(style, color, d.BlockOpacity, d.BlockBlur, x, "135deg")} border-radius: {Px(d.BlockRadius)} !important; padding: 0.8em 1em !important; margin: 0.6em 0 !important; }}");
            if (style == SurfaceStyle.NeoBrutalism)
            {
                sb.AppendLine($"{selector}, {selector} a, {selector} .selectLabel {{ color: {color.ContrastText} !important; }}");
            }
        }

        Block($"{x.P}html #itemDetailPage .trackSelections", d.SelectorsBlock, d.SelectorsBlockColor);
        Block($"{x.P}html #itemDetailPage .itemGenres", d.GenresBlock, d.GenresBlockColor);
        Block($"{x.P}html #itemDetailPage .itemTags", d.TagsBlock, d.TagsBlockColor);
        Block($"{x.P}html #itemDetailPage .itemExternalLinks", d.LinksBlock, d.LinksBlockColor);
        // The overview block wraps the tagline, the text and its "show more" control.
        Block($"{x.P}html #itemDetailPage .tagline, {x.P}html #itemDetailPage .overview", d.OverviewBlock, d.OverviewBlockColor);
        if (d.OverviewBlock != DetailBlockSurface.None)
        {
            sb.AppendLine($"{x.P}html #itemDetailPage .tagline {{ margin-bottom: 0 !important; border-bottom-left-radius: 0 !important; border-bottom-right-radius: 0 !important; }}");
            sb.AppendLine($"{x.P}html #itemDetailPage .tagline + .overview {{ margin-top: 0 !important; border-top-left-radius: 0 !important; border-top-right-radius: 0 !important; }}");
        }

        var overview = $"{x.P}html #itemDetailPage .overview";
        if (d.OverviewScale != 100)
        {
            sb.AppendLine($"{overview} {{ font-size: {d.OverviewScale}% !important; }}");
        }

        if (d.OverviewMaxWidth > 0)
        {
            sb.AppendLine($"{overview} {{ max-width: {Px(d.OverviewMaxWidth)} !important; }}");
        }

        if (d.HideTagline)
        {
            sb.AppendLine($"{x.P}html #itemDetailPage .tagline {{ display: none !important; }}");
        }

        if (d.HideSimilar)
        {
            sb.AppendLine($"{x.P}html #similarCollapsible {{ display: none !important; }}");
        }

        if (d.HideCast)
        {
            sb.AppendLine($"{x.P}html #castCollapsible, {x.P}html #guestCastCollapsible {{ display: none !important; }}");
        }

        var title = $"{x.P}html #itemDetailPage .detailVerticalSection .sectionTitle";
        switch (d.SectionTitles)
        {
            case SectionTitleStyle.Uppercase:
                sb.AppendLine($"{title} {{ font-size: 0.85em !important; text-transform: uppercase !important; letter-spacing: 0.12em !important; opacity: 0.8; }}");
                break;
            case SectionTitleStyle.AccentLine:
                sb.AppendLine($"{title} {{ position: relative; padding-bottom: 0.4em !important; }}");
                sb.AppendLine($"{title}::after {{ content: \"\"; position: absolute; left: 0; bottom: 0; width: 2.5em; height: 3px; border-radius: 3px; background: {x.Accent.Hex}; }}");
                break;
            case SectionTitleStyle.AccentBar:
                sb.AppendLine($"{title} {{ border-left: 4px solid {x.Accent.Hex} !important; padding-left: 0.5em !important; }}");
                break;
        }
    }

    /// <summary>
    /// Cast &amp; crew cards (.personCard): a 2:3 portrait whose photo is a
    /// background image on .cardImageContainer, sized by .cardPadder's
    /// padding. A square padder plus full rounding makes a circle.
    /// </summary>
    private static void AppendPeople(StringBuilder sb, Context x)
    {
        var d = x.Config.Detail;
        var card = $"{x.P}html .personCard";
        var photo = $"{card} .cardScalable, {card} .cardImageContainer, {card} .cardOverlayContainer";
        switch (d.People)
        {
            case PeopleShape.Circle:
                sb.AppendLine($"{card} .cardPadder-overflowPortrait {{ padding-bottom: 100% !important; }}");
                sb.AppendLine($"{photo} {{ border-radius: 50% !important; }}");
                sb.AppendLine($"{card} .cardScalable {{ overflow: hidden; }}");
                sb.AppendLine($"{card} .cardText {{ text-align: center !important; }}");
                break;
            case PeopleShape.Square:
                sb.AppendLine($"{card} .cardPadder-overflowPortrait {{ padding-bottom: 100% !important; }}");
                sb.AppendLine($"{photo} {{ border-radius: {Px(x.Config.Cards.Radius)} !important; }}");
                break;
            case PeopleShape.Rounded:
                sb.AppendLine($"{photo} {{ border-radius: 22px !important; }}");
                sb.AppendLine($"{card} .cardScalable {{ overflow: hidden; }}");
                break;
        }

        if (d.PeopleScale != 100)
        {
            // The card's width is in em (jellyfin-web sizes overflow cards by
            // font size), so scaling the font scales the whole card.
            sb.AppendLine($"{card} {{ font-size: {d.PeopleScale}% !important; }}");
        }

        if (d.PeopleRing)
        {
            sb.AppendLine($"{card} .cardImageContainer {{ box-shadow: inset 0 0 0 3px {x.Accent.Hex} !important; }}");
        }

        if (d.PeopleGrayscale)
        {
            sb.AppendLine($"{card} .cardImageContainer {{ filter: grayscale(1); transition: filter 0.2s ease; }}");
            sb.AppendLine($"{card}:hover .cardImageContainer, {card}:focus-within .cardImageContainer {{ filter: none; }}");
        }
    }

    private static void AppendLogin(StringBuilder sb, Context x)
    {
        var l = x.Config.Login;
        sb.AppendLine("/* --- login page --- */");
        if (!string.IsNullOrWhiteSpace(l.BackgroundUrl))
        {
            // The image goes into ::before so it can be blurred (a filter on
            // the page itself would blur the form too). z-index: 0 on the page
            // starts its own stacking context, so -1 ends up under the content
            // but above html.
            var blur = l.BackgroundBlur > 0 ? $"filter: blur({Px(l.BackgroundBlur)}); transform: scale(1.06);" : string.Empty;
            sb.AppendLine($"{x.P}html #loginPage {{ position: relative; z-index: 0; }}");
            sb.AppendLine($"{x.P}html #loginPage::before {{ content: ''; position: fixed; inset: 0; z-index: -1; background: url({CssUrl(l.BackgroundUrl)}) center / cover no-repeat; {blur} }}");
        }
        else if (l.GradientBackground)
        {
            // Two chosen colors (or background -> darkened accent), at the
            // chosen opacity so the backdrop behind can be left showing through.
            // A fixed, full-window ::before of the page like the image above:
            // it sits over the background layer (whose random-backdrop
            // ::before/::after would otherwise paint over a gradient put on
            // the layer itself) and, being fixed, does not start under the
            // space reserved for the bar.
            var from = Color.Parse(l.GradientFrom, x.Background).RgbaPercent(l.GradientOpacity);
            var to = Color.Parse(l.GradientTo, x.Accent.Darken(0.55)).RgbaPercent(l.GradientOpacity);
            sb.AppendLine($"{x.P}html #loginPage {{ position: relative; z-index: 0; background: transparent !important; }}");
            sb.AppendLine($"{x.P}html #loginPage::before {{ content: ''; position: fixed; inset: 0; z-index: -1; background: linear-gradient({l.GradientAngle}deg, {from} 0%, {to} 100%); }}");
        }

        if (l.TransparentBar)
        {
            // No bar on the login page - only the logo stays, floating over
            // the page background. Everything else in the toolbar is hidden.
            var loginBar = $"{x.P}html:has(#loginPage:not(.hide)) header.MuiAppBar-root";
            sb.AppendLine($"{loginBar} {{ background: transparent !important; background-image: none !important; box-shadow: none !important; backdrop-filter: none !important; -webkit-backdrop-filter: none !important; border: 0 !important; }}");
            sb.AppendLine($"{loginBar} .MuiToolbar-root > *:not(.MuiStack-root), {loginBar} .MuiToolbar-root > .MuiStack-root > *:not(a[href=\"#/\"]) {{ display: none !important; }}");
            sb.AppendLine($"{loginBar} .MuiToolbar-root > .MuiStack-root {{ background: transparent !important; box-shadow: none !important; backdrop-filter: none !important; -webkit-backdrop-filter: none !important; border: 0 !important; }}");
        }

        var forms = $"{x.P}html #loginPage .manualLoginForm, {x.P}html #loginPage .visualLoginForm:not(.hide)";
        switch (l.Form)
        {
            case LoginFormStyle.Card:
                sb.AppendLine($"{forms} {{ background: {x.Surface.Hex}; border-radius: {Px(l.Radius)}; padding: 1.5em 2em 2em; box-shadow: 0 16px 48px rgba(0, 0, 0, 0.45); }}");
                break;
            case LoginFormStyle.Glass:
                sb.AppendLine($"{forms} {{ background: {x.Surface.Rgba(0.55)}; backdrop-filter: blur(18px) saturate(1.4); -webkit-backdrop-filter: blur(18px) saturate(1.4); border: 1px solid {x.Text.Rgba(0.1)}; border-radius: {Px(l.Radius)}; padding: 1.5em 2em 2em; box-shadow: 0 16px 48px rgba(0, 0, 0, 0.45); }}");
                break;
            case LoginFormStyle.NeoBrutalism:
            case LoginFormStyle.Glowmorphism:
            case LoginFormStyle.Claymorphism:
            case LoginFormStyle.Neumorphism:
                // The same looks as the bar and dialogs get, on the form card.
                var formStyle = Enum.Parse<SurfaceStyle>(l.Form.ToString());
                sb.AppendLine($"{forms} {{ {Surface(formStyle, x.Surface, formStyle == SurfaceStyle.Glowmorphism ? 55 : 100, 18, x, "160deg")} border-radius: {Px(l.Radius)}; padding: 1.5em 2em 2em; }}");
                break;
        }

        var inputRadius = Px(l.InputRadius >= 0 ? l.InputRadius : l.Radius);
        sb.AppendLine($"{x.P}#loginPage .cardBox:not(.visualCardBox) .cardScalable, {x.P}#loginPage .cardImageContainer, {x.P}#loginPage .cardPadder {{ border-radius: {Px(l.Radius)} !important; }}");
        sb.AppendLine($"{x.P}#loginPage .emby-input, {x.P}#loginPage .emby-button {{ border-radius: {inputRadius} !important; }}");

        if (l.FormWidth > 0)
        {
            // The form and the buttons under it (Quick Connect, Forgot password) share the width.
            sb.AppendLine($"{x.P}html #loginPage .manualLoginForm, {x.P}html #loginPage .readOnlyContent {{ max-width: {Px(l.FormWidth)} !important; margin-left: auto !important; margin-right: auto !important; }}");
        }

        var input = $"{x.P}html #loginPage .emby-input";
        switch (l.Inputs)
        {
            // Deliberately far apart from each other and from Jellyfin's own
            // dark filled field, so the choice is visible at a glance.
            case LoginInputStyle.Glass:
                sb.AppendLine($"{input} {{ background: {x.Text.Rgba(0.1)} !important; border: 1px solid {x.Text.Rgba(0.35)} !important; backdrop-filter: blur(14px) saturate(1.3); -webkit-backdrop-filter: blur(14px) saturate(1.3); box-shadow: inset 0 1px 0 {x.Text.Rgba(0.15)}; }}");
                sb.AppendLine($"{input}:focus {{ border-color: {x.Accent.Hex} !important; background: {x.Text.Rgba(0.14)} !important; }}");
                break;
            case LoginInputStyle.Outline:
                sb.AppendLine($"{input} {{ background: transparent !important; border: 2px solid {x.Text.Rgba(0.6)} !important; }}");
                sb.AppendLine($"{input}:focus {{ border-color: {x.Accent.Hex} !important; box-shadow: 0 0 0 3px {x.Accent.Rgba(0.25)}; }}");
                break;
            case LoginInputStyle.Filled:
                sb.AppendLine($"{input} {{ background: {x.Text.Rgba(0.2)} !important; border: 2px solid transparent !important; }}");
                sb.AppendLine($"{input}:focus {{ background: {x.Text.Rgba(0.28)} !important; border-color: {x.Accent.Hex} !important; }}");
                break;
            case LoginInputStyle.NeoBrutalism:
                sb.AppendLine($"{input} {{ background: {x.Surface.Hex} !important; border: 3px solid {x.Outline.Hex} !important; box-shadow: 3px 3px 0 {x.Outline.Hex} !important; }}");
                sb.AppendLine($"{input}:focus {{ border-color: {x.Accent.Hex} !important; box-shadow: 3px 3px 0 {x.Accent.Hex} !important; }}");
                break;
            case LoginInputStyle.Glowmorphism:
                sb.AppendLine($"{input} {{ background: {x.Text.Rgba(0.06)} !important; border: 1px solid {x.Accent.Rgba(0.6)} !important; box-shadow: 0 0 12px {x.Accent.Rgba(0.35)} !important; }}");
                sb.AppendLine($"{input}:focus {{ border-color: {x.Accent.Hex} !important; box-shadow: 0 0 18px {x.Accent.Rgba(0.6)} !important; }}");
                break;
            case LoginInputStyle.Claymorphism:
                sb.AppendLine($"{input} {{ background: {x.Surface.Lighten(0.1).Hex} !important; border: 0 !important; box-shadow: inset 4px 4px 8px {x.Surface.Lighten(0.25).Hex}, inset -4px -4px 8px {x.Surface.Darken(0.35).Hex}, 4px 4px 12px rgba(0, 0, 0, 0.3) !important; }}");
                sb.AppendLine($"{input}:focus {{ box-shadow: inset 4px 4px 8px {x.Surface.Lighten(0.25).Hex}, inset -4px -4px 8px {x.Surface.Darken(0.35).Hex}, 0 0 0 2px {x.Accent.Hex} !important; }}");
                break;
            case LoginInputStyle.Neumorphism:
                // Pressed into the same color as what is around it: the page
                // when the form is plain, the card otherwise.
                var neuBase = l.Form == LoginFormStyle.Plain ? x.Background : x.Surface;
                sb.AppendLine($"{input} {{ background: {neuBase.Hex} !important; border: 0 !important; box-shadow: inset 4px 4px 8px {neuBase.Darken(0.6).Hex}, inset -4px -4px 8px {neuBase.Lighten(0.18).Hex} !important; }}");
                sb.AppendLine($"{input}:focus {{ box-shadow: inset 4px 4px 8px {neuBase.Darken(0.6).Hex}, inset -4px -4px 8px {neuBase.Lighten(0.18).Hex}, 0 0 0 2px {x.Accent.Hex} !important; }}");
                break;
        }

        // The buttons: Sign in (.button-submit, accent) and the two "cancel"
        // ones under the form (Quick Connect, Forgot password).
        var button = $"{x.P}html #loginPage .manualLoginForm .emby-button, {x.P}html #loginPage .readOnlyContent .emby-button";
        // More specific than the rule for all the buttons, so the accent wins.
        var submit = $"{x.P}html #loginPage .manualLoginForm .emby-button.button-submit";
        var buttonBase = l.Form == LoginFormStyle.Plain ? x.Background : x.Surface;
        switch (l.Buttons)
        {
            case LoginInputStyle.Glass:
                sb.AppendLine($"{button} {{ background: {x.Text.Rgba(0.1)} !important; border: 1px solid {x.Text.Rgba(0.35)} !important; color: {x.Text.Hex} !important; backdrop-filter: blur(14px) saturate(1.3); -webkit-backdrop-filter: blur(14px) saturate(1.3); box-shadow: none !important; }}");
                sb.AppendLine($"{submit} {{ background: {x.Accent.Rgba(0.4)} !important; border-color: {x.Accent.Rgba(0.7)} !important; }}");
                break;
            case LoginInputStyle.Outline:
                sb.AppendLine($"{button} {{ background: transparent !important; border: 2px solid {x.Text.Rgba(0.6)} !important; color: {x.Text.Hex} !important; box-shadow: none !important; }}");
                sb.AppendLine($"{submit} {{ border-color: {x.Accent.Hex} !important; color: {x.Accent.Hex} !important; }}");
                sb.AppendLine($"{submit}:hover, {submit}:focus {{ background: {x.Accent.Rgba(0.18)} !important; }}");
                break;
            case LoginInputStyle.Filled:
                sb.AppendLine($"{button} {{ background: {x.Text.Rgba(0.2)} !important; border: 0 !important; color: {x.Text.Hex} !important; box-shadow: none !important; }}");
                sb.AppendLine($"{submit} {{ background: {x.Accent.Hex} !important; color: {x.Accent.ContrastText} !important; }}");
                break;
            case LoginInputStyle.NeoBrutalism:
                sb.AppendLine($"{button} {{ background: {x.Surface.Hex} !important; border: 3px solid {x.Outline.Hex} !important; color: {x.Text.Hex} !important; box-shadow: 3px 3px 0 {x.Outline.Hex} !important; transition: transform 0.1s ease, box-shadow 0.1s ease; }}");
                sb.AppendLine($"{submit} {{ background: {x.Accent.Hex} !important; color: {x.Accent.ContrastText} !important; }}");
                // Pressing pushes the button into its shadow.
                sb.AppendLine($"{button.Replace(", ", ":hover, ", StringComparison.Ordinal)}:hover {{ transform: translate(2px, 2px); box-shadow: 1px 1px 0 {x.Outline.Hex} !important; }}");
                break;
            case LoginInputStyle.Glowmorphism:
                sb.AppendLine($"{button} {{ background: {x.Text.Rgba(0.06)} !important; border: 1px solid {x.Accent.Rgba(0.6)} !important; color: {x.Text.Hex} !important; box-shadow: 0 0 12px {x.Accent.Rgba(0.35)} !important; }}");
                sb.AppendLine($"{submit} {{ background: {x.Accent.Rgba(0.3)} !important; border-color: {x.Accent.Hex} !important; box-shadow: 0 0 18px {x.Accent.Rgba(0.6)} !important; }}");
                break;
            case LoginInputStyle.Claymorphism:
                sb.AppendLine($"{button} {{ background: {x.Surface.Lighten(0.1).Hex} !important; border: 0 !important; color: {x.Text.Hex} !important; box-shadow: inset 4px 4px 8px {x.Surface.Lighten(0.25).Hex}, inset -4px -4px 8px {x.Surface.Darken(0.35).Hex}, 4px 4px 12px rgba(0, 0, 0, 0.3) !important; }}");
                sb.AppendLine($"{submit} {{ background: {x.Accent.Hex} !important; color: {x.Accent.ContrastText} !important; box-shadow: inset 4px 4px 8px {x.Accent.Lighten(0.3).Hex}, inset -4px -4px 8px {x.Accent.Darken(0.3).Hex}, 4px 4px 12px rgba(0, 0, 0, 0.3) !important; }}");
                break;
            case LoginInputStyle.Neumorphism:
                // Raised out of the same color as what is around it (fields are pressed in).
                sb.AppendLine($"{button} {{ background: {buttonBase.Hex} !important; border: 0 !important; color: {x.Text.Hex} !important; box-shadow: 4px 4px 8px {buttonBase.Darken(0.6).Hex}, -4px -4px 8px {buttonBase.Lighten(0.18).Hex} !important; }}");
                sb.AppendLine($"{submit} {{ color: {x.Accent.Hex} !important; font-weight: 700 !important; }}");
                break;
        }

        if (l.InputScale != 100)
        {
            // Padding scales the field height; the text size stays readable.
            sb.AppendLine($"{input} {{ padding-top: {Em(0.4 * l.InputScale / 100.0)} !important; padding-bottom: {Em(0.4 * l.InputScale / 100.0)} !important; }}");
        }

        var titles = $"{x.P}html #loginPage h1.sectionTitle, {x.P}html #loginPage .visualLoginForm > h1";
        if (l.HideTitle)
        {
            sb.AppendLine($"{titles} {{ display: none !important; }}");
        }
        else if (!string.IsNullOrWhiteSpace(l.Title))
        {
            // The heading's own text is shrunk to nothing and the replacement
            // rendered by ::after - CSS cannot edit text, only add to it.
            sb.AppendLine($"{titles} {{ font-size: 0 !important; }}");
            // ::after has to go on each selector of the list, not just the last one.
            sb.AppendLine($"{x.P}html #loginPage h1.sectionTitle::after, {x.P}html #loginPage .visualLoginForm > h1::after {{ content: {CssString(l.Title)}; font-size: 1.6rem; font-weight: 500; }}");
        }

        if (l.HideQuickConnect)
        {
            sb.AppendLine($"{x.P}html #loginPage .btnQuick {{ display: none !important; }}");
        }

        if (l.HideForgotPassword)
        {
            sb.AppendLine($"{x.P}html #loginPage .btnForgotPassword {{ display: none !important; }}");
        }
    }

    private static void AppendMisc(StringBuilder sb, Context x)
    {
        var m = x.Config.Misc;
        if (!m.HideScrollbars)
        {
            return;
        }

        sb.AppendLine("/* --- misc --- */");
        sb.AppendLine($"{x.P}* {{ scrollbar-width: none !important; }}");
        sb.AppendLine($"{x.P}*::-webkit-scrollbar {{ display: none !important; }}");
    }

    /// <summary>
    /// The TV layout does not use the MUI header at all: it keeps the
    /// legacy .skinHeader (.headerTop with .headerLeft / .headerRight, and
    /// .headerTabs with .emby-tab-button links under it). The bar settings
    /// are translated to that markup here - the surface itself already
    /// reaches it through the .skinHeader-withBackground selector. A
    /// sidebar has no TV counterpart; TV keeps the top bar.
    /// </summary>
    private static void AppendTvHeader(StringBuilder sb, Context x)
    {
        var h = x.Config.Header;
        var tv = $"{x.P}html.layout-tv";
        var bar = $"{tv} .skinHeader";
        var color = x.HeaderColor;
        var radius = Px(h.Radius);
        var shadow = StyleShadow(h.Style, color, x) ?? (h.Shadow || x.HeaderFloating ? "0 6px 24px rgba(0, 0, 0, 0.35)" : "none");
        var border = StyleBorder(h.Style, x) ?? (h.BottomBorder ? $"1px solid {x.Text.Rgba(0.1)}" : "0");

        sb.AppendLine("/* --- TV layout: the legacy header --- */");
        if (h.Layout == HeaderLayout.Sections)
        {
            // Islands: the logo group, the icon group and the row of tabs.
            sb.AppendLine($"{bar} {{ background: transparent !important; box-shadow: none !important; backdrop-filter: none !important; -webkit-backdrop-filter: none !important; border: 0 !important; }}");
            sb.AppendLine($"{bar} .headerTop {{ gap: 10px; padding: 8px 12px !important; }}");
            sb.AppendLine($"{bar} .headerLeft, {bar} .headerRight, {bar} .headerTabs .emby-tabs-slider {{ {Surface(h.Style, color, h.Opacity, h.Blur, x, "90deg")} border-radius: {Px(h.SectionRadius)} !important; padding: 2px 10px !important; box-shadow: {shadow} !important; border: {border}; }}");
            sb.AppendLine($"{bar} .headerTabs .emby-tabs-slider {{ display: inline-flex !important; }}");
        }
        else
        {
            sb.AppendLine($"{bar} {{ {Surface(h.Style, color, h.Opacity, h.Blur, x, "90deg")} }}");
            if (x.HeaderFloating)
            {
                sb.AppendLine($"{bar} {{ margin: 8px 12px 0 !important; border-radius: {radius} !important; box-shadow: {shadow} !important; border-bottom: {border}; }}");
            }
            else
            {
                sb.AppendLine($"{bar} {{ border-radius: 0 0 {radius} {radius} !important; box-shadow: {shadow} !important; border-bottom: {border}; }}");
            }
        }

        // Jellyfin pulls the row of tabs up into the top row with a fixed
        // negative margin (-4.3em), sized for its own row height. Once the
        // row grows (bar height, icon scale) the tabs land above the bar
        // and the bar's box ends short of the icons. Taking the tabs out
        // of the flow instead - absolutely centred over the top row - keeps
        // the bar's box equal to the row whatever its height.
        sb.AppendLine($"{bar} {{ position: relative; }}");
        sb.AppendLine($"{bar} .headerTabs {{ position: absolute !important; top: 0; bottom: 0; left: 0; right: 0; width: auto !important; max-width: none !important; margin: 0 !important; display: flex !important; align-items: center; justify-content: center; pointer-events: none; }}");
        sb.AppendLine($"{bar} .headerTabs .emby-tabs-slider {{ pointer-events: auto; }}");

        // The page sits under the bar with Jellyfin's own 134px of top
        // padding, which fits its default row. A taller row (bar height,
        // icon scale, islands, floating) and the info strip need more;
        // the row height is estimated the same way it is built.
        // The row is as tall as its buttons (2.78em of the 20px base font,
        // scaled) plus the row's padding (16px each side; 8px + 2px island
        // padding for the islands), or the set height if that is more.
        var tvHeight = x.Config.Tv.BarHeight > 0 ? x.Config.Tv.BarHeight : h.Height;
        var buttons = 55.6 * x.Config.Tv.BarScale / 100;
        var rowEstimate = (int)Math.Ceiling(Math.Max(tvHeight, buttons + (h.Layout == HeaderLayout.Sections ? 20 : 32))) + (x.HeaderFloating ? 8 : 0);
        sb.AppendLine($"{tv} .mainAnimatedPage {{ margin-top: max(0px, calc({Px(rowEstimate)} + var(--jellycanvas-info, 0px) + 20px - 134px)) !important; }}");

        // The TV bar has its own height setting (under TV): Jellyfin's row is low.
        if (tvHeight > 0)
        {
            sb.AppendLine($"{bar} .headerTop {{ box-sizing: border-box; min-height: {Px(tvHeight)} !important; }}");
        }

        if (x.Config.Tv.BarScale != 100)
        {
            sb.AppendLine($"{bar} .headerButton, {bar} .emby-tab-button, {bar} .pageTitle, {bar} .currentTimeText {{ font-size: {x.Config.Tv.BarScale}% !important; }}");
            sb.AppendLine($"{bar} .headerButton .material-icons {{ font-size: 1.6em !important; }}");
        }

        if (h.Style == SurfaceStyle.NeoBrutalism)
        {
            sb.AppendLine($"{bar} .headerButton, {bar} .emby-tab-button, {bar} .pageTitle, {bar} .currentTimeText {{ color: {color.ContrastText} !important; }}");
        }

        // Navigation: the TV tabs (Home, Favorites, libraries) under the top row.
        var tab = $"{bar} .emby-tab-button";
        switch (h.Nav)
        {
            case NavStyle.Pill:
                sb.AppendLine($"{tab} {{ border-radius: 999px !important; padding: 0.35em 1.1em !important; margin: 0 0.2em !important; }}");
                sb.AppendLine($"{tab}.emby-tab-button-active {{ background: {x.Accent.Hex} !important; color: {x.Accent.ContrastText} !important; }}");
                sb.AppendLine($"{tab}.emby-tab-button-active .emby-button-foreground {{ color: inherit !important; }}");
                break;
            case NavStyle.Underline:
                sb.AppendLine($"{tab} {{ border-bottom: 2px solid transparent !important; }}");
                sb.AppendLine($"{tab}.emby-tab-button-active {{ border-bottom-color: {x.Accent.Hex} !important; color: {x.Text.Hex} !important; }}");
                break;
        }

        if (h.HideSyncPlay)
        {
            sb.AppendLine($"{bar} .headerSyncButton {{ display: none !important; }}");
        }

        if (h.HideCast)
        {
            sb.AppendLine($"{bar} .headerCastButton {{ display: none !important; }}");
        }

        if (h.HideSearch)
        {
            sb.AppendLine($"{bar} .headerSearchButton {{ display: none !important; }}");
        }

        if (h.HideLogo)
        {
            sb.AppendLine($"{bar} .headerLeft .pageTitle {{ display: none !important; }}");
        }
        else if (h.Logo == LogoImage.Custom && !string.IsNullOrWhiteSpace(h.LogoUrl))
        {
            var height = Px(Math.Max(16, h.LogoHeight));
            var width = h.LogoWidth > 0 ? Px(h.LogoWidth) : "auto";
            sb.AppendLine($"{bar} .pageTitleWithDefaultLogo {{ background-image: url({CssUrl(h.LogoUrl)}) !important; background-size: contain !important; height: {height} !important; width: {width} !important; min-width: {height}; }}");
        }
        else if (h.Logo == LogoImage.Hidden)
        {
            sb.AppendLine($"{bar} .pageTitleWithDefaultLogo {{ background-image: none !important; }}");
        }

        // The info bar. The legacy header is position: fixed and its tabs
        // are pulled up into the top row with a negative margin, so the
        // header's own box is shorter than what is visible - a strip in
        // the flow or at the header's 100% would land on the icons. It
        // hangs off the top row instead (.headerTop::after at 100%); the
        // page's margin above accounts for it.
        var i = x.Config.InfoBar;
        if (i.Enabled && !string.IsNullOrWhiteSpace(i.Text) && i.Position == InfoBarPosition.Top)
        {
            var bg = Color.Parse(i.Color, x.Accent);
            var fg = string.IsNullOrWhiteSpace(i.TextColor) ? bg.ContrastText : Color.Parse(i.TextColor, x.Text).Hex;
            var edge = i.Radius > 0 ? "10px" : "0px";
            // Jellyfin gives .skinHeader "contain: content", which paints
            // nothing outside its 41px box - the strip below it would be
            // clipped away, so the paint containment goes.
            sb.AppendLine($"{bar} {{ contain: layout style !important; }}");
            sb.AppendLine($"{bar} .headerTop {{ position: relative; }}");
            sb.AppendLine($"{bar} .headerTop::after {{ content: {CssString(i.Text)}; position: absolute; top: 100%; left: {edge}; right: {edge}; z-index: 2; display: flex; align-items: center; justify-content: center; box-sizing: border-box; min-height: {Px(Math.Max(20, i.Height))}; padding: 0.3em 1em; background: {bg.Hex}; color: {fg}; font-size: 0.92em; font-weight: 600; line-height: 1.3; text-align: center; white-space: normal; overflow-wrap: anywhere; border-radius: {Px(i.Radius)}; {(i.Radius > 0 ? "margin-top: 6px;" : string.Empty)} }}");
        }
    }

    private static void AppendTv(StringBuilder sb, Context x)
    {
        var tv = x.Config.Tv;
        sb.AppendLine("/* --- TV layout (remote control focus) --- */");

        // The focused element on TV is drawn in secondary-main; override it
        // for TV separately so a color other than the accent can be chosen.
        if (!string.IsNullOrWhiteSpace(tv.FocusColor))
        {
            var f = Color.Parse(tv.FocusColor, x.Accent);
            sb.AppendLine($"{x.RootTv} {{ --jf-palette-secondary-main: {f.Hex} !important; --jf-palette-secondary-mainChannel: {f.Channel} !important; --jf-palette-secondary-contrastText: {f.ContrastText} !important; }}");
        }

        AppendTvHeader(sb, x);

        sb.AppendLine($"{x.P}html.layout-tv .card.show-focus:not(.show-animation) .cardBox:not(.visualCardBox) .cardScalable {{ border-width: {Px(tv.FocusWidth)} !important; }}");
        if (tv.FocusScale != 100)
        {
            sb.AppendLine($"{x.P}html.layout-tv .card:focus .cardBox:not(.visualCardBox) .cardScalable {{ transform: scale({Dec(tv.FocusScale / 100.0)}); transition: transform 0.15s ease; }}");
            sb.AppendLine($"{x.P}html.layout-tv .card {{ contain: none !important; }}");
        }
    }

    private static void AppendMobile(StringBuilder sb, Context x)
    {
        var m = x.Config.Mobile;
        if (m.CardRadius < 0 && m.FontScale <= 0)
        {
            return;
        }

        sb.AppendLine("/* --- mobile layout --- */");
        if (m.CardRadius >= 0)
        {
            sb.AppendLine($"{x.RootMobile} {{ --jf-card-borderRadius: {Px(m.CardRadius)} !important; }}");
        }

        if (m.FontScale > 0)
        {
            sb.AppendLine($"{x.P}html.layout-mobile body {{ font-size: {m.FontScale}% !important; }}");
        }
    }

    // ------------------------------------------------------------------
    // Helpers.
    // ------------------------------------------------------------------

    /// <summary>
    /// Background declarations for one "surface" (bar, island, menu, dialog):
    /// solid / glass / gradient / transparent. The backdrop blur is added
    /// whenever it is set and the surface is not fully opaque - on a solid
    /// color at 100% it would not be visible anyway.
    /// </summary>
    private static string Surface(SurfaceStyle style, Color color, int opacity, int blur, Context x, string gradientAngle)
    {
        var filter = IsTranslucent(style, opacity) && blur > 0
            ? $"backdrop-filter: blur({Px(blur)}) saturate(1.4) !important; -webkit-backdrop-filter: blur({Px(blur)}) saturate(1.4) !important;"
            : "backdrop-filter: none !important; -webkit-backdrop-filter: none !important;";
        var shadow = $"box-shadow: {StyleShadow(style, color, x) ?? "none"} !important;";
        var border = StyleBorder(style, x) is { } b ? $" border: {b} !important;" : string.Empty;
        var accent = x.Accent;

        return style switch
        {
            SurfaceStyle.Glass => $"background-color: {color.RgbaPercent(opacity)} !important; background-image: none !important; {shadow} {filter}",
            SurfaceStyle.Gradient => $"background-color: transparent !important; background-image: linear-gradient({gradientAngle}, {color.RgbaPercent(opacity)}, {accent.Rgba(opacity / 100.0 * 0.85)}) !important; {shadow} {filter}",
            SurfaceStyle.Transparent => $"background: transparent !important; {shadow} {filter}",
            // Brutalism is flat and opaque by definition - opacity is ignored.
            SurfaceStyle.NeoBrutalism => $"background-color: {color.Hex} !important; background-image: none !important; {shadow}{border} {filter}",
            // Glow: glass with a faint accent tint fading across the surface.
            SurfaceStyle.Glowmorphism => $"background-color: {color.RgbaPercent(opacity)} !important; background-image: linear-gradient({gradientAngle}, {accent.Rgba(0.16)}, transparent 65%) !important; {shadow}{border} {filter}",
            // Clay is a touch lighter than the chosen color so the inner shading has room.
            SurfaceStyle.Claymorphism => $"background-color: {color.Lighten(0.08).RgbaPercent(opacity)} !important; background-image: none !important; {shadow} {filter}",
            // Neumorphism lives on the shadows alone; the fill must match the surroundings.
            SurfaceStyle.Neumorphism => $"background-color: {color.Hex} !important; background-image: none !important; {shadow} {filter}",
            _ => $"background-color: {color.RgbaPercent(opacity)} !important; background-image: none !important; {shadow} {filter}",
        };
    }

    /// <summary>Whether the page behind the surface shows through (and a backdrop blur makes sense).</summary>
    private static bool IsTranslucent(SurfaceStyle style, int opacity)
        => style is SurfaceStyle.Transparent or SurfaceStyle.Glass or SurfaceStyle.Glowmorphism || opacity < 100;

    /// <summary>The shadow that is part of a style's look; null for styles without one.</summary>
    private static string? StyleShadow(SurfaceStyle style, Color color, Context x) => style switch
    {
        SurfaceStyle.NeoBrutalism => $"4px 4px 0 {x.Outline.Hex}",
        SurfaceStyle.Glowmorphism => $"0 0 0 1px {x.Accent.Rgba(0.55)}, 0 0 24px {x.Accent.Rgba(0.45)}, 0 8px 32px rgba(0, 0, 0, 0.35)",
        SurfaceStyle.Claymorphism => $"inset 6px 6px 14px {color.Lighten(0.22).Hex}, inset -6px -6px 14px {color.Darken(0.35).Hex}, 10px 10px 28px rgba(0, 0, 0, 0.35)",
        SurfaceStyle.Neumorphism => $"8px 8px 18px {color.Darken(0.6).Hex}, -8px -8px 18px {color.Lighten(0.18).Hex}",
        _ => null,
    };

    /// <summary>The border that is part of a style's look; null for styles without one.</summary>
    private static string? StyleBorder(SurfaceStyle style, Context x) => style switch
    {
        SurfaceStyle.NeoBrutalism => $"3px solid {x.Outline.Hex}",
        SurfaceStyle.Glowmorphism => $"1px solid {x.Accent.Rgba(0.45)}",
        _ => null,
    };

    /// <summary>A URL for url() - quoted and escaped so the value cannot break out of the CSS.</summary>
    private static string CssUrl(string url)
        => "\"" + url.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal).Replace("\r", string.Empty, StringComparison.Ordinal) + "\"";

    // InvariantCulture everywhere: a Czech (or German...) server locale would
    // write the decimal separator as "0,5" and CSS would not understand it.
    private static void Var(StringBuilder sb, string name, string value)
        => sb.AppendLine($"  {name}: {value} !important;");

    private static string Px(int v) => v <= 0 ? "0" : v.ToString(CultureInfo.InvariantCulture) + "px";

    private static string Em(double v) => v.ToString("0.##", CultureInfo.InvariantCulture) + "em";

    private static string Dec(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Pct(double fraction) => (fraction * 100).ToString("0.##", CultureInfo.InvariantCulture) + "%";

    /// <summary>
    /// Colors and selectors computed once per build so the sections do not
    /// have to compute them again (and, more importantly, identically).
    /// </summary>
    private sealed class Context
    {
        public Context(PluginConfiguration config)
        {
            Config = config;
            Accent = Color.Parse(config.Colors.Accent, "#00a4dc");
            Background = Color.Parse(config.Colors.Background, "#101010");
            Surface = Color.Parse(config.Colors.Surface, "#202020");
            Text = Color.Parse(config.Colors.Text, "#ffffff");
            Outline = Color.Parse(config.Colors.Outline, "#000000");
            // Neumorphism only works when the surface has the same color as
            // what is around it, so its default is the page background, not
            // the surface color.
            // Neo-brutalism lives on a loud fill - the surface color would be
            // near-invisible next to the page, leaving just a frame.
            HeaderColor = Color.Parse(config.Header.Color, config.Header.Style switch
            {
                SurfaceStyle.Neumorphism => Background,
                SurfaceStyle.NeoBrutalism => Accent,
                _ => Surface,
            });
            Focus = Color.Parse(config.Tv.FocusColor, Accent);

            // ApplyTo.DarkOnly: leave the light theme alone. Done with a
            // selector prefix - "html:not([data-theme=light])" only matches
            // on dark themes.
            var darkOnly = config.ApplyTo == ApplyTo.DarkOnly;
            P = darkOnly ? "html:not([data-theme=\"light\"]) " : string.Empty;

            // Palette variables: MUI writes them on [data-theme="dark"]
            // (specificity 0,1,0). "html[data-theme]" is 0,1,1 and always wins;
            // ":root" additionally covers the moment after load when the
            // attribute is not set yet.
            Root = darkOnly
                ? "html[data-theme]:not([data-theme=\"light\"])"
                : ":root, html[data-theme]";
            RootTv = darkOnly ? "html.layout-tv[data-theme]:not([data-theme=\"light\"])" : "html.layout-tv, html.layout-tv[data-theme]";
            RootMobile = darkOnly ? "html.layout-mobile[data-theme]:not([data-theme=\"light\"])" : "html.layout-mobile, html.layout-mobile[data-theme]";
        }

        public PluginConfiguration Config { get; }

        public Color Accent { get; }

        public Color Background { get; }

        public Color Surface { get; }

        public Color Text { get; }

        /// <summary>Frame and hard-shadow color of neo-brutalist surfaces.</summary>
        public Color Outline { get; }

        /// <summary>The ribbon style, honouring the old "transparent" checkbox of saved themes.</summary>
        public RibbonStyle RibbonStyle => Config.Detail.Ribbon == RibbonStyle.SameAsBar && Config.Detail.TransparentRibbon ? RibbonStyle.Transparent : Config.Detail.Ribbon;

        /// <summary>The library row height to set, clamped to what the controls need; 0 = leave Jellyfin's own 52px alone.</summary>
        public int LibraryRowSetHeight => Config.Header.LibraryRowHeight > 0 && Config.Header.LibraryRowHeight != 52 ? Math.Max(44, Config.Header.LibraryRowHeight) : 0;

        /// <summary>
        /// Space the library row takes next to a sidebar, for the page offset
        /// under it: its height (52px by default), the brutalist frame, the
        /// inset a rounded row gets, and a little air so nothing touches.
        /// </summary>
        public int LibraryRowHeight
        {
            get
            {
                var h = Config.Header;
                return (LibraryRowSetHeight > 0 ? LibraryRowSetHeight : 52)
                    + ((h.LibraryRow == LibraryRowStyle.NeoBrutalism || (h.LibraryRow == LibraryRowStyle.SameAsBar && h.Style == SurfaceStyle.NeoBrutalism)) ? 6 : 0)
                    + (h.LibraryRowRadius > 0 ? 6 : 0)
                    + 4;
            }
        }

        public Color HeaderColor { get; }

        /// <summary>
        /// Whether the bar sits away from the window edges. Neo-brutalism
        /// forces it: its frame and offset shadow are the whole point, and a
        /// bar glued to the edges would show a single stripe of them.
        /// </summary>
        public bool HeaderFloating => Config.Header.Floating || Config.Header.Style == SurfaceStyle.NeoBrutalism;

        public Color Focus { get; }

        /// <summary>Height the info bar takes at the top of the content (0 when off or at the bottom).</summary>
        public int InfoBarTopHeight => Config.InfoBar.Enabled && !string.IsNullOrWhiteSpace(Config.InfoBar.Text) && Config.InfoBar.Position == InfoBarPosition.Top
            ? Math.Max(20, Config.InfoBar.Height)
            : 0;

        /// <summary>Prefix for every selector (empty, or the dark-only restriction).</summary>
        public string P { get; }

        public string Root { get; }

        public string RootTv { get; }

        public string RootMobile { get; }
    }
}
