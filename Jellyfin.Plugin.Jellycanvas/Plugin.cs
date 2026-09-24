using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.Jellycanvas.Configuration;
using MediaBrowser.Common.Configuration;
using Jellyfin.Plugin.Jellycanvas.Theme;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Jellycanvas;

/// <summary>
/// The plugin's entry point. At startup Jellyfin finds the class in our DLL
/// that derives from <see cref="BasePlugin{T}"/>, instantiates it, and from
/// then on knows the plugin exists.
///
/// By itself it does almost nothing - it holds the configuration (saved by
/// the server as XML in plugins/configurations/) and tells the server which
/// page to show in the Dashboard. The real work is in <c>Theme/</c> (CSS
/// generation) and <c>Api/</c> (what the settings page's buttons call).
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Jellyfin creates the plugin through dependency injection, so extra
    /// services can simply be asked for here; the base class only needs the
    /// first two.
    /// </summary>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, IServerConfigurationManager configurationManager, ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        ConfigurationManager = configurationManager;

        // The generated CSS sits in Branding and is written when the settings
        // are applied. A new version of the plugin generates different CSS,
        // so without this the server would keep serving what the previous
        // version made until someone opened the designer and pressed Apply -
        // an update would look like it had done nothing.
        try
        {
            if (BrandingWriter.Refresh(configurationManager, Configuration))
            {
                logger.LogInformation("Jellycanvas: the CSS in Branding was rebuilt for this version");
            }
        }
        catch (Exception ex)
        {
            // A theme that cannot be rewritten is no reason to fail startup;
            // the old block stays and Apply still works.
            logger.LogWarning(ex, "Jellycanvas: could not rebuild the CSS in Branding at startup");
        }
    }

    /// <summary>Server configuration - used for the network base URL when the client script tag is built.</summary>
    public IServerConfigurationManager ConfigurationManager { get; }

    /// <summary>The name shown in the plugin list.</summary>
    public override string Name => "Jellycanvas";

    /// <summary>
    /// The Dashboard lists the version the DLL carries, and a .NET assembly
    /// version always has four parts: a release 1.2.4 would read 1.2.4.0.
    /// Releases are three parts; only a test build has a fourth number
    /// (built with -p:Version=1.2.4.8), and only then is it shown.
    /// </summary>
    public override PluginInfo GetPluginInfo()
    {
        var info = base.GetPluginInfo();
        info.Version = DisplayVersion(Version);
        return info;
    }

    /// <summary>Three parts for a release, four for a test build.</summary>
    public static Version DisplayVersion(Version version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return version.Revision > 0 ? version : new Version(version.Major, version.Minor, Math.Max(0, version.Build));
    }

    /// <summary>
    /// The plugin's permanent identifier. Must match build.yaml and
    /// configPage.js - the Dashboard loads and saves the configuration by it.
    /// Never change it, or Jellyfin will see a "different" plugin.
    /// </summary>
    public override Guid Id => Guid.Parse("433e86f7-318f-4bd5-98e9-98fd3eea7d42");

    /// <summary>
    /// The single plugin instance for the server's lifetime. Other code (the
    /// API controller, for example) reaches the current configuration through it.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>
    /// Pages the plugin adds to the web UI. The first one (named after the
    /// plugin) appears in the Dashboard under "My plugins"; the second is just
    /// the JavaScript that page loads for itself.
    /// </summary>
    public IEnumerable<PluginPageInfo> GetPages()
    {
        var ns = GetType().Namespace;
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", ns),
                EnableInMainMenu = true,
                MenuIcon = "palette",
            },
            new PluginPageInfo
            {
                Name = "JellycanvasJs",
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.js", ns),
            },
        ];
    }
}
