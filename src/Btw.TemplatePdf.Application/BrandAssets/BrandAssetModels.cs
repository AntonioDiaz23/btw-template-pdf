using Btw.TemplatePdf.Application.Common;

namespace Btw.TemplatePdf.Application.BrandAssets;

public sealed record BrandAssetDto(
    Guid Id,
    string Nit,
    string Name,
    string Mime,
    long SizeBytes,
    DateTimeOffset CreatedAt,
    string ContentUrl);

public sealed record BrandAssetContent(
    Guid Id,
    string Name,
    string Mime,
    byte[] Bytes);

public sealed record UploadBrandAssetRequest(
    string Nit,
    string FileName,
    string ContentType,
    byte[] Bytes);

/// <summary>Persistence port for company brand image library.</summary>
public interface IBrandAssetStore
{
    Task<IReadOnlyList<BrandAssetDto>> ListAsync(
        string? nit,
        CancellationToken cancellationToken = default);

    Task<BrandAssetContent?> GetContentAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<BrandAssetDto> CreateAsync(
        UploadBrandAssetRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> CountByNitAsync(string nit, CancellationToken cancellationToken = default);
}

public static class BrandAssetLimits
{
    public const long MaxBytes = 1_500_000;
    public const int MaxAssetsPerNit = 5;
    public const string DefaultDemoNit = "900000000";

    public static string ContentUrl(Guid id) => $"/api/v1/brand-assets/{id}/content";
}
