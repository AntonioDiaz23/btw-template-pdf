namespace Btw.TemplatePdf.Application.Templates;

public sealed record TemplateDto(
    Guid Id,
    string Name,
    string DocumentType,
    string Status,
    int CurrentVersionNumber,
    DateTimeOffset UpdatedAt,
    string Nit,
    bool SectorSalud,
    int PublishedVersionNumber = 0,
    bool HasDraft = false);

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
    bool IsPublished,
    string AssetsJson = "[]",
    string Status = "draft");

public sealed record TemplateBundleDto(TemplateDto Template, IReadOnlyList<TemplateVersionDto> Versions);

public sealed record CreateTemplateRequest(
    string Name,
    string DocumentType,
    string Nit,
    bool SectorSalud = false,
    string? Html = null,
    string? Css = null,
    string? SchemaJson = null,
    string? SampleDataJson = null,
    string? BlocksJson = null,
    string? PageJson = null,
    string? AssetsJson = null);

/// <summary>
/// Upsert draft content, or publish current tip when <see cref="Status"/> is <c>published</c>.
/// When publishing, content fields are optional (republish existing tip).
/// </summary>
public sealed record SaveDraftRequest(
    string? Status = "draft",
    string? Html = null,
    string? Css = null,
    string? SchemaJson = null,
    string? SampleDataJson = null,
    string? BlocksJson = null,
    string? PageJson = null,
    string? Nit = null,
    bool? SectorSalud = null,
    string? AssetsJson = null);

/// <summary>Persistence port for studio template catalog operations.</summary>
public interface ITemplateCatalog
{
    /// <param name="nit">Normalized company NIT (digits only). Required for multi-tenant scoping.</param>
    Task<IReadOnlyList<TemplateDto>> ListAsync(string nit, CancellationToken cancellationToken = default);

    Task<TemplateBundleDto?> GetBundleAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TemplateDto> CreateAsync(CreateTemplateRequest request, CancellationToken cancellationToken = default);

    Task<TemplateVersionDto> SaveDraftAsync(
        Guid id,
        SaveDraftRequest request,
        CancellationToken cancellationToken = default);

    Task<TemplateVersionDto> PublishAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeleteDraftAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-archive: hide from catalog and live PDF lookup, keep all versions for pinned CUFEs.
    /// </summary>
    Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hard-delete only when the template was never published/used and has no invoice bindings.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes an existing <c>used</c> version the live published one again (no new version number).
    /// Discards a tip draft if present.
    /// </summary>
    Task<TemplateVersionDto> RollbackToVersionAsync(
        Guid id,
        int versionNumber,
        CancellationToken cancellationToken = default);
}
