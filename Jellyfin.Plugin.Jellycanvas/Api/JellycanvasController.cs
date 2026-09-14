using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.Jellycanvas.Configuration;
using Jellyfin.Plugin.Jellycanvas.Scripts;
using Jellyfin.Plugin.Jellycanvas.Theme;
using MediaBrowser.Common.Api;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Jellycanvas.Api;

/// <summary>
/// The plugin's web API - what the JavaScript on the settings page calls.
/// At startup Jellyfin finds every ControllerBase-derived class in the loaded
/// plugins and wires it into its API, so <c>POST /Jellycanvas/Preview</c>
/// works right next to <c>/Items</c> and friends (and shows up in
/// <c>/api-docs/swagger</c>).
///
/// <c>RequiresElevation</c> = administrators only; changing the look for
/// everyone is not something for a regular account. The exceptions
/// (<c>AllowAnonymous</c>) are the addresses the browser fetches through CSS
/// or a script tag - no auth header can travel with those, and the logo and
/// script must work on the login page too.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route("Jellycanvas")]
[Produces(MediaTypeNames.Application.Json)]
public class JellycanvasController : ControllerBase
{
    private const long MaxLogoBytes = 2 * 1024 * 1024;

    private static readonly Dictionary<string, string> LogoTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".svg"] = "image/svg+xml",
        [".webp"] = "image/webp",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
    };

    /// <summary>Two uploads at once would fight over the same file; let them through one at a time.</summary>
    private static readonly SemaphoreSlim LogoLock = new(1, 1);

    private readonly IServerConfigurationManager _config;
    private readonly ILibraryManager _library;
    private readonly IApplicationPaths _paths;
    private readonly IPluginManager _plugins;
    private readonly ILogger<JellycanvasController> _logger;

    /// <summary>
    /// Dependencies are supplied by Jellyfin (dependency injection) - asking
    /// for them in the constructor is enough. IServerConfigurationManager is
    /// the key to Branding, ILibraryManager to library items (random
    /// backdrop), IApplicationPaths says where the plugin may store files,
    /// IPluginManager lists the other installed plugins.
    /// </summary>
    public JellycanvasController(
        IServerConfigurationManager config,
        ILibraryManager library,
        IApplicationPaths paths,
        IPluginManager plugins,
        ILogger<JellycanvasController> logger)
    {
        _config = config;
        _library = library;
        _paths = paths;
        _plugins = plugins;
        _logger = logger;
    }

    /// <summary>Folder for the plugin's files (the uploaded logo). Next to the settings XML.</summary>
    private string DataDir => Path.Combine(_paths.PluginConfigurationsPath, "Jellycanvas");

    /// <summary>State: is the theme enabled, is it really in Branding, which helper plugins are around?</summary>
    [HttpGet("Status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<StatusDto> GetStatus()
    {
        var cfg = Plugin.Instance!.Configuration;
        var present = BrandingWriter.IsPresent(_config);
        var foreign = BrandingWriter.StripBlock(BrandingWriter.Read(_config));
        var injector = _plugins.Plugins.Any(p =>
            p.Name.Contains("injector", StringComparison.OrdinalIgnoreCase)
            || (p.Instance?.GetType().Assembly.FullName?.Contains("Injector", StringComparison.OrdinalIgnoreCase) ?? false));
        return new StatusDto(cfg.Enabled, present, foreign.Length, FindLogo() is not null, FileTransformation.IsAvailable(), injector);
    }

    /// <summary>Presets for a quick start.</summary>
    [HttpGet("Presets")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<Preset>> GetPresets() => Ok(Presets.All);

    /// <summary>
    /// Returns the CSS for the given settings without saving anything. The
    /// page calls this on every slider move and feeds the result to the preview.
    /// </summary>
    [HttpPost("Preview")]
    [Produces("text/css")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ContentResult Preview([FromBody] PluginConfiguration settings)
        => Content(CssBuilder.Build(settings), "text/css");

    /// <summary>The client script for the given settings, unsaved - for the preview and for copying into an injector plugin.</summary>
    [HttpPost("ScriptPreview")]
    [Produces("text/javascript")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ContentResult ScriptPreview([FromBody] PluginConfiguration settings)
        => Content(ScriptBuilder.Build(settings), "text/javascript");

    /// <summary>
    /// Saves the settings as the plugin configuration, generates the CSS and
    /// writes it to Branding. From this moment everyone who loads the web sees it.
    /// </summary>
    [HttpPost("Apply")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<ApplyResultDto> Apply([FromBody] PluginConfiguration settings)
    {
        settings.Enabled = true;
        var css = CssBuilder.Build(settings);
        BrandingWriter.Write(_config, css);
        Plugin.Instance!.UpdateConfiguration(settings);
        _logger.LogInformation("Jellycanvas: theme applied ({Length} bytes of CSS written to branding)", css.Length);
        return new ApplyResultDto(css, true);
    }

    /// <summary>
    /// Saves the settings but leaves Branding alone. For an admin who wants
    /// to work on a theme now and roll it out later.
    /// </summary>
    [HttpPost("Save")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult Save([FromBody] PluginConfiguration settings)
    {
        settings.Enabled = Plugin.Instance!.Configuration.Enabled;
        Plugin.Instance.UpdateConfiguration(settings);
        return NoContent();
    }

    /// <summary>
    /// Cuts our block out of Branding. The settings stay saved, so "Apply"
    /// brings it back unchanged. Foreign CSS in Branding is not touched.
    /// </summary>
    [HttpPost("Disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult Disable()
    {
        BrandingWriter.Remove(_config);
        var cfg = Plugin.Instance!.Configuration;
        cfg.Enabled = false;
        Plugin.Instance.UpdateConfiguration(cfg);
        _logger.LogInformation("Jellycanvas: theme removed from branding");
        return NoContent();
    }

    // ------------------------------------------------------------------
    // Client script (File Transformation / injector).
    // ------------------------------------------------------------------

    /// <summary>
    /// The client script for the *saved* configuration. The script tag that
    /// File Transformation injects into index.html points here. Anonymous:
    /// the tag loads before login. Never cached for long - the admin expects
    /// a change to show up on the next reload.
    /// </summary>
    [HttpGet("Script.js")]
    [AllowAnonymous]
    [Produces("text/javascript")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ContentResult GetScript()
    {
        Response.Headers.CacheControl = "no-cache";
        return Content(ScriptBuilder.Build(Plugin.Instance!.Configuration), "text/javascript");
    }

    // ------------------------------------------------------------------
    // Logo: upload from the settings page, served for the CSS.
    // ------------------------------------------------------------------

    /// <summary>
    /// Accepts a logo image (multipart, field "file"), stores it in the plugin
    /// folder and returns the URL the page writes into Header.LogoUrl.
    /// </summary>
    [HttpPost("Logo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LogoResultDto>> UploadLogo(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("No file.");
        }

        if (file.Length > MaxLogoBytes)
        {
            return BadRequest("Logo must be 2 MB or smaller.");
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !LogoTypes.ContainsKey(ext))
        {
            return BadRequest("Use PNG, SVG, WebP, JPG or GIF.");
        }

        await LogoLock.WaitAsync().ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(DataDir);

            // Write the whole file under a temporary name first, then move:
            // if the upload dies half-way the old logo stays intact.
            var target = Path.Combine(DataDir, "logo" + ext.ToLowerInvariant());
            var temp = target + ".upload";
            await using (var stream = System.IO.File.Create(temp))
            {
                await file.CopyToAsync(stream).ConfigureAwait(false);
            }

            foreach (var old in Directory.EnumerateFiles(DataDir, "logo.*"))
            {
                if (!string.Equals(old, temp, StringComparison.OrdinalIgnoreCase))
                {
                    System.IO.File.Delete(old);
                }
            }

            System.IO.File.Move(temp, target, overwrite: true);
        }
        finally
        {
            LogoLock.Release();
        }

        _logger.LogInformation("Jellycanvas: logo uploaded ({Length} bytes)", file.Length);

        // "?v=" makes the browser fetch the new file instead of its cached copy.
        return new LogoResultDto(CssBuilder.LogoUrl + "?v=" + DateTime.UtcNow.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>Deletes the uploaded logo.</summary>
    [HttpDelete("Logo")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult DeleteLogo()
    {
        var path = FindLogo();
        if (path is not null)
        {
            System.IO.File.Delete(path);
        }

        return NoContent();
    }

    /// <summary>Serves the uploaded logo. Anonymous - the CSS pulls it on the login page too.</summary>
    [HttpGet("Logo")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetLogo()
    {
        var path = FindLogo();
        if (path is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "public, max-age=86400";
        return PhysicalFile(path, LogoTypes[Path.GetExtension(path)]);
    }

    private string? FindLogo()
        => Directory.Exists(DataDir) ? Directory.EnumerateFiles(DataDir, "logo.*").FirstOrDefault() : null;

    // ------------------------------------------------------------------
    // Random backdrop from the library.
    // ------------------------------------------------------------------

    /// <summary>
    /// Redirects to the backdrop of a random movie or series. The CSS points
    /// at this as a background image, so every load of the web gets a
    /// different one (and a rotation asks for ?n=1, ?n=2... to get several).
    /// Anonymous, because url() in CSS cannot carry a token - which also
    /// means an unauthenticated visitor of the login page sees a random
    /// backdrop.
    /// </summary>
    [HttpGet("Backdrop")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetRandomBackdrop()
    {
        var item = _library.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series],
            ImageTypes = [ImageType.Backdrop],
            OrderBy = [(ItemSortBy.Random, SortOrder.Ascending)],
            Limit = 1,
            Recursive = true,
            IsVirtualItem = false,
        }).FirstOrDefault();

        if (item is null)
        {
            return NotFound();
        }

        var tag = item.GetImageInfo(ImageType.Backdrop, 0)?.DateModified.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Response.Headers.CacheControl = "no-store";
        return Redirect($"../Items/{item.Id:N}/Images/Backdrop/0?maxWidth=1920&quality=80&tag={tag}");
    }
}

/// <summary>Reply to /Status.</summary>
/// <param name="Enabled">The theme is enabled in the plugin configuration.</param>
/// <param name="PresentInBranding">Our block really is in Branding (someone may have deleted it by hand).</param>
/// <param name="ForeignCssLength">How many characters of foreign CSS Branding holds - so the page can say we share the space.</param>
/// <param name="HasLogo">A custom logo has been uploaded.</param>
/// <param name="FileTransformation">The File Transformation plugin is loaded - the client script is injected automatically.</param>
/// <param name="JsInjector">A JavaScript injector plugin is installed - the client script can be pasted into it.</param>
public sealed record StatusDto(bool Enabled, bool PresentInBranding, int ForeignCssLength, bool HasLogo, bool FileTransformation, bool JsInjector);

/// <summary>Reply to /Apply.</summary>
public sealed record ApplyResultDto(string Css, bool Applied);

/// <summary>Reply to a logo upload: the URL the page should write into the settings.</summary>
public sealed record LogoResultDto(string Url);
