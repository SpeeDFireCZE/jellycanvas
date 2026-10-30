using System;
using System.IO;
using System.Reflection;

namespace Jellyfin.Plugin.Jellycanvas.Scripts;

/// <summary>Reads a text file embedded next to this class (Scripts\*.js).</summary>
internal static class EmbeddedText
{
    /// <summary>Reads an embedded file of the Scripts folder.</summary>
    /// <param name="fileName">The file's name, e.g. inject.js.</param>
    /// <returns>Its text.</returns>
    public static string Read(string fileName)
    {
        var name = typeof(EmbeddedText).Namespace + "." + fileName;
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Embedded resource missing: " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
