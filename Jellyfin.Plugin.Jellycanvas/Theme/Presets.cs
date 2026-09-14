using System.Collections.Generic;
using Jellyfin.Plugin.Jellycanvas.Configuration;

namespace Jellyfin.Plugin.Jellycanvas.Theme;

/// <summary>One preset: key, label and a ready-made configuration.</summary>
public sealed record Preset(string Id, string Name, string Description, PluginConfiguration Settings);

/// <summary>
/// Starting points for those who do not want to begin from scratch. Each
/// preset is just a differently filled <see cref="PluginConfiguration"/> -
/// the page loads it into the controls and from then on it is the user's own.
/// </summary>
public static class Presets
{
    public static IReadOnlyList<Preset> All { get; } =
    [
        new(
            "jellyfin",
            "Jellyfin",
            "The stock look - a clean starting point.",
            new PluginConfiguration()),

        new(
            "glass",
            "Frosted glass",
            "Translucent blurred bars, rounded cards with a soft lift on hover.",
            new PluginConfiguration
            {
                Colors = new ColorSettings { Accent = "#7dd3fc", Background = "#0b1220", Surface = "#16213a" },
                Header = new HeaderSettings { Style = SurfaceStyle.Glass, Opacity = 55, Blur = 18, Radius = 14, Floating = true, BottomBorder = true, Nav = NavStyle.Pill },
                Drawer = new DrawerSettings { Style = SurfaceStyle.Glass, Opacity = 70, Blur = 18, ItemRadius = 10, Radius = 20 },
                Cards = new CardSettings { Radius = 12, Hover = CardHover.Lift, Shadow = true, Played = PlayedStyle.CornerBadge, Progress = ProgressStyle.Floating },
                Buttons = new ButtonSettings { Radius = 10, Play = PlayStyle.Accent, PlayLabel = true, HoverLift = true },
                Dialogs = new DialogSettings { Style = SurfaceStyle.Glass, Opacity = 75, Blur = 20, Radius = 14 },
                Backdrop = new BackdropSettings { Mode = BackdropMode.RandomLibrary, Blur = 24, Dim = 60 },
                Login = new LoginSettings { Radius = 14, Form = LoginFormStyle.Glass },
                Detail = new DetailSettings { PosterShadow = true },
            }),

        new(
            "soft",
            "Soft & rounded",
            "Warm dark grey, generous rounding everywhere, pill buttons.",
            new PluginConfiguration
            {
                Colors = new ColorSettings { Accent = "#f59e0b", Background = "#141210", Surface = "#221f1b", Text = "#f5efe6" },
                Header = new HeaderSettings { Style = SurfaceStyle.Solid, Layout = HeaderLayout.Sections, SectionRadius = 999, Nav = NavStyle.Pill, ShowServerName = false },
                Drawer = new DrawerSettings { ItemRadius = 999, Radius = 24 },
                Cards = new CardSettings { Radius = 16, Hover = CardHover.Zoom, Text = CardText.Below, Progress = ProgressStyle.Floating },
                Buttons = new ButtonSettings { Radius = 999, Style = ButtonStyle.Soft, Play = PlayStyle.Accent, PlayLabel = true, DetailLabels = true, IconRadius = 12 },
                Dialogs = new DialogSettings { Radius = 18 },
                Typography = new TypographySettings { Family = FontFamily.Nunito },
                Login = new LoginSettings { Radius = 18 },
            }),

        new(
            "neon",
            "Neon night",
            "Near-black background, magenta accent, glowing cards.",
            new PluginConfiguration
            {
                Colors = new ColorSettings { Accent = "#e879f9", Background = "#07070b", Surface = "#15131d" },
                Header = new HeaderSettings { Style = SurfaceStyle.Gradient, Opacity = 90, Nav = NavStyle.Underline },
                Drawer = new DrawerSettings { Style = SurfaceStyle.Gradient, Opacity = 95, ItemRadius = 8 },
                Cards = new CardSettings { Radius = 6, Hover = CardHover.Glow, Border = true, Text = CardText.Overlay, Played = PlayedStyle.Dimmed, Progress = ProgressStyle.Bold },
                Buttons = new ButtonSettings { Radius = 6, Style = ButtonStyle.Outline, Play = PlayStyle.Accent, Uppercase = true },
                Backdrop = new BackdropSettings { Dim = 8 },
                Tv = new TvSettings { FocusScale = 106 },
            }),

        new(
            "minimal",
            "Minimal",
            "Transparent bars, no overlays, system font - the content and nothing else.",
            new PluginConfiguration
            {
                Colors = new ColorSettings { Accent = "#e5e7eb", Background = "#000000", Surface = "#111111" },
                Header = new HeaderSettings { Style = SurfaceStyle.Transparent, ShowServerName = false, HideSyncPlay = true, HideCast = true },
                Drawer = new DrawerSettings { Style = SurfaceStyle.Solid },
                Cards = new CardSettings { Radius = 2, HideOverlayButtons = true, Played = PlayedStyle.Grayscale, Progress = ProgressStyle.Top },
                Buttons = new ButtonSettings { Radius = 2, Style = ButtonStyle.Outline },
                Typography = new TypographySettings { Family = FontFamily.System },
                Backdrop = new BackdropSettings { Dim = 60 },
                Detail = new DetailSettings { Ribbon = RibbonStyle.Transparent },
                Misc = new MiscSettings { HideScrollbars = true },
            }),
    ];
}
