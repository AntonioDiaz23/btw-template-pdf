using Btw.TemplatePdf.Application.Templates;
using Btw.TemplatePdf.Infrastructure.Assets;
using Btw.TemplatePdf.Infrastructure.Persistence;

namespace Btw.TemplatePdf.Infrastructure.Templates;

public sealed partial class PostgresTemplateCatalog
{
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
        var currentNumber = hasDraft
            ? tip!.VersionNumber
            : published?.VersionNumber ?? tip?.VersionNumber ?? t.CurrentVersionNumber;

        var status = TemplateStatuses.IsArchived(t.Status)
            ? TemplateStatuses.Archived
            : published is not null
                ? TemplateStatuses.Published
                : t.Versions.Any(v => VersionStatuses.IsUsed(v.Status))
                    ? TemplateStatuses.Used
                    : TemplateStatuses.Draft;

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
