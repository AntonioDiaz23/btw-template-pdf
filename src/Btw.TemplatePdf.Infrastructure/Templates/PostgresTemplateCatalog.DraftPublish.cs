using Btw.TemplatePdf.Application.Templates;
using Btw.TemplatePdf.Infrastructure.Assets;
using Btw.TemplatePdf.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Btw.TemplatePdf.Infrastructure.Templates;

public sealed partial class PostgresTemplateCatalog
{
    public async Task<TemplateVersionDto> SaveDraftAsync(
        Guid id,
        SaveDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        var template = await _db.Templates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Template '{id}' was not found.");

        EnsureNotArchived(template);

        var tip = Tip(template);
        var now = DateTimeOffset.UtcNow;

        if (request.SectorSalud is bool sector)
            template.SectorSalud = sector;
        if (!string.IsNullOrWhiteSpace(request.Nit))
            template.Nit = request.Nit.Trim();

        if (!VersionStatuses.IsDraft(tip.Status))
        {
            var published = template.Versions
                .Where(v => VersionStatuses.IsPublished(v.Status) || v.IsPublished)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefault();
            var source = VersionStatuses.IsUsed(tip.Status) && published is not null
                ? published
                : tip;

            var assetsJson = await BrandAssetHydrator.HydrateAssetsJsonAsync(
                    _db,
                    request.AssetsJson,
                    source.AssetsJson,
                    cancellationToken)
                .ConfigureAwait(false);

            var draft = new TemplateVersionEntity
            {
                Id = Guid.NewGuid(),
                TemplateId = template.Id,
                VersionNumber = tip.VersionNumber + 1,
                Html = request.Html ?? source.Html,
                Css = request.Css ?? source.Css,
                SchemaJson = request.SchemaJson ?? source.SchemaJson,
                SampleDataJson = request.SampleDataJson ?? source.SampleDataJson,
                BlocksJson = request.BlocksJson ?? source.BlocksJson,
                PageJson = request.PageJson ?? source.PageJson,
                AssetsJson = assetsJson,
                CreatedAt = now,
                Status = VersionStatuses.Draft,
                IsPublished = false
            };
            template.Versions.Add(draft);
            _db.TemplateVersions.Add(draft);
            template.UpdatedAt = now;
            DatabaseInitializer.SyncTemplateFlags(template);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return MapVersion(draft);
        }

        var draftAssets = await BrandAssetHydrator.HydrateAssetsJsonAsync(
                _db,
                request.AssetsJson,
                tip.AssetsJson,
                cancellationToken)
            .ConfigureAwait(false);

        tip.Html = request.Html ?? tip.Html;
        tip.Css = request.Css ?? tip.Css;
        tip.SchemaJson = request.SchemaJson ?? tip.SchemaJson;
        tip.SampleDataJson = request.SampleDataJson ?? tip.SampleDataJson;
        tip.BlocksJson = request.BlocksJson ?? tip.BlocksJson;
        if (request.PageJson is not null)
            tip.PageJson = request.PageJson;
        tip.AssetsJson = draftAssets;
        tip.CreatedAt = now;
        tip.Status = VersionStatuses.Draft;
        tip.IsPublished = false;

        template.UpdatedAt = now;
        DatabaseInitializer.SyncTemplateFlags(template);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapVersion(tip);
    }

    public async Task<TemplateVersionDto> PublishAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await _db.Templates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Template '{id}' was not found.");

        EnsureNotArchived(template);

        var tip = Tip(template);
        var now = DateTimeOffset.UtcNow;

        if (VersionStatuses.IsPublished(tip.Status))
        {
            DatabaseInitializer.SyncTemplateFlags(template);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return MapVersion(tip);
        }

        if (VersionStatuses.IsUsed(tip.Status))
            throw new InvalidOperationException("Cannot publish a used version. Save a new draft first.");

        foreach (var version in template.Versions)
        {
            if (VersionStatuses.IsPublished(version.Status) || version.IsPublished)
            {
                version.Status = VersionStatuses.Used;
                version.IsPublished = false;
            }
        }

        tip.Status = VersionStatuses.Published;
        tip.IsPublished = true;
        tip.CreatedAt = now;
        template.UpdatedAt = now;
        DatabaseInitializer.SyncTemplateFlags(template);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapVersion(tip);
    }

    public async Task DeleteDraftAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await _db.Templates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Template '{id}' was not found.");

        EnsureNotArchived(template);

        var tip = Tip(template);
        if (!VersionStatuses.IsDraft(tip.Status))
            throw new InvalidOperationException("Only a draft tip can be discarded.");

        if (template.Versions.Count == 1)
            throw new InvalidOperationException(
                "Cannot discard the only version. Delete the template instead, or publish first.");

        template.Versions.Remove(tip);
        _db.TemplateVersions.Remove(tip);
        template.UpdatedAt = DateTimeOffset.UtcNow;
        DatabaseInitializer.SyncTemplateFlags(template);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
