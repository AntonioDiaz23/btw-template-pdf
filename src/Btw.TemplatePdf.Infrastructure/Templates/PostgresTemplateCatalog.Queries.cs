using Btw.TemplatePdf.Application.Templates;
using Btw.TemplatePdf.Infrastructure.Assets;
using Btw.TemplatePdf.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Btw.TemplatePdf.Infrastructure.Templates;

public sealed partial class PostgresTemplateCatalog
{
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
}
