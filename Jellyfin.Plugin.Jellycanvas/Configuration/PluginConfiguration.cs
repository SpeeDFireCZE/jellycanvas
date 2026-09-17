using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Jellycanvas.Configuration;

/// <summary>
/// Fill style for "bars" and surfaces - the top bar, the side menu, dialogs.
/// One value, four looks; how each is built lives in the CssBuilder.
/// </summary>
public enum SurfaceStyle
{
    /// <summary>Solid color.</summary>
    Solid,

    /// <summary>Frosted glass: translucent color with the background blurred behind it.</summary>
    Glass,

    /// <summary>Gradient from the surface color to the accent color.</summary>
    Gradient,

    /// <summary>No fill - just the content over the page background.</summary>
    Transparent,

    /// <summary>Neo-brutalism: flat color, thick contrasting border, hard offset shadow.</summary>
    NeoBrutalism,

    /// <summary>Glowmorphism: translucent surface with a soft glow in the accent color around it.</summary>
    Glowmorphism,

    /// <summary>Claymorphism: a puffy, clay-like surface with an inner highlight and shadow.</summary>
    Claymorphism,

    /// <summary>Neumorphism: the surface color itself, raised by a light and a dark shadow.</summary>
    Neumorphism,
}

/// <summary>What happens to a card when the mouse hovers it (or the TV cursor lands on it).</summary>
public enum CardHover
{
    None,
    Lift,
    Zoom,
    Glow,
}

/// <summary>Where a card's title and secondary text sit.</summary>
public enum CardText
{
    /// <summary>Below the image - Jellyfin's default.</summary>
    Below,

    /// <summary>Over the bottom edge of the image.</summary>
    Overlay,

    /// <summary>Hidden, image only.</summary>
    Hidden,
}

/// <summary>Button look.</summary>
/// <summary>Look of the second row of the bar on library pages (title, sort, filter, view buttons).</summary>
public enum LibraryRowStyle
{
    /// <summary>Same surface as the bar.</summary>
    SameAsBar,

    /// <summary>Hidden altogether (the controls are still reachable through the library menu).</summary>
    Hidden,

    // A surface of its own, in the row's color - the same looks as the bar.
    Solid,
    Glass,
    Gradient,
    Transparent,
    NeoBrutalism,
    Glowmorphism,
    Claymorphism,
    Neumorphism,
}

public enum ButtonStyle
{
    Filled,
    Outline,
    Soft,
}

/// <summary>Look of the main Play button on the item detail page.</summary>
public enum PlayStyle
{
    /// <summary>Same as the other detail-page buttons.</summary>
    Inherit,

    /// <summary>Filled with the play color (accent by default).</summary>
    Accent,

    /// <summary>Outlined in the play color.</summary>
    Outline,

    /// <summary>Tinted with the play color.</summary>
    Soft,
}

/// <summary>UI font.</summary>
public enum FontFamily
{
    /// <summary>Noto Sans, bundled with Jellyfin.</summary>
    Default,

    /// <summary>The device's system font (Segoe UI, San Francisco, Roboto...).</summary>
    System,

    Inter,
    Roboto,
    Poppins,
    Nunito,

    /// <summary>A custom name - the value in <see cref="TypographySettings.CustomFamily"/>.</summary>
    Custom,
}

/// <summary>Which Jellyfin themes the generated CSS applies to.</summary>
public enum ApplyTo
{
    /// <summary>All of them (Dark, Light, Purple Haze...).</summary>
    All,

    /// <summary>Dark themes only - Light stays untouched.</summary>
    DarkOnly,
}

/// <summary>Top bar layout.</summary>
public enum HeaderLayout
{
    /// <summary>One continuous bar across the full width.</summary>
    Full,

    /// <summary>The bar is transparent and each of its groups (logo + links, icons, user) gets its own "island".</summary>
    Sections,

    /// <summary>A vertical panel on the left (logo on top, links below it, icons and user at the bottom) - the pre-12 feel. Desktop and TV only; phones keep the top bar.</summary>
    Sidebar,
}

/// <summary>Look of the navigation links in the bar (Favorites, Movies, Shows...).</summary>
public enum NavStyle
{
    /// <summary>Plain text, active link in the accent color - the default.</summary>
    Text,

    /// <summary>Pills; the active link is filled with the accent color.</summary>
    Pill,

    /// <summary>The active link is underlined.</summary>
    Underline,
}

/// <summary>The logo image in the bar.</summary>
public enum LogoImage
{
    /// <summary>The Jellyfin icon.</summary>
    Default,

    /// <summary>A custom image from <see cref="HeaderSettings.LogoUrl"/> (or one uploaded through the plugin page).</summary>
    Custom,

    /// <summary>No image.</summary>
    Hidden,
}

/// <summary>How a card shows that an item has been played.</summary>
public enum PlayedStyle
{
    /// <summary>A check-mark circle inset from the corner - the default.</summary>
    Badge,

    /// <summary>The check mark tucked into the card corner, rounded on the inside only.</summary>
    CornerBadge,

    /// <summary>The card image dimmed (translucent), no badge.</summary>
    Dimmed,

    /// <summary>The card image in black and white, no badge.</summary>
    Grayscale,

    /// <summary>Nothing - the badge is hidden.</summary>
    Hidden,
}

/// <summary>The resume-progress indicator on a card.</summary>
public enum ProgressStyle
{
    /// <summary>A thin line at the bottom edge - the default.</summary>
    Default,

    /// <summary>A floating rounded bar inset from the edges.</summary>
    Floating,

    /// <summary>A bold bar across the full bottom edge.</summary>
    Bold,

    /// <summary>A thin line at the top edge.</summary>
    Top,

    /// <summary>Hidden.</summary>
    Hidden,

    /// <summary>A translucent tint that covers the poster from the left as far as the item was watched.</summary>
    Fill,
}

/// <summary>Where the background image comes from.</summary>
public enum BackdropMode
{
    /// <summary>Whatever Jellyfin does on its own (the user's "show backdrops" setting).</summary>
    Default,

    /// <summary>A random backdrop from the library (different on every load) on every page.</summary>
    RandomLibrary,

    /// <summary>A custom image from a URL.</summary>
    Custom,
}

/// <summary>Look of the text fields (and, separately, the buttons) on the login page.</summary>
public enum LoginInputStyle
{
    /// <summary>Jellyfin's own.</summary>
    Default,

    /// <summary>Translucent with the background blurred behind.</summary>
    Glass,

    /// <summary>Transparent with an outline.</summary>
    Outline,

    /// <summary>A light filled field without a border.</summary>
    Filled,

    /// <summary>Neo-brutalism: flat color, thick contrasting border, hard offset shadow.</summary>
    NeoBrutalism,

    /// <summary>Glowmorphism: translucent surface with a soft glow in the accent color around it.</summary>
    Glowmorphism,

    /// <summary>Claymorphism: a puffy, clay-like surface with an inner highlight and shadow.</summary>
    Claymorphism,

    /// <summary>Neumorphism: the surface color itself, raised by a light and a dark shadow.</summary>
    Neumorphism,
}

/// <summary>Look of the login form.</summary>
public enum LoginFormStyle
{
    /// <summary>Fields straight on the background - the default.</summary>
    Plain,

    /// <summary>The form inside a card in the surface color.</summary>
    Card,

    /// <summary>The form inside a translucent glass card.</summary>
    Glass,

    /// <summary>Neo-brutalism: flat color, thick contrasting border, hard offset shadow.</summary>
    NeoBrutalism,

    /// <summary>Glowmorphism: translucent surface with a soft glow in the accent color around it.</summary>
    Glowmorphism,

    /// <summary>Claymorphism: a puffy, clay-like surface with an inner highlight and shadow.</summary>
    Claymorphism,

    /// <summary>Neumorphism: the surface color itself, raised by a light and a dark shadow.</summary>
    Neumorphism,
}

/// <summary>What a toolbar button does when clicked.</summary>
public enum ButtonAction
{
    /// <summary>Open the URL in an overlay (iframe) under the top bar, inside Jellyfin; click again or Esc closes it.</summary>
    Overlay,

    /// <summary>Open the URL in a new browser tab.</summary>
    NewTab,

    /// <summary>Navigate the current tab to the URL.</summary>
    Navigate,
}

/// <summary>Where a toolbar button sits.</summary>
public enum ButtonPlacement
{
    /// <summary>Among the icons on the right, before Search.</summary>
    Icons,

    /// <summary>After the navigation links on the left (with its label).</summary>
    Nav,
}

/// <summary>
/// A custom button in the top bar (e.g. "Requests" opening Seerr). Needs
/// JavaScript in the client, so it only works when the script can be
/// injected - see <see cref="ScriptSettings"/>.
/// </summary>
public class ToolbarButton
{
    public bool Enabled { get; set; } = true;

    /// <summary>Text shown as tooltip and, in the Nav placement, next to the icon.</summary>
    public string Label { get; set; } = "Requests";

    /// <summary>Material Icons name (e.g. playlist_add, movie_filter, rss_feed).</summary>
    public string Icon { get; set; } = "playlist_add";

    public string Url { get; set; } = string.Empty;

    public ButtonAction Action { get; set; } = ButtonAction.Overlay;

    public ButtonPlacement Placement { get; set; } = ButtonPlacement.Icons;

    /// <summary>Do not show the button in the TV layout.</summary>
    public bool HideOnTv { get; set; } = true;

    /// <summary>Overlay only: keep the iframe alive (hidden) when closed, so it remembers its state and login.</summary>
    public bool KeepAlive { get; set; } = true;

    /// <summary>Also show the button on the login / server selection pages, before anyone is signed in.</summary>
    public bool ShowBeforeLogin { get; set; } = false;
}

/// <summary>What the home-page slideshow shows.</summary>
public enum SlideshowSource
{
    /// <summary>Random movies and series with a backdrop.</summary>
    Random,

    /// <summary>The most recently added.</summary>
    Latest,

    /// <summary>Items the user has started and not finished.</summary>
    ContinueWatching,

    /// <summary>The user's favorites.</summary>
    Favorites,

    /// <summary>Items in a genre (<see cref="SlideshowSettings.Filter"/>).</summary>
    Genre,

    /// <summary>Items with a tag (<see cref="SlideshowSettings.Filter"/>).</summary>
    Tag,
}

/// <summary>Item types the slideshow draws from.</summary>
public enum SlideshowTypes
{
    MoviesAndSeries,
    Movies,
    Series,
}

/// <summary>
/// A hero slideshow at the top of the home page: backdrop, logo or title,
/// overview, a button to the item; auto-advances with a cross-fade. Needs
/// the client script (File Transformation / injector).
/// </summary>
public class SlideshowSettings
{
    public bool Enabled { get; set; } = false;

    public SlideshowSource Source { get; set; } = SlideshowSource.Random;

    /// <summary>Genre or tag name for the Genre / Tag sources.</summary>
    public string Filter { get; set; } = string.Empty;

    public SlideshowTypes Types { get; set; } = SlideshowTypes.MoviesAndSeries;

    /// <summary>How many items to load.</summary>
    public int Count { get; set; } = 8;

    /// <summary>Seconds per slide.</summary>
    public int IntervalSeconds { get; set; } = 8;

    /// <summary>Height as a percentage of the window height.</summary>
    public int Height { get; set; } = 55;

    public bool ShowLogo { get; set; } = true;

    public bool ShowOverview { get; set; } = true;

    public bool ShowButton { get; set; } = true;

    public bool HideOnTv { get; set; } = false;

    public bool HideOnMobile { get; set; } = false;
}

/// <summary>
/// Features that need JavaScript in the web client. Jellyfin has no
/// supported way to add scripts; the plugin injects
/// <c>&lt;script src="/Jellycanvas/Script.js"&gt;</c> into index.html through
/// the File Transformation plugin when that is installed. Without it the
/// generated script can still be copied into a JavaScript Injector plugin.
/// </summary>
public class ScriptSettings
{
    /// <summary>Master switch - when off, the injected script is empty.</summary>
    public bool Enabled { get; set; } = false;

    public System.Collections.Generic.List<ToolbarButton> ToolbarButtons { get; set; } = new();

    public SlideshowSettings Slideshow { get; set; } = new();

    public CardBadgeSettings CardBadges { get; set; } = new();
}

/// <summary>Look of the badges the client script draws on cards.</summary>
/// <summary>The color range of the Colorful badge style.</summary>
public enum BadgePalette
{
    /// <summary>Saturated colors across the whole wheel.</summary>
    Vivid,

    /// <summary>Teals, blues and purples.</summary>
    Cool,

    /// <summary>Reds, oranges and yellows.</summary>
    Warm,

    /// <summary>Soft light colors with dark text.</summary>
    Pastel,

    /// <summary>Bright glowing colors with dark text.</summary>
    Neon,
}

public enum CardBadgeStyle
{
    /// <summary>Dark translucent pills with white text.</summary>
    Dark,

    /// <summary>Pills in the accent color.</summary>
    Accent,

    /// <summary>Frosted glass pills.</summary>
    Glass,

    /// <summary>A color per value (4K, 1080p, HEVC, Atmos, each language...), from the chosen <see cref="BadgePalette"/>.</summary>
    Colorful,
}

/// <summary>
/// Small badges on movie and episode cards (resolution, HDR, audio and
/// subtitle languages), read from the items' media streams by the client
/// script. Each corner holds a comma-separated list of badge ids:
/// resolution, hdr, audio, subtitles.
/// </summary>
public class CardBadgeSettings
{
    public bool Enabled { get; set; } = false;

    public string TopLeft { get; set; } = "resolution,hdr";

    public string TopRight { get; set; } = string.Empty;

    public string BottomLeft { get; set; } = "audio,subtitles";

    public string BottomRight { get; set; } = string.Empty;

    public CardBadgeStyle Style { get; set; } = CardBadgeStyle.Dark;

    /// <summary>Which range of colors the Colorful style draws from.</summary>
    public BadgePalette Palette { get; set; } = BadgePalette.Vivid;

    /// <summary>Badge size in percent (100 = default).</summary>
    public int Scale { get; set; } = 100;

    public bool HideOnMobile { get; set; } = false;

    /// <summary>How the audio languages are shown.</summary>
    public LanguageBadgeStyle Languages { get; set; } = LanguageBadgeStyle.Flags;

    /// <summary>How the subtitle languages are shown - codes by default, so they do not repeat the audio flags.</summary>
    public LanguageBadgeStyle SubtitleLanguages { get; set; } = LanguageBadgeStyle.Codes;

    /// <summary>Badges in a corner stacked under each other instead of in a row.</summary>
    public bool Stacked { get; set; } = true;

    /// <summary>Hide in the TV layout.</summary>
    public bool HideOnTv { get; set; } = false;

    /// <summary>How many audio languages a card shows at most (1-4).</summary>
    public int AudioMax { get; set; } = 4;

    /// <summary>Audio languages shown first when the item has them, in this order (codes like "cs, en"); the rest fill up by how widely spoken they are.</summary>
    public string AudioPreferred { get; set; } = string.Empty;

    /// <summary>How many subtitle languages a card shows at most (1-4).</summary>
    public int SubtitleMax { get; set; } = 4;

    /// <summary>Subtitle languages shown first when the item has them, in this order; the rest fill up by how widely spoken they are.</summary>
    public string SubtitlePreferred { get; set; } = string.Empty;
}

/// <summary>How the audio / subtitle languages are shown on a card.</summary>
public enum LanguageBadgeStyle
{
    /// <summary>Short codes: EN CS DE.</summary>
    Codes,

    /// <summary>Small flags (drawn by the script, no downloads; a language with no flag falls back to its code).</summary>
    Flags,

    /// <summary>Flag and code side by side.</summary>
    FlagsAndCodes,
}

// Badge ids (comma-separated per corner): resolution, hdr, codec (H264 /
// HEVC / AV1), sound (Dolby Digital+ 5.1, DTS-HD 7.1...), audio (languages),
// subtitles (languages).

/// <summary>Colors. All in "#rrggbb" notation.</summary>
public class ColorSettings
{
    /// <summary>Accent color: buttons, active items, progress indicators.</summary>
    public string Accent { get; set; } = "#00a4dc";

    /// <summary>Page background.</summary>
    public string Background { get; set; } = "#101010";

    /// <summary>Color of surfaces above the background: the bar, dialogs, lists, cards without an image.</summary>
    public string Surface { get; set; } = "#202020";

    /// <summary>Primary text color.</summary>
    public string Text { get; set; } = "#ffffff";

    /// <summary>Opacity of secondary text (labels, second lines) in percent; 100 = same as primary text.</summary>
    public int SecondaryTextOpacity { get; set; } = 70;

    /// <summary>Color of the frame and hard shadow of neo-brutalist surfaces; empty = black.</summary>
    public string Outline { get; set; } = string.Empty;
}

/// <summary>The top bar (in Jellyfin 12 the MUI <c>header.MuiAppBar-root</c>).</summary>
public class HeaderSettings
{
    public SurfaceStyle Style { get; set; } = SurfaceStyle.Solid;

    /// <summary>Custom bar color; empty = use the surface color from <see cref="ColorSettings.Surface"/>.</summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>Bar opacity in percent (typically 60-80 for glass).</summary>
    public int Opacity { get; set; } = 100;

    /// <summary>Blur of whatever is behind the bar, in pixels. Only visible where the bar is not fully opaque (opacity below 100).</summary>
    public int Blur { get; set; } = 16;

    /// <summary>Corner radius of the bar in pixels.</summary>
    public int Radius { get; set; } = 0;

    /// <summary>Floating bar: detached from the window edges, with its own shadow.</summary>
    public bool Floating { get; set; } = false;

    public bool Shadow { get; set; } = false;

    /// <summary>A thin line under the bar.</summary>
    public bool BottomBorder { get; set; } = false;

    /// <summary>Hide the whole logo (image and server name) on the left of the bar.</summary>
    public bool HideLogo { get; set; } = false;

    public HeaderLayout Layout { get; set; } = HeaderLayout.Full;

    /// <summary>Radius of the islands in the Sections layout (999 = pill).</summary>
    public int SectionRadius { get; set; } = 999;

    /// <summary>Width of the panel in the Sidebar layout, in pixels.</summary>
    public int SidebarWidth { get; set; } = 220;

    /// <summary>Sidebar layout: keep the panel collapsed to icons and slide it out on hover.</summary>
    public bool SidebarCollapsible { get; set; } = false;

    /// <summary>Width of the collapsed panel in pixels (icons only).</summary>
    public int SidebarCollapsedWidth { get; set; } = 64;

    /// <summary>
    /// Which groups sit in which part of the bar, as comma-separated group
    /// ids in order: "nav" (logo + links), "icons" (SyncPlay, Cast, Search,
    /// custom buttons), "user" (the account menu). Every group should appear
    /// exactly once across the three slots; a missing group keeps its stock
    /// position. In the Sidebar layout the slots mean top / middle / bottom.
    /// </summary>
    public string SlotLeft { get; set; } = "nav";

    public string SlotCenter { get; set; } = string.Empty;

    public string SlotRight { get; set; } = "icons,user";

    public NavStyle Nav { get; set; } = NavStyle.Text;

    /// <summary>Bar height in pixels; 0 = default (48).</summary>
    public int Height { get; set; } = 0;

    public LibraryRowStyle LibraryRow { get; set; } = LibraryRowStyle.SameAsBar;

    /// <summary>Row color for a style of its own; empty = the surface color.</summary>
    public string LibraryRowColor { get; set; } = string.Empty;

    /// <summary>Row opacity in percent, for a style of its own.</summary>
    public int LibraryRowOpacity { get; set; } = 100;

    /// <summary>Background blur behind a translucent row, in pixels.</summary>
    public int LibraryRowBlur { get; set; } = 16;

    /// <summary>Height of the library row in pixels; 52 is Jellyfin's own and leaves it alone, 44 is the least the controls fit in.</summary>
    public int LibraryRowHeight { get; set; } = 52;

    /// <summary>Corner radius of the library row; with a radius the row is inset from the edges.</summary>
    public int LibraryRowRadius { get; set; } = 0;

    /// <summary>The row's surface around each group (title, count, play, sort / filter / view, paging) instead of the whole row. Under an islands bar with "same as bar" this is on by itself.</summary>
    public bool LibraryRowIslands { get; set; } = false;

    /// <summary>A thin outline around the row (or its islands).</summary>
    public bool LibraryRowBorder { get; set; } = false;

    /// <summary>Hide the library name (with its dropdown).</summary>
    public bool LibraryRowHideTitle { get; set; } = false;

    /// <summary>Hide the item count chip.</summary>
    public bool LibraryRowHideCount { get; set; } = false;

    /// <summary>Hide the Play all / Shuffle buttons.</summary>
    public bool LibraryRowHidePlay { get; set; } = false;

    /// <summary>Hide the Filter button.</summary>
    public bool LibraryRowHideFilter { get; set; } = false;

    /// <summary>Hide the Sort button.</summary>
    public bool LibraryRowHideSort { get; set; } = false;

    /// <summary>Hide the View settings button.</summary>
    public bool LibraryRowHideView { get; set; } = false;

    /// <summary>Hide the Previous / Next paging buttons.</summary>
    public bool LibraryRowHidePaging { get; set; } = false;

    public LogoImage Logo { get; set; } = LogoImage.Default;

    /// <summary>URL of the custom logo. Uploading through the plugin page stores "../Jellycanvas/Logo" here.</summary>
    public string LogoUrl { get; set; } = string.Empty;

    /// <summary>Logo height in pixels (the default icon is 28).</summary>
    public int LogoHeight { get; set; } = 28;

    /// <summary>Custom logo width in pixels; 0 = from the aspect ratio (estimated 3:1).</summary>
    public int LogoWidth { get; set; } = 0;

    /// <summary>Show the server name next to the logo.</summary>
    public bool ShowServerName { get; set; } = true;

    public bool HideSyncPlay { get; set; } = false;

    public bool HideCast { get; set; } = false;

    public bool HideSearch { get; set; } = false;
}

/// <summary>The side menu (<c>.mainDrawer</c>) and its items (<c>.navMenuOption</c>).</summary>
public class DrawerSettings
{
    public SurfaceStyle Style { get; set; } = SurfaceStyle.Solid;

    public string Color { get; set; } = string.Empty;

    public int Opacity { get; set; } = 100;

    public int Blur { get; set; } = 16;

    /// <summary>Radius of individual menu items in pixels.</summary>
    public int ItemRadius { get; set; } = 0;

    /// <summary>Menu width in pixels; 0 = keep the default.</summary>
    public int Width { get; set; } = 0;

    /// <summary>Radius of the right-hand corners of the open menu in pixels.</summary>
    public int Radius { get; set; } = 0;
}

/// <summary>Item cards - movie, series and album posters...</summary>
public class CardSettings
{
    /// <summary>Card corner radius in pixels.</summary>
    public int Radius { get; set; } = 4;

    public CardHover Hover { get; set; } = CardHover.None;

    public bool Shadow { get; set; } = false;

    /// <summary>A thin light border around the image.</summary>
    public bool Border { get; set; } = false;

    public CardText Text { get; set; } = CardText.Below;

    /// <summary>Hide the buttons that appear over a card on hover (play, menu...).</summary>
    public bool HideOverlayButtons { get; set; } = false;

    /// <summary>Gap between cards as a percentage of the default (50 = tighter, 150 = airier).</summary>
    public int Spacing { get; set; } = 100;

    public PlayedStyle Played { get; set; } = PlayedStyle.Badge;

    /// <summary>Color of the "played" check mark; empty = accent color.</summary>
    public string PlayedColor { get; set; } = string.Empty;

    public ProgressStyle Progress { get; set; } = ProgressStyle.Default;

    /// <summary>Color of the resume-progress indicator; empty = accent color.</summary>
    public string ProgressColor { get; set; } = string.Empty;

    /// <summary>Opacity of the "Fill" progress tint in percent.</summary>
    public int ProgressFillOpacity { get; set; } = 35;

    /// <summary>Hover buttons (play, menu) off on series, seasons and collections only - movies and episodes keep them.</summary>
    public bool HideOverlayButtonsOnFolders { get; set; } = false;
}

/// <summary>The video player's on-screen controls (bottom bar, progress, buttons) and the "Skip intro / credits" button.</summary>
public class PlayerSettings
{
    /// <summary>Look of the bottom control bar; Default = Jellyfin's fade to dark.</summary>
    public OsdStyle Osd { get; set; } = OsdStyle.Default;

    /// <summary>Bar color (empty = surface color; accent for neo-brutalism).</summary>
    public string OsdColor { get; set; } = string.Empty;

    /// <summary>Bar opacity in percent.</summary>
    public int OsdOpacity { get; set; } = 75;

    /// <summary>Background blur behind the bar in pixels (glass).</summary>
    public int OsdBlur { get; set; } = 16;

    /// <summary>The bar detached from the screen edges, with rounded corners.</summary>
    public bool OsdFloating { get; set; } = false;

    /// <summary>Corner radius of the bar in pixels (floating, or a styled bar).</summary>
    public int OsdRadius { get; set; } = 16;

    /// <summary>Color of the played part of the progress slider and its knob (empty = accent).</summary>
    public string ProgressColor { get; set; } = string.Empty;

    /// <summary>Height of the progress slider track in pixels; 0 = Jellyfin's (about 3).</summary>
    public int ProgressHeight { get; set; } = 0;

    /// <summary>Size of the control buttons in percent (100 = default).</summary>
    public int ButtonScale { get; set; } = 100;

    /// <summary>Look of the "Skip intro / credits" button; Default = Jellyfin's dark box.</summary>
    public SkipStyle Skip { get; set; } = SkipStyle.Default;

    /// <summary>Button color (empty = accent for Accent / neo-brutalism, surface color otherwise).</summary>
    public string SkipColor { get; set; } = string.Empty;

    /// <summary>Corner radius of the skip button in pixels.</summary>
    public int SkipRadius { get; set; } = 4;

    /// <summary>Where the skip button sits.</summary>
    public SkipPosition SkipPosition { get; set; } = SkipPosition.BottomRight;

    /// <summary>Distance of the skip button from the bottom (or top) edge in pixels.</summary>
    public int SkipOffset { get; set; } = 128;

    /// <summary>Size of the skip button in percent (100 = default).</summary>
    public int SkipScale { get; set; } = 100;
}

/// <summary>Look of the player's bottom bar.</summary>
public enum OsdStyle
{
    /// <summary>Jellyfin's own fade to dark.</summary>
    Default,
    Solid,
    Glass,
    Gradient,
    Transparent,
    NeoBrutalism,
    Glowmorphism,
    Claymorphism,
    Neumorphism,
}

/// <summary>Look of the skip button.</summary>
public enum SkipStyle
{
    /// <summary>Jellyfin's own dark box.</summary>
    Default,

    /// <summary>Filled with the accent color.</summary>
    Accent,

    /// <summary>The surface color.</summary>
    Surface,

    /// <summary>Frosted glass.</summary>
    Glass,

    /// <summary>Just an outline, transparent inside.</summary>
    Outline,

    /// <summary>Loud fill, hard frame and shadow.</summary>
    NeoBrutalism,
}

/// <summary>Where the skip button sits on the screen.</summary>
public enum SkipPosition
{
    BottomRight,
    BottomCenter,
    BottomLeft,
    TopRight,
}

/// <summary>
/// Seerr (Jellyseerr / Overseerr): rows on the home page built from its
/// requests and discover lists. The client script draws them; the server
/// talks to Seerr, so the API key stays on the server.
/// </summary>
public class SeerrSettings
{
    /// <summary>Seerr's address as the server reaches it (http://seerr:5055 or the public URL); empty = off.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>API key from Seerr → Settings → General.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>The rows, in order.</summary>
    public System.Collections.Generic.List<SeerrRow> Rows { get; set; } = new();
}

/// <summary>What a Seerr row lists.</summary>
public enum SeerrRowKind
{
    /// <summary>Approved requests not in the library yet, next release first.</summary>
    Upcoming,

    /// <summary>The latest requests, whatever their state.</summary>
    Recent,

    /// <summary>Requests waiting for approval.</summary>
    Pending,

    /// <summary>Requests that have arrived in the library, newest first.</summary>
    Available,

    /// <summary>Seerr's trending list.</summary>
    Trending,

    /// <summary>Seerr's popular movies.</summary>
    PopularMovies,

    /// <summary>Seerr's popular series.</summary>
    PopularTv,
}

/// <summary>One home page row fed by Seerr.</summary>
public class SeerrRow
{
    public bool Enabled { get; set; } = true;

    public SeerrRowKind Kind { get; set; } = SeerrRowKind.Upcoming;

    /// <summary>Movies, series, or both.</summary>
    public SeerrMedia Media { get; set; } = SeerrMedia.Both;

    /// <summary>The row's heading; empty = a default in the viewer's language.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Above Jellyfin's own rows, or below them.</summary>
    public RowPosition Position { get; set; } = RowPosition.Top;

    /// <summary>How many posters at most (16, like Jellyfin's own rows).</summary>
    public int Limit { get; set; } = 16;

    /// <summary>The title under the poster.</summary>
    public bool ShowTitle { get; set; } = true;

    /// <summary>The year (or who requested it, for request rows) under the title.</summary>
    public bool ShowSubtitle { get; set; } = true;

    /// <summary>The release date in the poster's corner.</summary>
    public bool ShowDate { get; set; } = true;

    /// <summary>The request state (requested, processing, partly, available) in the poster's corner.</summary>
    public bool ShowState { get; set; } = true;

    /// <summary>"Movie" / "Series" in the poster's corner.</summary>
    public bool ShowType { get; set; } = false;

    /// <summary>Who requested it, on the poster (request rows).</summary>
    public bool ShowRequester { get; set; } = false;
}

/// <summary>What a Seerr row is limited to.</summary>
public enum SeerrMedia
{
    Both,
    Movies,
    Series,
}

/// <summary>Where a custom row goes on the home page.</summary>
public enum RowPosition
{
    Top,
    Bottom,
}

/// <summary>Dialogs, menus and popovers (<c>.dialog</c>, <c>.MuiMenu-paper</c>...).</summary>
public class DialogSettings
{
    public SurfaceStyle Style { get; set; } = SurfaceStyle.Solid;

    public int Opacity { get; set; } = 100;

    public int Blur { get; set; } = 16;

    public int Radius { get; set; } = 4;

    /// <summary>Give the player's "Up next" prompt (next episode countdown) the same look.</summary>
    public bool UpNext { get; set; } = false;
}

/// <summary>Look of the title ribbon on the item page (the strip under the backdrop with the title and the buttons).</summary>
public enum RibbonStyle
{
    /// <summary>The bar's color, slightly translucent - Jellycanvas's default.</summary>
    SameAsBar,

    Solid,
    Glass,
    Gradient,
    Transparent,
    NeoBrutalism,
    Glowmorphism,
    Claymorphism,
    Neumorphism,
}

/// <summary>The movie / series detail page.</summary>
public class DetailSettings
{
    /// <summary>Transparent "ribbon" with the title and buttons under the backdrop.</summary>
    /// <summary>Kept for themes saved before the ribbon got a style of its own; true means "Transparent".</summary>
    public bool TransparentRibbon { get; set; } = false;

    public RibbonStyle Ribbon { get; set; } = RibbonStyle.SameAsBar;

    /// <summary>Ribbon color; empty = the bar color.</summary>
    public string RibbonColor { get; set; } = string.Empty;

    /// <summary>Ribbon opacity in percent.</summary>
    public int RibbonOpacity { get; set; } = 80;

    /// <summary>Background blur behind a translucent ribbon, in pixels.</summary>
    public int RibbonBlur { get; set; } = 12;

    /// <summary>Poster radius; -1 = same as cards.</summary>
    public int PosterRadius { get; set; } = -1;

    public bool PosterShadow { get; set; } = false;

    /// <summary>Hide the title's logo image (ClearLogo) above the description.</summary>
    public bool HideTitleLogo { get; set; } = false;

    public PeopleShape People { get; set; } = PeopleShape.Default;

    /// <summary>Size of the cast cards in percent (100 = default).</summary>
    public int PeopleScale { get; set; } = 100;

    /// <summary>A ring in the accent color around each photo.</summary>
    public bool PeopleRing { get; set; } = false;

    /// <summary>Photos in black and white, in color on hover.</summary>
    public bool PeopleGrayscale { get; set; } = false;

    /// <summary>The Version / Video / Audio / Subtitles selectors above the description.</summary>
    public DetailBlockStyle TrackSelections { get; set; } = DetailBlockStyle.Default;

    /// <summary>The genre links.</summary>
    public DetailBlockStyle Genres { get; set; } = DetailBlockStyle.Default;

    /// <summary>The tag links.</summary>
    public DetailBlockStyle Tags { get; set; } = DetailBlockStyle.Default;

    /// <summary>The external links (IMDb, TMDB...).</summary>
    public DetailBlockStyle ExternalLinks { get; set; } = DetailBlockStyle.Default;

    /// <summary>Font size of the overview text in percent (100 = default).</summary>
    public int OverviewScale { get; set; } = 100;

    /// <summary>Maximum width of the overview text in pixels; 0 = no limit.</summary>
    public int OverviewMaxWidth { get; set; } = 0;

    public bool HideTagline { get; set; } = false;

    /// <summary>Hide the "Similar items" section.</summary>
    public bool HideSimilar { get; set; } = false;

    /// <summary>Hide the cast &amp; crew section.</summary>
    public bool HideCast { get; set; } = false;

    /// <summary>Titles of the sections further down (Cast & crew, Similar items...).</summary>
    public SectionTitleStyle SectionTitles { get; set; } = SectionTitleStyle.Default;

    /// <summary>Color of the chips (genres, tags, links, selectors as chips); empty = automatic (light tint, or accent for accent chips).</summary>
    public string ChipColor { get; set; } = string.Empty;

    /// <summary>Own background for the version / video / audio / subtitles selectors block.</summary>
    public DetailBlockSurface SelectorsBlock { get; set; } = DetailBlockSurface.None;

    /// <summary>Color of that block; empty = the surface color.</summary>
    public string SelectorsBlockColor { get; set; } = string.Empty;

    /// <summary>Own background for the overview (tagline + description) block.</summary>
    public DetailBlockSurface OverviewBlock { get; set; } = DetailBlockSurface.None;

    /// <summary>Color of that block; empty = the surface color.</summary>
    public string OverviewBlockColor { get; set; } = string.Empty;

    /// <summary>Own background for the genres block.</summary>
    public DetailBlockSurface GenresBlock { get; set; } = DetailBlockSurface.None;

    /// <summary>Color of that block; empty = the surface color.</summary>
    public string GenresBlockColor { get; set; } = string.Empty;

    /// <summary>Own background for the tags block.</summary>
    public DetailBlockSurface TagsBlock { get; set; } = DetailBlockSurface.None;

    /// <summary>Color of that block; empty = the surface color.</summary>
    public string TagsBlockColor { get; set; } = string.Empty;

    /// <summary>Own background for the external links block.</summary>
    public DetailBlockSurface LinksBlock { get; set; } = DetailBlockSurface.None;

    /// <summary>Color of that block; empty = the surface color.</summary>
    public string LinksBlockColor { get; set; } = string.Empty;

    /// <summary>Opacity of the block backgrounds in percent.</summary>
    public int BlockOpacity { get; set; } = 75;

    /// <summary>Background blur behind translucent blocks, in pixels.</summary>
    public int BlockBlur { get; set; } = 12;

    /// <summary>Corner radius of the blocks.</summary>
    public int BlockRadius { get; set; } = 12;
}

/// <summary>A background of its own for one block of the item page; None = straight on the page.</summary>
public enum DetailBlockSurface
{
    None,
    Solid,
    Glass,
    Gradient,
    NeoBrutalism,
    Glowmorphism,
    Claymorphism,
    Neumorphism,
}

/// <summary>How one block of the item page (selectors, genres, tags, links) is shown.</summary>
public enum DetailBlockStyle
{
    Default,

    /// <summary>As small rounded chips in a row.</summary>
    Chips,

    /// <summary>As chips in the accent color.</summary>
    AccentChips,

    Hidden,
}

/// <summary>Look of the section titles on the item page.</summary>
public enum SectionTitleStyle
{
    Default,

    /// <summary>Small uppercase text with letter spacing.</summary>
    Uppercase,

    /// <summary>With a short accent-colored line underneath.</summary>
    AccentLine,

    /// <summary>With an accent-colored bar on the left.</summary>
    AccentBar,
}

/// <summary>Shape of the cast &amp; crew photos on the item page.</summary>
public enum PeopleShape
{
    /// <summary>Jellyfin's portrait cards.</summary>
    Default,

    /// <summary>Round photos with the name centered under them.</summary>
    Circle,

    /// <summary>Square photos with the cards' rounding.</summary>
    Square,

    /// <summary>Portrait photos with generous rounding.</summary>
    Rounded,
}

/// <summary>Where the info bar sits.</summary>
public enum InfoBarPosition
{
    /// <summary>Under the top bar (or at the very top of the content when the bar is a sidebar).</summary>
    Top,

    /// <summary>Along the bottom edge of the window.</summary>
    Bottom,
}

/// <summary>
/// A one-line announcement strip ("Maintenance on Sunday", "New: requests
/// via the Requests button"...). Pure CSS - the text is rendered with a
/// pseudo-element - so it cannot hold links or a close button.
/// </summary>
public class InfoBarSettings
{
    public bool Enabled { get; set; } = false;

    public string Text { get; set; } = string.Empty;

    public InfoBarPosition Position { get; set; } = InfoBarPosition.Top;

    /// <summary>Background color; empty = accent color.</summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>Text color; empty = whatever reads well on the background.</summary>
    public string TextColor { get; set; } = string.Empty;

    /// <summary>Height in pixels.</summary>
    public int Height { get; set; } = 36;

    /// <summary>Hide on phones, where every pixel of height counts.</summary>
    public bool HideOnMobile { get; set; } = false;

    /// <summary>Hide in the TV layout (no close button there - a remote cannot reach it).</summary>
    public bool HideOnTv { get; set; } = false;

    /// <summary>Corner radius in pixels; with a radius the strip is inset from the edges so the rounding shows.</summary>
    public int Radius { get; set; } = 0;

    /// <summary>
    /// A close button on the strip. CSS alone cannot do it, so this needs
    /// the client script (File Transformation or a JS injector); a closed
    /// strip stays closed in that browser until the text changes.
    /// </summary>
    public bool Closable { get; set; } = false;

    /// <summary>A closed strip stays closed in that browser until the text changes; off = it is back on the next page load.</summary>
    public bool RememberClose { get; set; } = true;
}

/// <summary>Small things that belong nowhere else.</summary>
public class MiscSettings
{
    /// <summary>Hide scrollbars (content still scrolls with the wheel and touch).</summary>
    public bool HideScrollbars { get; set; } = false;
}

/// <summary>Buttons (<c>.emby-button</c>, <c>.raised</c>, <c>.button-submit</c>, MUI buttons, item-page buttons).</summary>
public class ButtonSettings
{
    public int Radius { get; set; } = 4;

    public ButtonStyle Style { get; set; } = ButtonStyle.Filled;

    /// <summary>Size of the buttons on the item detail page in percent (100 = default).</summary>
    public int DetailScale { get; set; } = 100;

    /// <summary>Show the text label (Play, Trailer, Mark played...) next to each icon on the detail page.</summary>
    public bool DetailLabels { get; set; } = false;

    /// <summary>Lift a button slightly on hover.</summary>
    public bool HoverLift { get; set; } = false;

    /// <summary>Uppercase button text.</summary>
    public bool Uppercase { get; set; } = false;

    /// <summary>Radius of round icon buttons (header icons, card overlay buttons); -1 = keep them round.</summary>
    public int IconRadius { get; set; } = -1;

    public PlayStyle Play { get; set; } = PlayStyle.Inherit;

    /// <summary>Color of the Play button; empty = accent color.</summary>
    public string PlayColor { get; set; } = string.Empty;

    /// <summary>Radius of the Play button; -1 = same as other buttons.</summary>
    public int PlayRadius { get; set; } = -1;

    /// <summary>Show the "Play" label next to the icon (even when the other labels are off).</summary>
    public bool PlayLabel { get; set; } = false;
}

/// <summary>Font.</summary>
public class TypographySettings
{
    public FontFamily Family { get; set; } = FontFamily.Default;

    /// <summary>Name of the custom font when <see cref="Family"/> = Custom (a Google Fonts family, a font installed on the device, or one loaded in ExtraCss).</summary>
    public string CustomFamily { get; set; } = string.Empty;

    /// <summary>Load the font from Google Fonts (Inter, Roboto, Poppins, Nunito and custom names). Turn off when browsers must not reach the internet.</summary>
    public bool LoadFromGoogle { get; set; } = true;

    /// <summary>Font size in percent (100 = default).</summary>
    public int Scale { get; set; } = 100;
}

/// <summary>The background image (backdrop) behind the content.</summary>
public class BackdropSettings
{
    public BackdropMode Mode { get; set; } = BackdropMode.Default;

    /// <summary>URL of the custom image (for <see cref="BackdropMode.Custom"/>).</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Image blur in pixels.</summary>
    public int Blur { get; set; } = 0;

    /// <summary>Image dimming in percent (0 = as is, 100 = fully hidden).</summary>
    public int Dim { get; set; } = 14;

    /// <summary>Slow panning of the image from side to side.</summary>
    public bool Animate { get; set; } = false;

    /// <summary>
    /// For <see cref="BackdropMode.RandomLibrary"/>: switch to another random
    /// backdrop every N seconds with a cross-fade; 0 = only on page load.
    /// </summary>
    public int RotateSeconds { get; set; } = 0;

    /// <summary>On an item's page show that item's own backdrop instead (client script; random and custom modes).</summary>
    public bool ItemDetail { get; set; } = false;
}

/// <summary>The login page.</summary>
public class LoginSettings
{
    /// <summary>Gradient background. A background image URL, when set, takes precedence.</summary>
    public bool GradientBackground { get; set; } = false;

    /// <summary>First gradient color; empty = the page background color.</summary>
    public string GradientFrom { get; set; } = string.Empty;

    /// <summary>Second gradient color; empty = a darkened accent color.</summary>
    public string GradientTo { get; set; } = string.Empty;

    /// <summary>Gradient direction in degrees (0 = bottom to top, 90 = left to right).</summary>
    public int GradientAngle { get; set; } = 160;

    /// <summary>Gradient opacity in percent; below 100 the backdrop behind it (random or splash screen) shows through.</summary>
    public int GradientOpacity { get; set; } = 100;

    /// <summary>Make the bar (just the logo on the login page) transparent so the page background - color, gradient, image or backdrop - runs under it.</summary>
    public bool TransparentBar { get; set; } = true;

    /// <summary>Replacement for the "Please sign in" heading; empty = Jellyfin's own text.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Maximum width of the form in pixels; 0 = Jellyfin's default.</summary>
    public int FormWidth { get; set; } = 0;

    public LoginInputStyle Inputs { get; set; } = LoginInputStyle.Default;

    /// <summary>Look of the Sign in / Quick Connect / Forgot password buttons.</summary>
    public LoginInputStyle Buttons { get; set; } = LoginInputStyle.Default;

    /// <summary>Radius of the text fields and buttons; -1 = same as the form radius.</summary>
    public int InputRadius { get; set; } = -1;

    /// <summary>Height of the text fields in percent (100 = default).</summary>
    public int InputScale { get; set; } = 100;

    /// <summary>Hide the "Please sign in" heading.</summary>
    public bool HideTitle { get; set; } = false;

    public bool HideQuickConnect { get; set; } = false;

    public bool HideForgotPassword { get; set; } = false;

    /// <summary>Radius of the user tiles and the form.</summary>
    public int Radius { get; set; } = 4;

    /// <summary>URL of the login page background image; empty = none.</summary>
    public string BackgroundUrl { get; set; } = string.Empty;

    /// <summary>Blur of the background image in pixels.</summary>
    public int BackgroundBlur { get; set; } = 0;

    public LoginFormStyle Form { get; set; } = LoginFormStyle.Plain;
}

/// <summary>Tweaks for the TV layout (<c>html.layout-tv</c>) - remote-control navigation.</summary>
/// <summary>How a focused tab in the TV top bar shows.</summary>
public enum TabFocusStyle
{
    /// <summary>Filled with the focus color, no movement.</summary>
    Highlight,

    /// <summary>A ring in the focus color around the tab.</summary>
    Ring,

    /// <summary>Jellyfin's own: the tab grows (1.3x).</summary>
    Scale,

    /// <summary>A gentle grow (1.1x) with a glow in the focus color.</summary>
    Glow,
}

public class TvSettings
{
    /// <summary>Color of the ring around the focused element; empty = accent color.</summary>
    public string FocusColor { get; set; } = string.Empty;

    /// <summary>Zoom of the focused card in percent (100 = no zoom).</summary>
    public int FocusScale { get; set; } = 100;

    /// <summary>Focus ring width in pixels.</summary>
    public int FocusWidth { get; set; } = 3;

    /// <summary>Height of the TV top bar's first row (logo, icons) in pixels; 0 = Jellyfin's own, which is rather low.</summary>
    public int BarHeight { get; set; } = 0;

    /// <summary>Size of the TV bar's icons and tabs in percent (100 = default).</summary>
    public int BarScale { get; set; } = 100;

    /// <summary>What a focused tab in the TV bar (Home, Favorites, libraries) does.</summary>
    public TabFocusStyle TabFocus { get; set; } = TabFocusStyle.Highlight;
}

/// <summary>Tweaks for the mobile layout (<c>html.layout-mobile</c>).</summary>
public class MobileSettings
{
    /// <summary>Card radius on mobile; -1 = same as on the web.</summary>
    public int CardRadius { get; set; } = -1;

    /// <summary>Font size on mobile in percent; 0 = same as on the web.</summary>
    public int FontScale { get; set; } = 0;
}

/// <summary>
/// The whole plugin configuration = one theme. Jellyfin stores it as the XML
/// file <c>plugins/configurations/Jellyfin.Plugin.Jellycanvas.xml</c> and the
/// settings page receives it as JSON through <c>ApiClient.getPluginConfiguration</c>.
///
/// That is why there are only simple properties here (numbers, strings,
/// enums, nested classes) - both XML and JSON handle those without help.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Whether the generated CSS should be written to Branding. Turning it off
    /// removes the block, but the settings stay here so the theme can be
    /// re-enabled unchanged.
    /// </summary>
    public bool Enabled { get; set; } = false;

    public ApplyTo ApplyTo { get; set; } = ApplyTo.All;

    public ColorSettings Colors { get; set; } = new();

    public HeaderSettings Header { get; set; } = new();

    public DrawerSettings Drawer { get; set; } = new();

    public CardSettings Cards { get; set; } = new();

    public ButtonSettings Buttons { get; set; } = new();

    public TypographySettings Typography { get; set; } = new();

    public BackdropSettings Backdrop { get; set; } = new();

    public LoginSettings Login { get; set; } = new();

    public DialogSettings Dialogs { get; set; } = new();

    public PlayerSettings Player { get; set; } = new();

    public DetailSettings Detail { get; set; } = new();

    public MiscSettings Misc { get; set; } = new();

    public InfoBarSettings InfoBar { get; set; } = new();

    public TvSettings Tv { get; set; } = new();

    public MobileSettings Mobile { get; set; } = new();

    public ScriptSettings Scripts { get; set; } = new();

    public SeerrSettings Seerr { get; set; } = new();

    /// <summary>Custom CSS appended after the generated block - for anything the controls do not cover.</summary>
    public string ExtraCss { get; set; } = string.Empty;
}
