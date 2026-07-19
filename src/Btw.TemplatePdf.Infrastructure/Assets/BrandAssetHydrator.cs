using System.Text.Json;
using Btw.TemplatePdf.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Btw.TemplatePdf.Infrastructure.Assets;

/// <summary>
/// Expands draft assetsJson (ids only) into a version snapshot with dataUrls
/// loaded from the branding library, so PDF pinning keeps immutable logos.
/// </summary>
public static class BrandAssetHydrator
{
    public static async Task<string> HydrateAssetsJsonAsync(
        TemplateDbContext db,
        string? incomingAssetsJson,
        string? previousAssetsJson,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(incomingAssetsJson))
            return previousAssetsJson ?? "[]";

        List<AssetItem> incoming;
        try
        {
            incoming = JsonSerializer.Deserialize<List<AssetItem>>(incomingAssetsJson, JsonOptions)
                       ?? new List<AssetItem>();
        }
        catch
        {
            return previousAssetsJson ?? "[]";
        }

        if (incoming.Count == 0)
            return "[]";

        var previousById = ParseMap(previousAssetsJson);
        var missingIds = incoming
            .Where(a => !string.IsNullOrWhiteSpace(a.Id)
                        && string.IsNullOrWhiteSpace(a.DataUrl)
                        && !previousById.ContainsKey(a.Id!))
            .Select(a => Guid.TryParse(a.Id, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .Distinct()
            .ToList();

        Dictionary<Guid, BrandAssetEntity> brandById = new();
        if (missingIds.Count > 0)
        {
            var rows = await db.BrandAssets
                .AsNoTracking()
                .Where(x => missingIds.Contains(x.Id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            brandById = rows.ToDictionary(x => x.Id);
        }

        var hydrated = new List<AssetItem>();
        foreach (var item in incoming)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
                continue;

            if (!string.IsNullOrWhiteSpace(item.DataUrl))
            {
                hydrated.Add(item);
                continue;
            }

            if (previousById.TryGetValue(item.Id, out var prev) &&
                !string.IsNullOrWhiteSpace(prev.DataUrl))
            {
                hydrated.Add(prev with
                {
                    Name = item.Name ?? prev.Name,
                    Mime = item.Mime ?? prev.Mime
                });
                continue;
            }

            if (Guid.TryParse(item.Id, out var guid) &&
                brandById.TryGetValue(guid, out var brand))
            {
                hydrated.Add(new AssetItem
                {
                    Id = item.Id,
                    Name = item.Name ?? brand.Name,
                    Mime = item.Mime ?? brand.Mime,
                    DataUrl = $"data:{brand.Mime};base64,{Convert.ToBase64String(brand.Bytes)}"
                });
            }
        }

        return JsonSerializer.Serialize(hydrated, JsonOptions);
    }

    /// <summary>API responses omit dataUrls so GET/PUT bodies stay small.</summary>
    public static string StripDataUrls(string? assetsJson)
    {
        if (string.IsNullOrWhiteSpace(assetsJson) || assetsJson.Trim() == "[]")
            return "[]";

        try
        {
            var items = JsonSerializer.Deserialize<List<AssetItem>>(assetsJson, JsonOptions)
                        ?? new List<AssetItem>();
            var slim = items
                .Where(x => !string.IsNullOrWhiteSpace(x.Id))
                .Select(x => new AssetItem
                {
                    Id = x.Id,
                    Name = x.Name,
                    Mime = x.Mime
                })
                .ToList();
            return JsonSerializer.Serialize(slim, JsonOptions);
        }
        catch
        {
            return "[]";
        }
    }

    private static Dictionary<string, AssetItem> ParseMap(string? json)
    {
        var map = new Dictionary<string, AssetItem>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
            return map;
        try
        {
            var items = JsonSerializer.Deserialize<List<AssetItem>>(json, JsonOptions)
                        ?? new List<AssetItem>();
            foreach (var item in items)
            {
                if (!string.IsNullOrWhiteSpace(item.Id))
                    map[item.Id] = item;
            }
        }
        catch
        {
            /* ignore */
        }

        return map;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private sealed record AssetItem
    {
        public string? Id { get; init; }
        public string? Name { get; init; }
        public string? Mime { get; init; }
        public string? DataUrl { get; init; }
    }
}
