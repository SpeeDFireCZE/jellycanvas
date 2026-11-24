using Jellyfin.Plugin.Jellycanvas.Configuration;

namespace Jellyfin.Plugin.Jellycanvas.Theme;

/// <summary>
/// The looks of the card badges. Each look is CSS for a badge drawn in
/// variables the client script sets from the color chosen - so every look
/// serves the accent, neutral dark and a color per value alike:
/// --jcb (the color, "r, g, b"), --jcb-a (the opacity of its fill),
/// --jcb-text (text on that fill), --jcb-ink (lines and text on a dark or
/// clear badge: white for neutral dark, a lighter shade of a value's color).
/// </summary>
public static class BadgeLooks
{
    private const string Fill = "background: rgba(var(--jcb), var(--jcb-a)); color: var(--jcb-text);";

    /// <summary>The CSS declarations of a look (for .jellycanvas-badge).</summary>
    public static string Css(BadgeLook look) => look switch
    {
        // (the tint over a dark base: the colored text stays legible on a bright poster)
        BadgeLook.Soft => "background: linear-gradient(rgba(var(--jcb-ink), 0.25), rgba(var(--jcb-ink), 0.25)) rgba(0, 0, 0, 0.45); color: rgb(var(--jcb-ink)); text-shadow: 0 1px 2px rgba(0, 0, 0, 0.6);",
        BadgeLook.Outline => "background: rgba(0, 0, 0, 0.45); color: rgb(var(--jcb-ink)); border: 1.5px solid rgb(var(--jcb-ink));",
        BadgeLook.Glass => "background: rgba(var(--jcb-ink), 0.18); color: #fff; backdrop-filter: blur(6px); -webkit-backdrop-filter: blur(6px); border: 1px solid rgba(var(--jcb-ink), 0.25);",
        BadgeLook.Glow => Fill + " box-shadow: 0 0 0.7em rgba(var(--jcb-ink), 0.75);",
        BadgeLook.Gradient => "background: linear-gradient(135deg, rgba(var(--jcb), var(--jcb-a)) 0%, rgba(var(--jcb), 0.45) 100%); color: var(--jcb-text);",
        BadgeLook.NeoBrutalism => Fill + " border: 2px solid #000; box-shadow: 2px 2px 0 #000; border-radius: 0.2em;",
        BadgeLook.Glowmorphism => "background: rgba(var(--jcb-ink), 0.16); color: #fff; border: 1px solid rgba(var(--jcb-ink), 0.55); box-shadow: 0 0 0.8em rgba(var(--jcb-ink), 0.45); backdrop-filter: blur(6px); -webkit-backdrop-filter: blur(6px); text-shadow: 0 1px 2px rgba(0, 0, 0, 0.6);",
        BadgeLook.Claymorphism => Fill + " border-radius: 0.7em; box-shadow: inset 0.12em 0.12em 0.25em rgba(255, 255, 255, 0.4), inset -0.12em -0.12em 0.25em rgba(0, 0, 0, 0.3), 0.1em 0.18em 0.35em rgba(0, 0, 0, 0.45);",
        BadgeLook.Neumorphism => Fill + " box-shadow: 0.15em 0.15em 0.3em rgba(0, 0, 0, 0.55), -0.1em -0.1em 0.25em rgba(255, 255, 255, 0.2);",
        BadgeLook.Neon => "background: rgba(0, 0, 0, 0.72); color: rgb(var(--jcb-ink)); border: 1px solid rgb(var(--jcb-ink)); box-shadow: 0 0 0.5em rgba(var(--jcb-ink), 0.9), inset 0 0 0.35em rgba(var(--jcb-ink), 0.45); text-shadow: 0 0 0.35em rgba(var(--jcb-ink), 0.9);",
        BadgeLook.Flat => Fill + " border-radius: 0; box-shadow: none;",
        _ => Fill,
    };

    /// <summary>The look and the color the badges are drawn in: the ones chosen, or what the former style had.</summary>
    public static (BadgeLook Look, BadgeColor Color) Of(CardBadgeSettings cb)
    {
        var look = cb.Look != BadgeLook.Auto ? cb.Look : cb.Style == CardBadgeStyle.Glass ? BadgeLook.Glass : BadgeLook.Filled;
        var color = cb.Color != BadgeColor.Auto ? cb.Color : cb.Style switch
        {
            CardBadgeStyle.Accent => BadgeColor.Accent,
            CardBadgeStyle.Colorful => BadgeColor.PerValue,
            _ => BadgeColor.Neutral,
        };
        return (look, color);
    }
}
