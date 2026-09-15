using System.Text;

namespace Jellyfin.Plugin.Jellycanvas.Theme;

/// <summary>
/// The rules of Jellyfin 12's theme stylesheet that read the --jf-* CSS
/// variables, repeated in the theme's own words. A server upgraded from
/// 10.x can be left with the old themes/*/theme.css (the upgrade does not
/// always replace it), and the old file has these places hard-coded: the
/// palette this plugin sets is then ignored there, and --jf-card-borderRadius
/// is never read at all. Written after the theme stylesheet with the same
/// specificity, these rules are a no-op on a current install and put the
/// variables back to work on a stale one.
/// </summary>
/// <remarks>
/// Extracted from jellyfin-web 12.1 themes/dark/theme.css (GPL-2.0,
/// https://github.com/jellyfin/jellyfin-web) - only the declarations that
/// reference a variable; every theme ships the same ones.
/// </remarks>
public static class ThemeBridge
{
    /// <summary>Marks a theme.css the repair has already been appended to.</summary>
    public const string RepairMarker = "/* === JELLYCANVAS THEME BRIDGE === */";

    /// <summary>The variable that tells a current theme.css (12.x) from an old one.</summary>
    public const string Signature = "--jf-card-borderRadius";

    private static readonly (string Selector, string Declarations)[] Rules =
    [
        (".skinHeader,html", "color: var(--jf-palette-text-secondary, hsla(0, 0%, 100%, .7));"),
        (".ui-corner-all,.ui-shadow,.wizardStartForm", "background-color: var(--jf-palette-background-default, #101010); background-image: var(--jf-palette-background-defaultImage, none);"),
        (".emby-collapsible-button", "border-color: var(--jf-palette-divider, hsla(0, 0%, 100%, .12));"),
        (".detailRibbon,.skinHeader-withBackground", "background-color: var(--jf-palette-AppBar-defaultBg, #202020); background-image: var(--jf-palette-AppBar-gradient, none);"),
        (".skinHeader.semiTransparent", "background-color: var(--jf-palette-AppBar-transparentBg, rgba(0, 0, 0, .4));"),
        (".backgroundContainer,.nowPlayingPlaylist,html", "background-color: var(--jf-palette-background-default, #101010); background-image: var(--jf-palette-background-defaultImage, none);"),
        (".dialog", "background-color: var(--jf-palette-background-default, #101010);"),
        (".paper-icon-button-light:hover:not(:disabled)", "background-color: rgba(var(--jf-palette-primary-mainChannel)/var(--jf-palette-action-selectedOpacity)); color: var(--jf-palette-primary-main, #00a4dc);"),
        (".paper-icon-button-light:active:not(:disabled)", "background-color: rgba(var(--jf-palette-primary-mainChannel)/var(--jf-palette-action-selectedOpacity)); color: var(--jf-palette-primary-main, #00a4dc);"),
        (".paper-icon-button-light.show-focus:focus", "color: var(--jf-palette-secondary-main, #00a4dc);"),
        (".emby-scrollbuttons,.fab,.raised,a[data-role=button]", "color: var(--jf-palette-text-secondary, hsla(0, 0%, 100%, .7));"),
        (".fab,.raised,a[data-role=button]", "background: var(--jf-palette-Button-inheritContainedBg, #424242);"),
        (".fab:hover,.raised:hover,a[data-role=button]:hover", "background: var(--jf-palette-Button-inheritContainedHoverBg, #616161);"),
        (".fab:focus,.raised:focus,a[data-role=button]:focus", "background: var(--jf-palette-secondary-main, #00a4dc);"),
        (".button-submit", "background: var(--jf-palette-primary-main, #00a4dc); color: var(--jf-palette-primary-contrastText, rgba(0, 0, 0, .87));"),
        (".button-submit:hover", "background: var(--jf-palette-primary-dark, #00729a);"),
        (".button-submit:focus", "background: var(--jf-palette-secondary-main, #00a4dc); color: var(--jf-palette-secondary-contrastText, rgba(0, 0, 0, .87));"),
        (".button-delete", "background: var(--jf-palette-error-main, #c62828); color: var(--jf-palette-error-contrastText, #fff);"),
        (".checkboxListLabel,.inputLabel,.inputLabelUnfocused,.paperListLabel,.textareaLabelUnfocused", "color: var(--jf-palette-text-secondary, hsla(0, 0%, 100%, .7));"),
        (".inputLabelFocused,.selectLabelFocused,.textareaLabelFocused", "color: var(--jf-palette-secondary-main, #00a4dc);"),
        (".blurhash-canvas,.cardBox:not(.visualCardBox) .cardPadder,.cardContent,.cardImageContainer,.cardOverlayContainer,.itemDetailImage,.paperList,.visualCardBox", "border-radius: var(--jf-card-borderRadius, .2em);"),
        (".cardText-secondary,.fieldDescription,.guide-programNameCaret,.listItem .secondary,.nowPlayingBarSecondaryText,.programSecondaryTitle,.secondaryText", "color: var(--jf-palette-text-secondary, hsla(0, 0%, 100%, .7));"),
        (".actionsheetDivider", "background: var(--jf-palette-divider, hsla(0, 0%, 100%, .12));"),
        (".toast", "background: var(--jf-palette-SnackbarContent-bg, #303030); color: var(--jf-palette-SnackbarContent-color, hsla(0, 0%, 100%, .87));"),
        (".appfooter,.playlistSectionButton", "background: var(--jf-palette-background-paper, #202020); color: var(--jf-palette-text-secondary, hsla(0, 0%, 100%, .7));"),
        (".itemSelectionPanel", "border-color: var(--jf-palette-primary-main, #00a4dc);"),
        (".selectionCommandsPanel", "background: var(--jf-palette-primary-main, #00a4dc); color: var(--jf-palette-primary-contrastText, rgba(0, 0, 0, .87)) !important;"),
        (".selectionCommandsPanel .paper-icon-button-light:not(:disabled)", "color: var(--jf-palette-primary-contrastText, rgba(0, 0, 0, .87));"),
        (".selectionCommandsPanel .paper-icon-button-light:not(:disabled):focus,.selectionCommandsPanel .paper-icon-button-light:not(:disabled):hover", "background-color: var(--jf-palette-primary-dark, #00729a);"),
        (".upNextDialog-countdownText", "color: var(--jf-palette-primary-main, #00a4dc);"),
        (".alphaPickerButton", "color: var(--jf-palette-text-secondary, hsla(0, 0%, 100%, .7));"),
        (".alphaPickerButton-selected", "color: var(--jf-palette-text-primary, #fff);"),
        (".alphaPickerButton-tv:focus", "background-color: var(--jf-palette-secondary-main, #00a4dc); color: var(--jf-palette-secondary-contrastText, rgba(0, 0, 0, .87));"),
        (".noBackdropTransparency .detailPageWrapperContainer", "background-color: var(--jf-palette-background-default, #101010); background-image: var(--jf-palette-background-defaultImage, none);"),
        (".listItem-border", "border-color: var(--jf-palette-divider, hsla(0, 0%, 100%, .12));"),
        (".listItem:focus", "background-color: var(--jf-palette-action-focus, hsla(0, 0%, 100%, .12));"),
        (".listItem:hover", "background-color: var(--jf-palette-action-hover, hsla(0, 0%, 100%, .08));"),
        (".progressring-spiner", "border-color: var(--jf-palette-primary-main, #00a4dc);"),
        (".button-flat:hover,.button-link", "color: var(--jf-palette-primary-main, #00a4dc);"),
        (".emby-input,.emby-textarea", "background: var(--jf-palette-FilledInput-bg, hsla(0, 0%, 100%, .09)); border-color: var(--jf-palette-FilledInput-borderColor, hsla(0, 0%, 100%, .09));"),
        (".emby-input:focus,.emby-textarea:focus", "border-color: var(--jf-palette-secondary-main, #00a4dc);"),
        (".emby-select-withcolor", "border-color: var(--jf-palette-FilledInput-borderColor, hsla(0, 0%, 100%, .09));"),
        (".emby-select-withcolor,.emby-select-withcolor>option", "background: var(--jf-palette-background-paper, #202020);"),
        (".emby-select-withcolor:focus", "border-color: var(--jf-palette-secondary-main, #00a4dc) !important;"),
        (".emby-select-tv-withcolor:focus", "background-color: var(--jf-palette-secondary-main, #00a4dc) !important; color: var(--jf-palette-secondary-contrastText, rgba(0, 0, 0, .87)) !important;"),
        (".emby-checkbox:checked+span+.checkboxOutline", "border-color: var(--jf-palette-primary-main, #00a4dc);"),
        (".emby-checkbox:focus+span+.checkboxOutline", "border-color: var(--jf-palette-common-white, #fff);"),
        (".emby-checkbox:checked+span+.checkboxOutline,.itemProgressBarForeground", "background-color: var(--jf-palette-primary-main, #00a4dc);"),
        (".emby-checkbox:focus:not(:checked)+span+.checkboxOutline", "border-color: var(--jf-palette-secondary-main, #00a4dc);"),
        (".itemProgressBarForeground-recording", "background-color: var(--jf-palette-error-light, #d15353);"),
        (".countIndicator,.fullSyncIndicator,.mediaSourceIndicator,.playedIndicator", "background: var(--jf-palette-primary-main, #00a4dc); color: var(--jf-palette-primary-contrastText, rgba(0, 0, 0, .87));"),
        (".countIndicator>svg,.fullSyncIndicator>svg,.mediaSourceIndicator>svg,.playedIndicator>svg", "fill: var(--jf-palette-primary-contrastText, rgba(0, 0, 0, .87));"),
        (".drawer-open,.mainDrawer", "background-color: var(--jf-palette-background-default, #101010);"),
        (".navMenuOption:hover", "background: var(--jf-palette-action-hover, hsla(0, 0%, 100%, .08));"),
        (".navMenuOption-selected", "background: var(--jf-palette-primary-main, #00a4dc) !important;"),
        (".emby-button.show-focus:focus,.navMenuOption-selected", "color: var(--jf-palette-primary-contrastText, rgba(0, 0, 0, .87));"),
        (".emby-button.show-focus:focus", "background: var(--jf-palette-primary-main, #00a4dc);"),
        (".emby-tab-button", "color: var(--jf-palette-text-secondary, hsla(0, 0%, 100%, .7));"),
        (".emby-tab-button-active", "color: var(--jf-palette-text-primary, #fff);"),
        (".emby-tab-button.show-focus:focus,.emby-tab-button:hover", "color: var(--jf-palette-secondary-main, #00a4dc);"),
        (".guide-channelHeaderCell:focus,.programCell:focus", "background-color: var(--jf-palette-primary-main, #00a4dc) !important; color: var(--jf-palette-primary-contrastText, rgba(0, 0, 0, .87)) !important;"),
        (".guide-date-tab-button.emby-tab-button-active,.guide-date-tab-button:focus", "color: var(--jf-palette-secondary-main, #00a4dc);"),
        (".guide-date-tab-button.show-focus:focus", "background-color: var(--jf-palette-primary-main, #00a4dc); color: var(--jf-palette-primary-contrastText, rgba(0, 0, 0, .87));"),
        (".infoBanner", "background: var(--jf-palette-Alert-infoFilledBg, #0288d1); color: var(--jf-palette-Alert-infoFilledColor, #fff);"),
        (".playstatebutton-icon-played,.ratingbutton-icon-withrating", "color: var(--jf-palette-error-light, #d15353);"),
        (".buttonActive", "color: var(--jf-palette-primary-main, #00a4dc) !important;"),
        (".card:focus .cardBox.visualCardBox,.card:focus .cardBox:not(.visualCardBox) .cardScalable", "border-color: var(--jf-palette-secondary-main, #00a4dc) !important; border-radius: var(--jf-card-borderRadius, .2em);"),
        (".card.show-focus:not(.show-animation) .cardBox.visualCardBox,.card.show-focus:not(.show-animation) .cardBox:not(.visualCardBox) .cardScalable", "border-radius: calc(var(--jf-card-borderRadius, .2em) + .5em);"),
        (".metadataSidebarIcon", "color: var(--jf-palette-primary-main, #00a4dc);"),
        ("#bookPlayer,#comicsPlayer,#comicsPlayer .swiper-pagination,#pdfPlayer", "background-color: var(--jf-palette-background-default, #101010);"),
        ("#comicsPlayer .swiper-pagination", "color: var(--jf-palette-text-primary, #fff);"),
        ("#dialogToc", "background-color: var(--jf-palette-background-default, #101010);"),
        ("#dialogToc .bookplayerButtonIcon,#dialogToc .toc li a", "color: var(--jf-palette-text-secondary, hsla(0, 0%, 100%, .7));"),
        ("#dialogToc .bookplayerButtonIcon:active,#dialogToc .bookplayerButtonIcon:hover,#dialogToc .toc li a:active,#dialogToc .toc li a:hover", "color: var(--jf-palette-primary-main, #00a4dc);"),
        (".mdl-radio.show-focus .mdl-radio__button:focus+.mdl-radio__circles svg .mdl-radio__inner-circle,.mdl-radio.show-focus .mdl-radio__button:focus+.mdl-radio__circles svg .mdl-radio__outer-circle", "color: var(--jf-palette-secondary-main, #00a4dc);"),
        (".detailRibbon", "background: rgba(var(--jf-palette-background-paperChannel, 32 32 32)/.8);"),
    ];

    /// <summary>Appends the rules, each selector prefixed with the scope (empty, or the dark-only guard).</summary>
    /// <param name="sb">Where the CSS goes.</param>
    /// <param name="prefix">Selector prefix ending in a space, or empty.</param>
    public static void Append(StringBuilder sb, string prefix)
    {
        sb.AppendLine("/* --- theme bridge: the theme stylesheet's variable rules, for servers with an old themes/*/theme.css --- */");
        foreach (var (selector, declarations) in Rules)
        {
            var parts = selector.Split(',');
            for (var i = 0; i < parts.Length; i++)
            {
                // "html" itself cannot sit under the prefix (which starts with html).
                parts[i] = parts[i] == "html"
                    ? (prefix.Length > 0 ? prefix.TrimEnd() : "html")
                    : prefix + parts[i];
            }

            sb.Append(string.Join(", ", parts)).Append(" { ").Append(declarations).AppendLine(" }");
        }
    }

    /// <summary>The block appended to an old theme.css on disk: the rules, unscoped, between markers.</summary>
    public static string RepairBlock()
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine(RepairMarker);
        sb.AppendLine("/* Appended by the Jellycanvas plugin: this theme.css predates Jellyfin 12 and does not read the --jf-* variables. Reinstalling jellyfin-web replaces the file properly. */");
        Append(sb, string.Empty);
        sb.AppendLine(RepairMarker);
        return sb.ToString();
    }
}
