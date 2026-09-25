using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.Jellycanvas.Scripts;

/// <summary>
/// Glue to the File Transformation plugin (IAmParadox27). Jellyfin itself
/// has no supported way for a plugin to add a script to the web client;
/// File Transformation patches the served files, and other plugins register
/// their patches with it.
///
/// Because plugins load in separate assembly contexts, File Transformation
/// cannot be referenced as a library. Registration goes through reflection:
/// find its assembly, call <c>PluginInterface.RegisterTransformation</c> with
/// a JSON payload naming our callback. When index.html is served, File
/// Transformation calls that method with the file contents and uses what it
/// returns. The payload shape and the JObject type are exactly what the
/// plugin's README prescribes.
/// </summary>
public static class FileTransformation
{
    /// <summary>The id of our transformation as registered with File Transformation - stable across restarts.</summary>
    public const string TransformationId = "7c1e9d1a-3b5f-4c0e-9a7d-433e86f7318f";

    /// <summary>Is the File Transformation plugin loaded in this server?</summary>
    public static bool IsAvailable() => FindAssembly() is not null;

    /// <summary>
    /// Registers the index.html patch. Safe to call when File Transformation
    /// is missing (does nothing) or again after it was already registered
    /// (the plugin de-duplicates by id).
    /// </summary>
    public static bool Register(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        var assembly = FindAssembly();
        if (assembly is null)
        {
            logger.LogInformation("Jellycanvas: File Transformation plugin not found - the client script will not be injected");
            return false;
        }

        var pluginInterface = assembly.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface");
        var register = pluginInterface?.GetMethod("RegisterTransformation");
        if (register is null)
        {
            logger.LogWarning("Jellycanvas: File Transformation is loaded but has no PluginInterface.RegisterTransformation - version mismatch?");
            return false;
        }

        var payload = new JObject
        {
            ["id"] = TransformationId,
            ["fileNamePattern"] = "index.html",
            ["callbackAssembly"] = typeof(FileTransformation).Assembly.FullName,
            ["callbackClass"] = typeof(FileTransformation).FullName,
            ["callbackMethod"] = nameof(IndexHtml),
        };

        try
        {
            register.Invoke(null, [payload]);
            logger.LogInformation("Jellycanvas: index.html transformation registered with File Transformation");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Jellycanvas: registering with File Transformation failed");
            return false;
        }
    }

    /// <summary>
    /// The callback File Transformation invokes for index.html. Adds our
    /// script tag before &lt;/body&gt;. The tag is always added; the script
    /// itself is empty when nothing is enabled, so the page stays clean
    /// without needing a re-registration.
    /// </summary>
    public static string IndexHtml(PatchRequestPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var contents = payload.Contents ?? string.Empty;
        if (contents.Contains("data-jellycanvas-script", StringComparison.Ordinal))
        {
            return contents;
        }

        // CSS only: index.html is served exactly as Jellyfin has it.
        if (Plugin.Instance?.Configuration.CssOnly == true)
        {
            return contents;
        }

        // Base URL for installs behind a reverse proxy path (/jellyfin/...).
        var root = string.Empty;
        var config = Plugin.Instance?.ConfigurationManager;
        var baseUrl = config?.GetNetworkConfiguration().BaseUrl;
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            root = "/" + baseUrl.Trim().Trim('/');
        }

        // The query carries the plugin version and a stamp of the current
        // settings: a changed setting changes the address, so no client
        // (a phone's web view, a PWA) keeps running a script it cached.
        var version = Plugin.Instance?.Version.ToString() ?? "0";
        var stamp = Plugin.Instance is null ? "0" : ScriptBuilder.Stamp(Plugin.Instance.Configuration);
        var tag = string.Format(
            CultureInfo.InvariantCulture,
            "<script data-jellycanvas-script=\"1\" src=\"{0}/Jellycanvas/Script.js?v={1}-{2}\" defer></script>",
            root,
            version,
            stamp);

        return Regex.Replace(contents, "(</body>)", tag + "$1", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
    }

    private static Assembly? FindAssembly()
        => AssemblyLoadContext.All
            .SelectMany(x => x.Assemblies)
            .FirstOrDefault(x => x.FullName?.Contains(".FileTransformation", StringComparison.Ordinal) ?? false);
}

/// <summary>The object File Transformation hands to a callback: the current contents of the file being served.</summary>
public class PatchRequestPayload
{
    [JsonPropertyName("contents")]
    public string? Contents { get; set; }
}

/// <summary>
/// Registers with File Transformation at server start. A scheduled task with
/// a startup trigger is the hook Jellyfin offers plugins for "run once after
/// boot", and it is the same mechanism the File Transformation author's own
/// plugins use, so the ordering between the two plugins is known to work.
/// </summary>
public class StartupTask : IScheduledTask
{
    private readonly ILogger<StartupTask> _logger;

    public StartupTask(ILogger<StartupTask> logger)
    {
        _logger = logger;
    }

    public string Name => "Jellycanvas startup";

    public string Key => "Jellyfin.Plugin.Jellycanvas.Startup";

    public string Description => "Registers the Jellycanvas client script with the File Transformation plugin.";

    public string Category => "Startup Services";

    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        FileTransformation.Register(_logger);
        return Task.CompletedTask;
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo { Type = TaskTriggerInfoType.StartupTrigger };
    }
}
