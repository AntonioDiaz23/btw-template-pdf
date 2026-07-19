using Btw.TemplatePdf.Application.BrandAssets;
using Btw.TemplatePdf.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Btw.TemplatePdf.Infrastructure.Assets;

public sealed class PostgresBrandAssetStore : IBrandAssetStore
{
    private readonly TemplateDbContext _db;

    public PostgresBrandAssetStore(TemplateDbContext db) => _db = db;

    public async Task<IReadOnlyList<BrandAssetDto>> ListAsync(
        string? nit,
        CancellationToken cancellationToken = default)
    {
        var query = _db.BrandAssets.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(nit))
            query = query.Where(x => x.Nit == nit);

        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(MapDto).ToList();
    }

    public async Task<BrandAssetContent?> GetContentAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var asset = await _db.BrandAssets.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (asset is null)
            return null;

        return new BrandAssetContent(asset.Id, asset.Name, asset.Mime, asset.Bytes);
    }

    public async Task<BrandAssetDto> CreateAsync(
        UploadBrandAssetRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = new BrandAssetEntity
        {
            Id = Guid.NewGuid(),
            Nit = request.Nit,
            Name = string.IsNullOrWhiteSpace(request.FileName)
                ? "imagen"
                : Path.GetFileName(request.FileName),
            Mime = string.IsNullOrWhiteSpace(request.ContentType)
                ? "image/png"
                : request.ContentType,
            Bytes = request.Bytes,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.BrandAssets.Add(entity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapDto(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.BrandAssets
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
            return false;

        _db.BrandAssets.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public Task<int> CountByNitAsync(string nit, CancellationToken cancellationToken = default) =>
        _db.BrandAssets.CountAsync(x => x.Nit == nit, cancellationToken);

    private static BrandAssetDto MapDto(BrandAssetEntity x) =>
        new(
            x.Id,
            x.Nit,
            x.Name,
            x.Mime,
            x.Bytes.LongLength,
            x.CreatedAt,
            BrandAssetLimits.ContentUrl(x.Id));
}
