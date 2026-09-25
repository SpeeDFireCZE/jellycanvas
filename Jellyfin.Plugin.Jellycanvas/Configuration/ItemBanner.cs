namespace Jellyfin.Plugin.Jellycanvas.Configuration;

/// <summary>
/// The item page's banner as it applies. The background once had a switch
/// of its own for "the item's own backdrop on its page" - the same thing as
/// this banner with the backdrop chosen, so it went; a theme saved with it
/// still gets that banner, dimmed like the background it was part of (and,
/// as before, only where the background is not Jellyfin's own).
/// </summary>
/// <param name="On">Whether the item page shows the item's picture.</param>
/// <param name="Image">Which picture.</param>
/// <param name="Dim">The dimming over it, in percent.</param>
public readonly record struct ItemBanner(bool On, BannerImage Image, int Dim)
{
    /// <summary>The banner these settings give.</summary>
    public static ItemBanner Of(PluginConfiguration c)
    {
        var d = c.Detail;
        var bd = c.Backdrop;
        if (!d.Banner && bd.ItemDetail && bd.Mode != BackdropMode.Default)
        {
            return new ItemBanner(true, BannerImage.Backdrop, bd.Dim);
        }

        return new ItemBanner(d.Banner, d.BannerImage, d.BannerDim);
    }
}
