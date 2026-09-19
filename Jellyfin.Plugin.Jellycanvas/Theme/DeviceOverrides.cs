using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.Jellycanvas.Configuration;

namespace Jellyfin.Plugin.Jellycanvas.Theme;

/// <summary>
/// Per-device settings on top of the defaults. The designer keeps each
/// device's changes as a sparse JSON document (only the keys the admin
/// changed for that device); merging it over the default configuration
/// gives the configuration the device's CSS is built from.
/// </summary>
public static class DeviceOverrides
{
    /// <summary>The devices with their own overrides and the selector that scopes their CSS.</summary>
    public static readonly (string Name, string Scope)[] Devices =
    [
        ("Web", "html:not(.layout-tv):not(.layout-mobile)"),
        ("Tv", "html.layout-tv"),
        ("Mobile", "html.layout-mobile"),
    ];

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Whether the document holds anything (not empty, not "{}").</summary>
    public static bool HasContent(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            return JsonNode.Parse(json) is JsonObject o && o.Count > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>The device's JSON document from the settings, by device name.</summary>
    public static string For(PluginConfiguration c, string device) => device switch
    {
        "Web" => c.Overrides.Web,
        "Tv" => c.Overrides.Tv,
        "Mobile" => c.Overrides.Mobile,
        _ => string.Empty,
    };

    /// <summary>
    /// The defaults with the device's changes laid over them. Objects merge
    /// key by key; a value or a list in the override replaces the default.
    /// A document that does not parse leaves the defaults as they are.
    /// </summary>
    public static PluginConfiguration Merge(PluginConfiguration defaults, string? overrideJson)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        if (!HasContent(overrideJson))
        {
            return defaults;
        }

        var baseNode = JsonSerializer.SerializeToNode(defaults, Options) as JsonObject;
        JsonObject? over;
        try
        {
            over = JsonNode.Parse(overrideJson!) as JsonObject;
        }
        catch (JsonException)
        {
            return defaults;
        }

        if (baseNode is null || over is null)
        {
            return defaults;
        }

        // The overrides never carry these: the server-side and script-only
        // parts stay whatever the defaults say.
        over.Remove("Overrides");
        over.Remove("Scripts");
        over.Remove("Seerr");
        over.Remove("Enabled");
        MergeInto(baseNode, over);
        try
        {
            return baseNode.Deserialize<PluginConfiguration>(Options) ?? defaults;
        }
        catch (JsonException)
        {
            return defaults;
        }
    }

    private static void MergeInto(JsonObject target, JsonObject source)
    {
        foreach (var (key, value) in source)
        {
            if (value is JsonObject so && target[key] is JsonObject to)
            {
                MergeInto(to, so);
            }
            else
            {
                target[key] = value?.DeepClone();
            }
        }
    }
}
