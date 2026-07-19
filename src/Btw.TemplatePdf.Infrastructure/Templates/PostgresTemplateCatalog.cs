using Btw.TemplatePdf.Application.Templates;
using Btw.TemplatePdf.Infrastructure.Assets;
using Btw.TemplatePdf.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Btw.TemplatePdf.Infrastructure.Templates;

public sealed class PostgresTemplateCatalog : ITemplateCatalog
{
    private readonly TemplateDbContext _db;

    public PostgresTemplateCatalog(TemplateDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TemplateDto>> ListAsync(
        string nit,
        CancellationToken cancellationToken = default)
    {
        var templates = await _db.Templates
            .AsNoTracking()
            .Include(t => t.Versions)
            .Where(t => t.Nit == nit && t.Status != TemplateStatuses.Archived)
            .OrderByDescending(t => t.UpdatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return templates.Select(MapTemplate).ToList();
    }

    public async Task<TemplateBundleDto?> GetBundleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await _db.Templates
            .AsNoTracking()
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return template is null ? null : MapBundle(template);
    }

    public async Task<TemplateDto> CreateAsync(
        CreateTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var entity = new TemplateEntity
        {
            Id = templateId,
            Name = request.Name.Trim(),
            DocumentType = string.IsNullOrWhiteSpace(request.DocumentType) ? "factura" : request.DocumentType,
            Status = "draft",
            CurrentVersionNumber = 1,
            Nit = request.Nit.Trim(), // already normalized by CreateTemplateUseCase
            SectorSalud = request.SectorSalud,
            UpdatedAt = now,
            Versions =
            {
                new TemplateVersionEntity
                {
                    Id = versionId,
                    TemplateId = templateId,
                    VersionNumber = 1,
                    Html = request.Html ?? "",
                    Css = request.Css ?? "",
                    SchemaJson = request.SchemaJson ?? "{}",
                    SampleDataJson = request.SampleDataJson ?? "{}",
                    BlocksJson = request.BlocksJson ?? "[]",
                    PageJson = request.PageJson ?? "{}",
                    AssetsJson = await BrandAssetHydrator.HydrateAssetsJsonAsync(
                            _db,
                            request.AssetsJson,
                            "[]",
                            cancellationToken)
                        .ConfigureAwait(false),
                    CreatedAt = now,
                    Status = VersionStatuses.Draft,
                    IsPublished = false
                }
            }
        };

        _db.Templates.Add(entity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapTemplate(entity);
    }

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
            template.Nit = request.Nit.Trim(); // digits-only when provided by SaveDraftUseCase

        // Published / used tips are immutable — fork a new draft from the live published
        // version when the tip is an older "used" snapshot (e.g. after rollback).
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

        // Upsert existing draft tip (same version number).
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

        // Already on a published tip — no empty clone.
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

    public async Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await _db.Templates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Template '{id}' was not found.");

        if (TemplateStatuses.IsArchived(template.Status))
            return;

        // Demote live published tip so GetPublished cannot select this template.
        foreach (var version in template.Versions)
        {
            if (VersionStatuses.IsPublished(version.Status) || version.IsPublished)
            {
                version.Status = VersionStatuses.Used;
                version.IsPublished = false;
            }
        }

        template.Status = TemplateStatuses.Archived;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await _db.Templates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Template '{id}' was not found.");

        if (TemplateStatuses.IsArchived(template.Status))
        {
            throw new InvalidOperationException(
                "La plantilla está archivada. No se puede eliminar: conserva versiones para facturas ya graficadas.");
        }

        var hasReleasedVersion = template.Versions.Any(v =>
            VersionStatuses.IsPublished(v.Status)
            || VersionStatuses.IsUsed(v.Status)
            || v.IsPublished);
        if (hasReleasedVersion)
        {
            throw new InvalidOperationException(
                "La plantilla ya fue publicada. Archívala para ocultarla sin romper facturas pineadas.");
        }

        var bindingCount = await _db.InvoiceTemplateBindings
            .CountAsync(b => b.TemplateId == id, cancellationToken)
            .ConfigureAwait(false);
        if (bindingCount > 0)
        {
            throw new InvalidOperationException(
                "Hay facturas vinculadas a esta plantilla. Archívala en lugar de eliminarla.");
        }

        _db.TemplateVersions.RemoveRange(template.Versions);
        _db.Templates.Remove(template);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<TemplateVersionDto> RollbackToVersionAsync(
        Guid id,
        int versionNumber,
        CancellationToken cancellationToken = default)
    {
        var template = await _db.Templates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Template '{id}' was not found.");

        EnsureNotArchived(template);

        var target = template.Versions.FirstOrDefault(v => v.VersionNumber == versionNumber)
            ?? throw new InvalidOperationException($"Version {versionNumber} was not found.");

        if (VersionStatuses.IsPublished(target.Status) || target.IsPublished)
        {
            DatabaseInitializer.SyncTemplateFlags(template);
            return MapVersion(target);
        }

        if (!VersionStatuses.IsUsed(target.Status))
        {
            throw new InvalidOperationException(
                "Solo se puede volver a una versión usada (ya publicada antes).");
        }

        var tip = Tip(template);
        if (VersionStatuses.IsDraft(tip.Status))
        {
            if (template.Versions.Count <= 1)
                throw new InvalidOperationException("No se puede descartar el único borrador en rollback.");
            template.Versions.Remove(tip);
            _db.TemplateVersions.Remove(tip);
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var version in template.Versions)
        {
            if (VersionStatuses.IsPublished(version.Status) || version.IsPublished)
            {
                version.Status = VersionStatuses.Used;
                version.IsPublished = false;
            }
        }

        target.Status = VersionStatuses.Published;
        target.IsPublished = true;
        template.UpdatedAt = now;
        DatabaseInitializer.SyncTemplateFlags(template);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapVersion(target);
    }

    private static TemplateVersionEntity Tip(TemplateEntity template) =>
        template.Versions.OrderByDescending(v => v.VersionNumber).First();

    private static TemplateBundleDto MapBundle(TemplateEntity template) =>
        new(
            MapTemplate(template),
            template.Versions
                .OrderByDescending(v => v.VersionNumber)
                .Select(MapVersion)
                .ToList());

    private static TemplateDto MapTemplate(TemplateEntity t)
    {
        var tip = t.Versions.Count == 0
            ? null
            : t.Versions.OrderByDescending(v => v.VersionNumber).First();
        var published = t.Versions
            .Where(v => VersionStatuses.IsPublished(v.Status) || v.IsPublished)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault();
        var hasDraft = tip is not null && VersionStatuses.IsDraft(tip.Status);
        // After rollback, tip may be a higher "used" version — show published (or draft) as current.
        var currentNumber = hasDraft
            ? tip!.VersionNumber
            : published?.VersionNumber ?? tip?.VersionNumber ?? t.CurrentVersionNumber;

        var status = TemplateStatuses.IsArchived(t.Status)
            ? TemplateStatuses.Archived
            : published is null ? TemplateStatuses.Draft : TemplateStatuses.Published;

        return new TemplateDto(
            t.Id,
            t.Name,
            t.DocumentType,
            status,
            currentNumber,
            t.UpdatedAt,
            t.Nit,
            t.SectorSalud,
            published?.VersionNumber ?? 0,
            hasDraft);
    }

    private static void EnsureNotArchived(TemplateEntity template)
    {
        if (TemplateStatuses.IsArchived(template.Status))
        {
            throw new InvalidOperationException(
                "La plantilla está archivada y no se puede modificar.");
        }
    }

    private static TemplateVersionDto MapVersion(TemplateVersionEntity v)
    {
        var status = string.IsNullOrWhiteSpace(v.Status)
            ? (v.IsPublished ? VersionStatuses.Published : VersionStatuses.Draft)
            : v.Status;
        return new(
            v.Id,
            v.TemplateId,
            v.VersionNumber,
            v.Html,
            v.Css,
            v.SchemaJson,
            v.SampleDataJson,
            v.BlocksJson,
            v.CreatedAt,
            VersionStatuses.IsPublished(status),
            BrandAssetHydrator.StripDataUrls(v.AssetsJson),
            status);
    }
}
