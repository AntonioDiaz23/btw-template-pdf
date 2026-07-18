namespace Btw.TemplatePdf.Application.Templates;

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

/// <summary>Persistence port for studio template catalog operations.</summary>
public interface ITemplateCatalog
{
    Task<IReadOnlyList<TemplateDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<TemplateBundleDto?> GetBundleAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TemplateDto> CreateAsync(CreateTemplateRequest request, CancellationToken cancellationToken = default);

    Task<TemplateVersionDto> SaveDraftAsync(
        Guid id,
        SaveDraftRequest request,
        CancellationToken cancellationToken = default);

    Task<TemplateVersionDto> PublishAsync(Guid id, CancellationToken cancellationToken = default);
}
