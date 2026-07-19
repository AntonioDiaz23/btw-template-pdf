using System.Text.Json;
using Btw.TemplatePdf.Domain.Abstractions;
using Btw.TemplatePdf.Domain.Common;
using Btw.TemplatePdf.Domain.Templates;
using Btw.TemplatePdf.Infrastructure.Assets;
using Btw.TemplatePdf.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Btw.TemplatePdf.Infrastructure.Templates;

public sealed class PostgresTemplateStore : ITemplateStore
{
    private readonly TemplateDbContext _db;

    public PostgresTemplateStore(TemplateDbContext db)
    {
        _db = db;
    }

    public async Task<TemplateDefinition?> GetPublishedAsync(
        string nit,
        DocumentType documentType,
        CancellationToken cancellationToken = default)
    {
        var docType = DocumentTypeMapper.ToApi(documentType);
        // Live PDF uses the published version even while a draft tip is being edited.
        var template = await _db.Templates
            .AsNoTracking()
            .Include(t => t.Versions)
            .Where(t => t.Nit == nit
                        && t.DocumentType == docType
                        && t.Versions.Any(v => VersionStatuses.IsPublished(v.Status) || v.IsPublished))
            .OrderByDescending(t => t.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (template is null)
            return null;

        var version = template.Versions
            .Where(v => VersionStatuses.IsPublished(v.Status) || v.IsPublished)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault();

        if (version is null)
            return null;

        return MapDefinition(template, version);
    }

    public async Task<TemplateDefinition?> GetPublishedByIdAsync(
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var template = await _db.Templates
            .AsNoTracking()
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken)
            .ConfigureAwait(false);

        if (template is null)
            return null;

        var version = template.Versions
            .Where(v => VersionStatuses.IsPublished(v.Status) || v.IsPublished)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault();

        return version is null ? null : MapDefinition(template, version);
    }

    public async Task<TemplateDefinition?> GetByVersionAsync(
        Guid templateId,
        int versionNumber,
        CancellationToken cancellationToken = default)
    {
        var template = await _db.Templates
            .AsNoTracking()
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken);

        if (template is null)
            return null;

        var version = template.Versions.FirstOrDefault(v => v.VersionNumber == versionNumber);
        return version is null ? null : MapDefinition(template, version);
    }

    internal static TemplateDefinition MapDefinition(TemplateEntity template, TemplateVersionEntity version)
    {
        PageSettings page;
        try
        {
            page = JsonSerializer.Deserialize<PageSettings>(
                string.IsNullOrWhiteSpace(version.PageJson) ? "{}" : version.PageJson,
                JsonOptions.Default) ?? new PageSettings();
        }
        catch
        {
            page = new PageSettings();
        }

        return new TemplateDefinition
        {
            TemplateId = template.Id,
            Nit = template.Nit,
            DocumentType = DocumentTypeMapper.FromApi(template.DocumentType),
            Version = version.VersionNumber,
            Status = template.Status == "published" ? TemplateStatus.Published : TemplateStatus.Draft,
            Page = page,
            Features = new TemplateFeatures { SectorSalud = template.SectorSalud },
            BlocksJson = version.BlocksJson,
            Html = version.Html,
            Css = version.Css,
            Assets = EmbeddedDataUrlAssetStore.ParseAssetsJson(version.AssetsJson),
            UpdatedAt = template.UpdatedAt
        };
    }
}

internal static class DocumentTypeMapper
{
    public static string ToApi(DocumentType type) => type switch
    {
        DocumentType.NotaCredito => "nota_credito",
        DocumentType.NotaDebito => "nota_debito",
        DocumentType.Otro => "otro",
        _ => "factura"
    };

    public static DocumentType FromApi(string value) =>
        (value ?? "factura").Trim().ToLowerInvariant() switch
        {
            "nota_credito" => DocumentType.NotaCredito,
            "nota_debito" => DocumentType.NotaDebito,
            "otro" => DocumentType.Otro,
            _ => DocumentType.Factura
        };
}

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
