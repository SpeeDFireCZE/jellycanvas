using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
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
    private readonly IHttpClientFactory _http;

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
        ILogger<JellycanvasController> logger,
        IHttpClientFactory http)
    {
        _config = config;
        _library = library;
        _paths = paths;
        _plugins = plugins;
        _logger = logger;
        _http = http;
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
        SeerrClient.Forget();
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
        SeerrClient.Forget();
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
    // The theme files of the web client. An upgrade from 10.x can leave
    // the old themes/*/theme.css behind; those do not read the --jf-*
    // variables, so the palette (and the card rounding) is ignored there.
    // The generated CSS carries the missing rules itself, this just tells
    // the admin and can patch the files on disk on request.
    // ------------------------------------------------------------------

    private string ThemesDir => Path.Combine(_paths.WebPath, "themes");

    /// <summary>Which theme files are current, old, or already patched.</summary>
    [HttpGet("Themes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<ThemesDto> GetThemes()
    {
        var list = new List<ThemeFileDto>();
        if (Directory.Exists(ThemesDir))
        {
            foreach (var dir in Directory.GetDirectories(ThemesDir).OrderBy(d => d, StringComparer.Ordinal))
            {
                var file = Path.Combine(dir, "theme.css");
                if (!System.IO.File.Exists(file))
                {
                    continue;
                }

                var css = System.IO.File.ReadAllText(file);
                list.Add(new ThemeFileDto(
                    Path.GetFileName(dir),
                    css.Length,
                    css.Contains(ThemeBridge.Signature, StringComparison.Ordinal) && !css.Contains(ThemeBridge.RepairMarker, StringComparison.Ordinal),
                    css.Contains(ThemeBridge.RepairMarker, StringComparison.Ordinal),
                    null));
            }
        }

        return new ThemesDto(ThemesDir, list);
    }

    /// <summary>
    /// Appends the bridge rules to every old theme.css (a copy is kept as
    /// theme.css.jellycanvas-bak). Fails per file where the web folder is
    /// not writable - a packaged install usually is not; then the reply
    /// says so and the admin reinstalls jellyfin-web instead.
    /// </summary>
    [HttpPost("Themes/Repair")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<ThemesDto> RepairThemes()
    {
        var before = GetThemes().Value!;
        var list = new List<ThemeFileDto>();
        foreach (var theme in before.Themes)
        {
            if (theme.Current || theme.Repaired)
            {
                list.Add(theme);
                continue;
            }

            var file = Path.Combine(ThemesDir, theme.Name, "theme.css");
            try
            {
                var backup = file + ".jellycanvas-bak";
                if (!System.IO.File.Exists(backup))
                {
                    System.IO.File.Copy(file, backup);
                }

                System.IO.File.AppendAllText(file, ThemeBridge.RepairBlock());
                _logger.LogInformation("Jellycanvas: theme bridge appended to {File}", file);
                list.Add(theme with { Repaired = true, Size = (int)new FileInfo(file).Length });
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(e, "Jellycanvas: could not patch {File}", file);
                list.Add(theme with { Error = e.Message });
            }
        }

        return new ThemesDto(ThemesDir, list);
    }

    // ------------------------------------------------------------------
    // Seerr rows. The client script asks here (as the signed-in user); the
    // server asks Seerr with the key from the settings.
    // ------------------------------------------------------------------

    /// <summary>
    /// The posters of one row: kind = upcoming, recent or trending. For any
    /// signed-in user, not just admins: the class-level policy would add
    /// up with a method-level one, so the check is done by hand.
    /// </summary>
    [HttpGet("Seerr/{kind}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SeerrItemDto>>> GetSeerrRow([FromRoute] string kind, [FromQuery] int limit = 20, [FromQuery] string media = "both", CancellationToken ct = default)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized();
        }

        var s = Plugin.Instance!.Configuration.Seerr;
        if (string.IsNullOrWhiteSpace(s.Url) || string.IsNullOrWhiteSpace(s.ApiKey) || !SeerrClient.Kinds.Contains(kind))
        {
            return Array.Empty<SeerrItemDto>();
        }

        try
        {
            var items = await new SeerrClient(_http, _logger).GetAsync(s, kind, Math.Clamp(limit, 1, 60), media is "movies" or "series" ? media : "both", ct).ConfigureAwait(false);
            // Who requested a title is shown only where a row asks for it;
            // otherwise the names stay on the server.
            var showsRequester = s.Rows.Any(r => r.Enabled && r.ShowRequester && string.Equals(r.Kind.ToString(), kind, StringComparison.OrdinalIgnoreCase));
            return showsRequester ? items.ToList() : items.Select(i => i with { RequestedBy = string.Empty }).ToList();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or UriFormatException)
        {
            _logger.LogWarning(e, "Jellycanvas: Seerr row {Kind} failed", kind);
            return Array.Empty<SeerrItemDto>();
        }
    }

    /// <summary>
    /// A TMDB poster for a Seerr row, fetched by the server and kept on disk
    /// for a week: the browser then needs no access to TMDB itself (some
    /// networks block it) and the same poster is not fetched by everyone.
    /// Anonymous like the logo: an image URL cannot carry a token.
    /// </summary>
    [HttpGet("Seerr/Image")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetSeerrImage([FromQuery] string p, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(p) || !System.Text.RegularExpressions.Regex.IsMatch(p, "^/[A-Za-z0-9_-]{1,64}\\.(jpg|jpeg|png|webp)$"))
        {
            return NotFound();
        }

        var dir = Path.Combine(DataDir, "seerr-images");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, p.TrimStart('/'));
        if (!System.IO.File.Exists(file) || DateTime.UtcNow - System.IO.File.GetLastWriteTimeUtc(file) > TimeSpan.FromDays(7))
        {
            // Only posters that came up in a Seerr answer are fetched: the
            // address is open, and it must not be a way to make this server
            // pull and store arbitrary TMDB files.
            if (!SeerrClient.IsKnownPoster(p))
            {
                return NotFound();
            }

            try
            {
                using var client = _http.CreateClient("Jellycanvas.Tmdb");
                client.Timeout = TimeSpan.FromSeconds(15);
                var bytes = await client.GetByteArrayAsync("https://image.tmdb.org/t/p/w342" + p, ct).ConfigureAwait(false);
                await System.IO.File.WriteAllBytesAsync(file, bytes, ct).ConfigureAwait(false);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException)
            {
                _logger.LogDebug(e, "Jellycanvas: poster {Path} not fetched", p);
                return NotFound();
            }
        }

        var type = p.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : p.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ? "image/webp" : "image/jpeg";
        Response.Headers.CacheControl = "public, max-age=604800";
        Response.Headers.XContentTypeOptions = "nosniff";
        return PhysicalFile(file, type);
    }

    /// <summary>Tries the address and key from the body (unsaved settings) against Seerr. Admin only.</summary>
    [HttpPost("Seerr/Test")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<SeerrTestDto>> TestSeerr([FromBody] SeerrSettings s, CancellationToken ct)
    {
        try
        {
            var client = new SeerrClient(_http, _logger);
            var result = await client.TestAsync(s, ct).ConfigureAwait(false);
            SeerrClient.Forget();
            var ok = result.StartsWith("ok", StringComparison.Ordinal);
            if (ok)
            {
                // The rows as configured, so an empty row is explained here
                // rather than by a home page with nothing on it.
                result += " | " + await client.ProbeRowsAsync(s, ct).ConfigureAwait(false);
            }

            return new SeerrTestDto(ok, result);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or UriFormatException or InvalidOperationException)
        {
            return new SeerrTestDto(false, e.Message);
        }
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

            // An SVG is a document the browser can run scripts from when it is
            // opened by its address; a logo has no business carrying any.
            if (ext.Equals(".svg", StringComparison.OrdinalIgnoreCase) && SvgHasScript(await System.IO.File.ReadAllTextAsync(temp).ConfigureAwait(false)))
            {
                System.IO.File.Delete(temp);
                return BadRequest("The SVG contains script or event handlers - not allowed in a logo.");
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
        Response.Headers.XContentTypeOptions = "nosniff";
        // Opened by its address the file is a document of this origin; as an
        // image (CSS, <img>) the policy is irrelevant. Scripts off, always.
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; img-src data:; sandbox";
        return PhysicalFile(path, LogoTypes[Path.GetExtension(path)]);
    }

    private string? FindLogo()
        => Directory.Exists(DataDir) ? Directory.EnumerateFiles(DataDir, "logo.*").FirstOrDefault() : null;

    private static bool SvgHasScript(string svg)
        => System.Text.RegularExpressions.Regex.IsMatch(svg, @"<\s*script|\son[a-z]+\s*=|javascript\s*:|<\s*foreignObject|<\s*iframe|<\s*embed", System.Text.RegularExpressions.RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

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

/// <summary>Reply to /Seerr/Test.</summary>
/// <param name="Ok">The address and key work.</param>
/// <param name="Message">What Seerr said, or the error.</param>
public sealed record SeerrTestDto(bool Ok, string Message);

/// <summary>The web client's theme files.</summary>
/// <param name="Path">The themes folder that was looked at.</param>
/// <param name="Themes">One entry per theme.</param>
public sealed record ThemesDto(string Path, IReadOnlyList<ThemeFileDto> Themes);

/// <summary>One themes/NAME/theme.css.</summary>
/// <param name="Name">The theme folder (dark, light, ...).</param>
/// <param name="Size">File size in characters.</param>
/// <param name="Current">The file reads the --jf-* variables (Jellyfin 12 format) and is untouched.</param>
/// <param name="Repaired">The bridge block has been appended to it.</param>
/// <param name="Error">Why the repair failed, if it did.</param>
public sealed record ThemeFileDto(string Name, int Size, bool Current, bool Repaired, string? Error);

/// <summary>Reply to a logo upload: the URL the page should write into the settings.</summary>
public sealed record LogoResultDto(string Url);
