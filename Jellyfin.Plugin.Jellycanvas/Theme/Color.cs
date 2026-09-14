using System;
using System.Globalization;

namespace Jellyfin.Plugin.Jellycanvas.Theme;

/// <summary>
/// A color as three 0-255 numbers. A small helper struct, because one color
/// picked by the user has to yield several others: a darker shade for hover,
/// a translucent variant for glass, a text color that stays readable on it,
/// and the "channels" (r g b without commas) that MUI uses in Jellyfin.
/// </summary>
public readonly record struct Color(int R, int G, int B)
{
    /// <summary>
    /// Parses "#rrggbb" or "#rgb". When the input makes no sense (empty, a
    /// typo) it returns <paramref name="fallback"/> - better than crashing over
    /// one bad field and leaving the user with no CSS at all.
    /// </summary>
    public static Color Parse(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return fallback;
        }

        var s = hex.Trim().TrimStart('#');
        if (s.Length == 3)
        {
            s = string.Concat(s[0], s[0], s[1], s[1], s[2], s[2]);
        }

        if (s.Length != 6 || !int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
        {
            return fallback;
        }

        return new Color((v >> 16) & 0xff, (v >> 8) & 0xff, v & 0xff);
    }

    public static Color Parse(string? hex, string fallbackHex) => Parse(hex, Parse(fallbackHex, new Color(0, 0, 0)));

    /// <summary>"#rrggbb".</summary>
    public string Hex => string.Create(CultureInfo.InvariantCulture, $"#{R:x2}{G:x2}{B:x2}");

    /// <summary>"r g b" - the form MUI expects in the *Channel variables so it can write rgba(var(--x) / 0.2).</summary>
    public string Channel => string.Create(CultureInfo.InvariantCulture, $"{R} {G} {B}");

    /// <summary>"rgba(r, g, b, a)" with opacity 0-1.</summary>
    public string Rgba(double alpha)
    {
        var a = Math.Clamp(alpha, 0, 1);
        return string.Create(CultureInfo.InvariantCulture, $"rgba({R}, {G}, {B}, {a:0.###})");
    }

    /// <summary>The same with opacity in percent 0-100, as the sliders deliver it.</summary>
    public string RgbaPercent(int percent) => Rgba(percent / 100.0);

    /// <summary>
    /// Mixes this color with another. <paramref name="amount"/> 0 = unchanged,
    /// 1 = entirely the other one. Used for "a bit darker/lighter".
    /// </summary>
    public Color Mix(Color other, double amount)
    {
        var t = Math.Clamp(amount, 0, 1);
        return new Color(
            (int)Math.Round(R + ((other.R - R) * t)),
            (int)Math.Round(G + ((other.G - G) * t)),
            (int)Math.Round(B + ((other.B - B) * t)));
    }

    public Color Darken(double amount) => Mix(new Color(0, 0, 0), amount);

    public Color Lighten(double amount) => Mix(new Color(255, 255, 255), amount);

    /// <summary>
    /// Perceived brightness 0-1 (WCAG relative luminance). Decides whether
    /// black or white text belongs on the color.
    /// </summary>
    public double Luminance
    {
        get
        {
            static double Lin(int c)
            {
                var v = c / 255.0;
                return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
            }

            return (0.2126 * Lin(R)) + (0.7152 * Lin(G)) + (0.0722 * Lin(B));
        }
    }

    /// <summary>Is the color light? (A 0.4 threshold suits a dark UI better than an exact half.)</summary>
    public bool IsLight => Luminance > 0.4;

    /// <summary>Text that stays readable on this color - the same rule as MUI's contrastText.</summary>
    public string ContrastText => IsLight ? "rgba(0, 0, 0, 0.87)" : "#ffffff";
}
