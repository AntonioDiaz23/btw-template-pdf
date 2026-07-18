using Btw.TemplatePdf.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Btw.TemplatePdf.Infrastructure.Templates;

public sealed class TemplateCatalogService
{
    private readonly TemplateDbContext _db;

    public TemplateCatalogService(TemplateDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TemplateDto>> ListAsync(CancellationToken ct = default)
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
            .ToListAsync(ct);
    }

    public async Task<TemplateBundleDto> GetBundleAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _db.Templates
            .AsNoTracking()
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new KeyNotFoundException("No encontramos esa plantilla.");

        return MapBundle(template);
    }

    public async Task<TemplateDto> CreateAsync(CreateTemplateRequest request, CancellationToken ct = default)
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
            Nit = string.IsNullOrWhiteSpace(request.Nit) ? "900000000" : request.Nit.Trim(),
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
                    CreatedAt = now,
                    IsPublished = false
                }
            }
        };

        _db.Templates.Add(entity);
        await _db.SaveChangesAsync(ct);
        return MapTemplate(entity);
    }

    public async Task<TemplateVersionDto> SaveDraftAsync(
        Guid id,
        SaveDraftRequest request,
        CancellationToken ct = default)
    {
        var template = await _db.Templates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new KeyNotFoundException("No encontramos esa plantilla.");

        var current = template.Versions
            .OrderByDescending(v => v.VersionNumber)
            .First();

        var now = DateTimeOffset.UtcNow;
        current.Html = request.Html;
        current.Css = request.Css;
        current.SchemaJson = request.SchemaJson;
        current.SampleDataJson = request.SampleDataJson;
        current.BlocksJson = request.BlocksJson;
        if (request.PageJson is not null)
            current.PageJson = request.PageJson;
        current.CreatedAt = now;
        current.IsPublished = false;

        template.Status = "draft";
        template.UpdatedAt = now;
        if (request.SectorSalud is bool sector)
            template.SectorSalud = sector;
        if (!string.IsNullOrWhiteSpace(request.Nit))
            template.Nit = request.Nit.Trim();

        await _db.SaveChangesAsync(ct);
        return MapVersion(current);
    }

    public async Task<TemplateVersionDto> PublishAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _db.Templates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new KeyNotFoundException("No encontramos esa plantilla.");

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
                CreatedAt = now,
                IsPublished = true
            };
            foreach (var version in template.Versions)
                version.IsPublished = false;
            template.Versions.Add(next);
            template.CurrentVersionNumber = next.VersionNumber;
            template.Status = "published";
            template.UpdatedAt = now;
            await _db.SaveChangesAsync(ct);
            return MapVersion(next);
        }

        foreach (var version in template.Versions)
            version.IsPublished = false;
        current.IsPublished = true;
        current.CreatedAt = now;
        template.Status = "published";
        template.CurrentVersionNumber = current.VersionNumber;
        template.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
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
            v.IsPublished);
}

public sealed record TemplateDto(
    Guid Id,
    string Name,
    string DocumentType,
    string Status,
    int CurrentVersionNumber,
    DateTimeOffset UpdatedAt,
    string Nit,
    bool SectorSalud);

public sealed record TemplateVersionDto(
    Guid Id,
    Guid TemplateId,
    int VersionNumber,
    string Html,
    string Css,
    string SchemaJson,
    string SampleDataJson,
    string BlocksJson,
    DateTimeOffset CreatedAt,
    bool IsPublished);

public sealed record TemplateBundleDto(TemplateDto Template, IReadOnlyList<TemplateVersionDto> Versions);

public sealed record CreateTemplateRequest(
    string Name,
    string DocumentType,
    string? Nit = null,
    bool SectorSalud = false,
    string? Html = null,
    string? Css = null,
    string? SchemaJson = null,
    string? SampleDataJson = null,
    string? BlocksJson = null,
    string? PageJson = null);

public sealed record SaveDraftRequest(
    string Html,
    string Css,
    string SchemaJson,
    string SampleDataJson,
    string BlocksJson,
    string? PageJson = null,
    string? Nit = null,
    bool? SectorSalud = null);
