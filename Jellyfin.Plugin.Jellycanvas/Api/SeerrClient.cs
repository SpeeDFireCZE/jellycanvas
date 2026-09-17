using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Jellycanvas.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Jellycanvas.Api;

/// <summary>
/// Talks to Seerr (Jellyseerr / Overseerr) for the home page rows. The
/// API key never leaves the server: the client asks this plugin, the
/// plugin asks Seerr. Answers are kept for a few minutes - a home page
/// opened by twenty people should not mean twenty rounds of requests.
/// </summary>
public sealed class SeerrClient
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);
    private static readonly Dictionary<string, (DateTime At, IReadOnlyList<SeerrItemDto> Items)> Cache = new();
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly IHttpClientFactory _http;
    private readonly ILogger _logger;

    public SeerrClient(IHttpClientFactory http, ILogger logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>The row kinds (SeerrRowKind names, lower-case).</summary>
    public static readonly string[] Kinds = { "upcoming", "recent", "pending", "available", "trending", "popularmovies", "populartv" };

    /// <summary>Drops the cached answers (after the settings change).</summary>
    public static void Forget()
    {
        lock (Cache)
        {
            Cache.Clear();
        }
    }

    /// <summary>Whether the URL and key work: Seerr's own status endpoint.</summary>
    public async Task<string> TestAsync(SeerrSettings s, CancellationToken ct)
    {
        using var client = Create(s);
        using var res = await client.GetAsync("api/v1/settings/main", ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            return $"HTTP {(int)res.StatusCode}";
        }

        var main = await res.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct).ConfigureAwait(false);
        var name = main.TryGetProperty("applicationTitle", out var t) ? t.GetString() : null;
        return "ok " + (name ?? "Seerr");
    }

    /// <summary>The items of one row, from the cache when fresh.</summary>
    public async Task<IReadOnlyList<SeerrItemDto>> GetAsync(SeerrSettings s, string kind, int limit, CancellationToken ct)
    {
        var key = kind + ":" + limit;
        lock (Cache)
        {
            if (Cache.TryGetValue(key, out var hit) && DateTime.UtcNow - hit.At < CacheFor)
            {
                return hit.Items;
            }
        }

        await Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            lock (Cache)
            {
                if (Cache.TryGetValue(key, out var hit) && DateTime.UtcNow - hit.At < CacheFor)
                {
                    return hit.Items;
                }
            }

            var items = await FetchAsync(s, kind, limit, ct).ConfigureAwait(false);
            lock (Cache)
            {
                Cache[key] = (DateTime.UtcNow, items);
            }

            return items;
        }
        finally
        {
            Gate.Release();
        }
    }

    private HttpClient Create(SeerrSettings s)
    {
        var client = _http.CreateClient("Jellycanvas.Seerr");
        client.BaseAddress = new Uri(s.Url.Trim().TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.Add("X-Api-Key", s.ApiKey.Trim());
        return client;
    }

    private async Task<IReadOnlyList<SeerrItemDto>> FetchAsync(SeerrSettings s, string kind, int limit, CancellationToken ct)
    {
        using var client = Create(s);
        var discover = kind switch
        {
            "trending" => "api/v1/discover/trending?page=1",
            "popularmovies" => "api/v1/discover/movies?page=1",
            "populartv" => "api/v1/discover/tv?page=1",
            _ => null,
        };
        if (discover is not null)
        {
            var page = await client.GetFromJsonAsync<JsonElement>(discover, ct).ConfigureAwait(false);
            var fixedType = kind == "popularmovies" ? "movie" : kind == "populartv" ? "tv" : null;
            return Results(page).Select(r => fixedType is null ? FromDiscover(r, s) : FromMedia(r, fixedType, s)).Where(i => i is not null).Cast<SeerrItemDto>().Take(limit).ToList();
        }

        // Requests carry only ids; the title, poster and date come from the
        // media endpoint, one call per distinct title (a few at a time).
        var filter = kind switch { "upcoming" => "approved", "pending" => "pending", "available" => "available", _ => "all" };
        var requests = await client.GetFromJsonAsync<JsonElement>($"api/v1/request?take=60&skip=0&sort=added&filter={filter}", ct).ConfigureAwait(false);
        var wanted = new List<(string Type, int TmdbId, string By, DateTime Added)>();
        foreach (var r in Results(requests))
        {
            if (!r.TryGetProperty("media", out var media) || !media.TryGetProperty("tmdbId", out var tmdb))
            {
                continue;
            }

            var type = media.TryGetProperty("mediaType", out var mt) ? mt.GetString() ?? "movie" : "movie";
            var status = media.TryGetProperty("status", out var st) ? st.GetInt32() : 0;
            // Upcoming: approved but not in the library yet (5 = available).
            if (kind == "upcoming" && status >= 5)
            {
                continue;
            }

            var by = r.TryGetProperty("requestedBy", out var rb) && rb.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? string.Empty : string.Empty;
            var added = r.TryGetProperty("createdAt", out var ca) && DateTime.TryParse(ca.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var d) ? d : DateTime.MinValue;
            var id = tmdb.GetInt32();
            if (!wanted.Any(w => w.Type == type && w.TmdbId == id))
            {
                wanted.Add((type, id, by, added));
            }
        }

        var details = new List<SeerrItemDto>();
        var throttle = new SemaphoreSlim(4, 4);
        var tasks = wanted.Take(40).Select(async w =>
        {
            await throttle.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var el = await client.GetFromJsonAsync<JsonElement>($"api/v1/{(w.Type == "tv" ? "tv" : "movie")}/{w.TmdbId}", ct).ConfigureAwait(false);
                var item = FromMedia(el, w.Type, s);
                if (item is not null)
                {
                    item = item with { RequestedBy = w.By, Added = w.Added == DateTime.MinValue ? null : w.Added.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) };
                    lock (details)
                    {
                        details.Add(item);
                    }
                }
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogDebug(e, "Jellycanvas: Seerr media {Type}/{Id} not read", w.Type, w.TmdbId);
            }
            finally
            {
                throttle.Release();
            }
        });
        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Upcoming: what releases next comes first, then titles without a
        // date, then what is out already but still on its way (newest first).
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        IEnumerable<SeerrItemDto> ordered = kind == "upcoming"
            ? details.OrderBy(i => string.IsNullOrEmpty(i.Date) ? "1" : string.CompareOrdinal(i.Date, today) >= 0 ? "0" + i.Date : "2" + Flip(i.Date), StringComparer.Ordinal)
            : details.OrderByDescending(i => i.Added ?? string.Empty, StringComparer.Ordinal);
        return ordered.Take(limit).ToList();
    }

    /// <summary>"2024-05-17" → "7975-94-82": sorts ascending where the date would sort descending.</summary>
    private static string Flip(string date)
        => new string(date.Select(c => char.IsDigit(c) ? (char)('9' - (c - '0')) : c).ToArray());

    private static IEnumerable<JsonElement> Results(JsonElement page)
        => page.TryGetProperty("results", out var r) && r.ValueKind == JsonValueKind.Array ? r.EnumerateArray() : Enumerable.Empty<JsonElement>();

    private static SeerrItemDto? FromDiscover(JsonElement r, SeerrSettings s)
    {
        var type = r.TryGetProperty("mediaType", out var mt) ? mt.GetString() ?? "movie" : "movie";
        if (type != "movie" && type != "tv")
        {
            return null;
        }

        return FromMedia(r, type, s);
    }

    private static SeerrItemDto? FromMedia(JsonElement m, string type, SeerrSettings s)
    {
        var id = m.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0;
        var title = Str(m, type == "tv" ? "name" : "title") ?? Str(m, "title") ?? Str(m, "name");
        if (id == 0 || string.IsNullOrEmpty(title))
        {
            return null;
        }

        var date = Str(m, type == "tv" ? "firstAirDate" : "releaseDate") ?? string.Empty;
        var poster = Str(m, "posterPath");
        var status = 0;
        string? jellyfinId = null;
        if (m.TryGetProperty("mediaInfo", out var info) && info.ValueKind == JsonValueKind.Object)
        {
            status = info.TryGetProperty("status", out var st) ? st.GetInt32() : 0;
            jellyfinId = Str(info, "jellyfinMediaId") ?? Str(info, "jellyfinMediaId4k");
        }

        return new SeerrItemDto(
            title,
            type,
            id,
            string.IsNullOrEmpty(poster) ? null : poster,
            date.Length >= 10 ? date[..10] : date,
            status,
            jellyfinId,
            s.Url.Trim().TrimEnd('/') + "/" + type + "/" + id,
            string.Empty,
            null);
    }

    private static string? Str(JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}

/// <summary>One poster in a Seerr row.</summary>
/// <param name="Title">Title.</param>
/// <param name="Type">"movie" or "tv".</param>
/// <param name="TmdbId">TMDB id.</param>
/// <param name="Poster">TMDB poster path ("/abc.jpg"), served through the plugin's image proxy; or null.</param>
/// <param name="Date">Release / first-air date (yyyy-MM-dd), possibly empty.</param>
/// <param name="Status">Seerr media status: 2 pending, 3 processing, 4 partially, 5 available.</param>
/// <param name="JellyfinId">The item's id in this Jellyfin when Seerr knows it.</param>
/// <param name="SeerrUrl">The title's page in Seerr.</param>
/// <param name="RequestedBy">Who requested it (requests only).</param>
/// <param name="Added">When it was requested (requests only).</param>
public sealed record SeerrItemDto(
    string Title,
    string Type,
    int TmdbId,
    string? Poster,
    string Date,
    int Status,
    string? JellyfinId,
    string SeerrUrl,
    string RequestedBy,
    string? Added);
