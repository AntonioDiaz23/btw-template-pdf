using Btw.TemplatePdf.Application.Templates;
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

    public async Task<IReadOnlyList<TemplateDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Templates
            .AsNoTracking()
            .OrderByDescending(t => t.UpdatedAt)
            .Select(t => new TemplateDto(
                t.Id,
                t.Name,
                t.DocumentType,
                t.Status,
                t.CurrentVersionNumber,
                t.UpdatedAt,
                t.Nit,
                t.SectorSalud))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
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
            Nit = request.Nit.Trim(),
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
                    AssetsJson = request.AssetsJson ?? "[]",
                    CreatedAt = now,
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

        var current = template.Versions
            .OrderByDescending(v => v.VersionNumber)
            .First();

        var now = DateTimeOffset.UtcNow;
        if (request.SectorSalud is bool sector)
            template.SectorSalud = sector;
        if (!string.IsNullOrWhiteSpace(request.Nit))
            template.Nit = request.Nit.Trim();

        var html = request.Html ?? current.Html;
        var css = request.Css ?? current.Css;
        var schemaJson = request.SchemaJson ?? current.SchemaJson;
        var sampleDataJson = request.SampleDataJson ?? current.SampleDataJson;
        var blocksJson = request.BlocksJson ?? current.BlocksJson;

        if (current.IsPublished || template.Status == "published")
        {
            var draft = new TemplateVersionEntity
            {
                Id = Guid.NewGuid(),
                TemplateId = template.Id,
                VersionNumber = current.VersionNumber + 1,
                Html = html,
                Css = css,
                SchemaJson = schemaJson,
                SampleDataJson = sampleDataJson,
                BlocksJson = blocksJson,
                PageJson = request.PageJson ?? current.PageJson,
                AssetsJson = request.AssetsJson ?? current.AssetsJson,
                CreatedAt = now,
                IsPublished = false
            };
            template.Versions.Add(draft);
            template.Status = "draft";
            template.UpdatedAt = now;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return MapVersion(draft);
        }

        current.Html = html;
        current.Css = css;
        current.SchemaJson = schemaJson;
        current.SampleDataJson = sampleDataJson;
        current.BlocksJson = blocksJson;
        if (request.PageJson is not null)
            current.PageJson = request.PageJson;
        if (request.AssetsJson is not null)
            current.AssetsJson = request.AssetsJson;
        current.CreatedAt = now;
        current.IsPublished = false;

        template.Status = "draft";
        template.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapVersion(current);
    }

    public async Task<TemplateVersionDto> PublishAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await _db.Templates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Template '{id}' was not found.");

        var current = template.Versions
            .OrderByDescending(v => v.VersionNumber)
            .First();

        var now = DateTimeOffset.UtcNow;

        if (current.IsPublished)
        {
            var next = new TemplateVersionEntity
            {
                Id = Guid.NewGuid(),
                TemplateId = template.Id,
                VersionNumber = current.VersionNumber + 1,
                Html = current.Html,
                Css = current.Css,
                SchemaJson = current.SchemaJson,
                SampleDataJson = current.SampleDataJson,
                BlocksJson = current.BlocksJson,
                PageJson = current.PageJson,
                AssetsJson = current.AssetsJson,
                CreatedAt = now,
                IsPublished = true
            };
            foreach (var version in template.Versions)
                version.IsPublished = false;
            template.Versions.Add(next);
            template.CurrentVersionNumber = next.VersionNumber;
            template.Status = "published";
            template.UpdatedAt = now;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return MapVersion(next);
        }

        foreach (var version in template.Versions)
            version.IsPublished = false;
        current.IsPublished = true;
        current.CreatedAt = now;
        template.Status = "published";
        template.CurrentVersionNumber = current.VersionNumber;
        template.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapVersion(current);
    }

    private static TemplateBundleDto MapBundle(TemplateEntity template) =>
        new(
            MapTemplate(template),
            template.Versions
                .OrderByDescending(v => v.VersionNumber)
                .Select(MapVersion)
                .ToList());

    private static TemplateDto MapTemplate(TemplateEntity t) =>
        new(t.Id, t.Name, t.DocumentType, t.Status, t.CurrentVersionNumber, t.UpdatedAt, t.Nit, t.SectorSalud);

    private static TemplateVersionDto MapVersion(TemplateVersionEntity v) =>
        new(
            v.Id,
            v.TemplateId,
            v.VersionNumber,
            v.Html,
            v.Css,
            v.SchemaJson,
            v.SampleDataJson,
            v.BlocksJson,
            v.CreatedAt,
            v.IsPublished,
            v.AssetsJson);
}
