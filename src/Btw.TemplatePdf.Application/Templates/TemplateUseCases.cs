using Btw.TemplatePdf.Application.Common;
using FluentValidation;
using Btw.TemplatePdf.Application;

namespace Btw.TemplatePdf.Application.Templates;

public sealed class ListTemplatesUseCase
{
    private readonly ITemplateCatalog _catalog;

    public ListTemplatesUseCase(ITemplateCatalog catalog) => _catalog = catalog;

    public Task<IReadOnlyList<TemplateDto>> ExecuteAsync(
        string nit,
        CancellationToken cancellationToken = default)
    {
        var normalized = RequireNit(nit);
        return _catalog.ListAsync(normalized, cancellationToken);
    }

    private static string RequireNit(string? nit)
    {
        var normalized = NitNormalizer.Normalize(nit);
        if (string.IsNullOrEmpty(normalized))
        {
            throw new AppException(AppErrorCodes.ValidationError, "nit is required.");
        }

        return normalized;
    }
}

public sealed class GetTemplateUseCase
{
    private readonly ITemplateCatalog _catalog;

    public GetTemplateUseCase(ITemplateCatalog catalog) => _catalog = catalog;

    public async Task<TemplateBundleDto> ExecuteAsync(
        Guid id,
        string nit,
        CancellationToken cancellationToken = default)
    {
        var normalized = NitNormalizer.Normalize(nit);
        if (string.IsNullOrEmpty(normalized))
        {
            throw new AppException(AppErrorCodes.ValidationError, "nit is required.");
        }

        var bundle = await _catalog.GetBundleAsync(id, cancellationToken).ConfigureAwait(false);
        if (bundle is null || !NitNormalizer.Matches(bundle.Template.Nit, normalized))
        {
            throw new AppException(
                AppErrorCodes.TemplateNotFound,
                $"Template '{id}' was not found.");
        }

        return bundle;
    }
}

public sealed class CreateTemplateUseCase
{
    private readonly ITemplateCatalog _catalog;
    private readonly IValidator<CreateTemplateRequest> _validator;

    public CreateTemplateUseCase(ITemplateCatalog catalog, IValidator<CreateTemplateRequest> validator)
    {
        _catalog = catalog;
        _validator = validator;
    }

    public async Task<TemplateDto> ExecuteAsync(
        CreateTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAppAsync(request, cancellationToken).ConfigureAwait(false);
        var nit = NitNormalizer.Normalize(request.Nit);
        if (string.IsNullOrEmpty(nit))
        {
            throw new AppException(AppErrorCodes.ValidationError, "nit is required.");
        }

        return await _catalog
            .CreateAsync(request with { Nit = nit }, cancellationToken)
            .ConfigureAwait(false);
    }
}

public sealed class SaveDraftUseCase
{
    private readonly ITemplateCatalog _catalog;
    private readonly IValidator<SaveDraftRequest> _validator;

    public SaveDraftUseCase(ITemplateCatalog catalog, IValidator<SaveDraftRequest> validator)
    {
        _catalog = catalog;
        _validator = validator;
    }

    public async Task<TemplateVersionDto> ExecuteAsync(
        Guid id,
        SaveDraftRequest request,
        string nit,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAppAsync(request, cancellationToken).ConfigureAwait(false);

        var normalized = NitNormalizer.Normalize(nit);
        if (string.IsNullOrEmpty(normalized))
        {
            throw new AppException(AppErrorCodes.ValidationError, "nit is required.");
        }

        var bundle = await _catalog.GetBundleAsync(id, cancellationToken).ConfigureAwait(false);
        if (bundle is null || !NitNormalizer.Matches(bundle.Template.Nit, normalized))
        {
            throw new AppException(
                AppErrorCodes.TemplateNotFound,
                $"Template '{id}' was not found.");
        }

        var status = NormalizeStatus(request.Status);
        var draftRequest = string.IsNullOrWhiteSpace(request.Nit)
            ? request
            : request with { Nit = NitNormalizer.Normalize(request.Nit) };

        try
        {
            if (status == "published")
            {
                return await _catalog.PublishAsync(id, cancellationToken).ConfigureAwait(false);
            }

            return await _catalog.SaveDraftAsync(id, draftRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (KeyNotFoundException)
        {
            throw new AppException(
                AppErrorCodes.TemplateNotFound,
                $"Template '{id}' was not found.");
        }
        catch (InvalidOperationException ex)
        {
            throw new AppException(AppErrorCodes.ValidationError, ex.Message);
        }
    }

    internal static string NormalizeStatus(string? status)
    {
        var value = string.IsNullOrWhiteSpace(status) ? "draft" : status.Trim().ToLowerInvariant();
        return value is "draft" or "published"
            ? value
            : throw new AppException(
                AppErrorCodes.ValidationError,
                "status must be 'draft' or 'published'.");
    }
}

public sealed class DeleteDraftUseCase
{
    private readonly ITemplateCatalog _catalog;

    public DeleteDraftUseCase(ITemplateCatalog catalog) => _catalog = catalog;

    public async Task ExecuteAsync(
        Guid id,
        string nit,
        CancellationToken cancellationToken = default)
    {
        var normalized = NitNormalizer.Normalize(nit);
        if (string.IsNullOrEmpty(normalized))
        {
            throw new AppException(AppErrorCodes.ValidationError, "nit is required.");
        }

        var bundle = await _catalog.GetBundleAsync(id, cancellationToken).ConfigureAwait(false);
        if (bundle is null || !NitNormalizer.Matches(bundle.Template.Nit, normalized))
        {
            throw new AppException(
                AppErrorCodes.TemplateNotFound,
                $"Template '{id}' was not found.");
        }

        try
        {
            await _catalog.DeleteDraftAsync(id, cancellationToken).ConfigureAwait(false);
        }
        catch (KeyNotFoundException)
        {
            throw new AppException(
                AppErrorCodes.TemplateNotFound,
                $"Template '{id}' was not found.");
        }
        catch (InvalidOperationException ex)
        {
            throw new AppException(AppErrorCodes.ValidationError, ex.Message);
        }
    }
}

public sealed class ArchiveTemplateUseCase
{
    private readonly ITemplateCatalog _catalog;

    public ArchiveTemplateUseCase(ITemplateCatalog catalog) => _catalog = catalog;

    public async Task ExecuteAsync(
        Guid id,
        string nit,
        CancellationToken cancellationToken = default)
    {
        var normalized = NitNormalizer.Normalize(nit);
        if (string.IsNullOrEmpty(normalized))
        {
            throw new AppException(AppErrorCodes.ValidationError, "nit is required.");
        }

        var bundle = await _catalog.GetBundleAsync(id, cancellationToken).ConfigureAwait(false);
        if (bundle is null || !NitNormalizer.Matches(bundle.Template.Nit, normalized))
        {
            throw new AppException(
                AppErrorCodes.TemplateNotFound,
                $"Template '{id}' was not found.");
        }

        try
        {
            await _catalog.ArchiveAsync(id, cancellationToken).ConfigureAwait(false);
        }
        catch (KeyNotFoundException)
        {
            throw new AppException(
                AppErrorCodes.TemplateNotFound,
                $"Template '{id}' was not found.");
        }
        catch (InvalidOperationException ex)
        {
            throw new AppException(AppErrorCodes.ValidationError, ex.Message);
        }
    }
}

public sealed class DeleteTemplateUseCase
{
    private readonly ITemplateCatalog _catalog;

    public DeleteTemplateUseCase(ITemplateCatalog catalog) => _catalog = catalog;

    public async Task ExecuteAsync(
        Guid id,
        string nit,
        CancellationToken cancellationToken = default)
    {
        var normalized = NitNormalizer.Normalize(nit);
        if (string.IsNullOrEmpty(normalized))
        {
            throw new AppException(AppErrorCodes.ValidationError, "nit is required.");
        }

        var bundle = await _catalog.GetBundleAsync(id, cancellationToken).ConfigureAwait(false);
        if (bundle is null || !NitNormalizer.Matches(bundle.Template.Nit, normalized))
        {
            throw new AppException(
                AppErrorCodes.TemplateNotFound,
                $"Template '{id}' was not found.");
        }

        try
        {
            await _catalog.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        }
        catch (KeyNotFoundException)
        {
            throw new AppException(
                AppErrorCodes.TemplateNotFound,
                $"Template '{id}' was not found.");
        }
        catch (InvalidOperationException ex)
        {
            // Published / bound templates must be archived — surface as 409.
            throw new AppException(AppErrorCodes.Conflict, ex.Message);
        }
    }
}

public sealed class RollbackTemplateVersionUseCase
{
    private readonly ITemplateCatalog _catalog;

    public RollbackTemplateVersionUseCase(ITemplateCatalog catalog) => _catalog = catalog;

    public async Task<TemplateVersionDto> ExecuteAsync(
        Guid id,
        int versionNumber,
        string nit,
        CancellationToken cancellationToken = default)
    {
        var normalized = NitNormalizer.Normalize(nit);
        if (string.IsNullOrEmpty(normalized))
        {
            throw new AppException(AppErrorCodes.ValidationError, "nit is required.");
        }

        var bundle = await _catalog.GetBundleAsync(id, cancellationToken).ConfigureAwait(false);
        if (bundle is null || !NitNormalizer.Matches(bundle.Template.Nit, normalized))
        {
            throw new AppException(
                AppErrorCodes.TemplateNotFound,
                $"Template '{id}' was not found.");
        }

        try
        {
            return await _catalog
                .RollbackToVersionAsync(id, versionNumber, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (KeyNotFoundException)
        {
            throw new AppException(
                AppErrorCodes.TemplateNotFound,
                $"Template '{id}' was not found.");
        }
        catch (InvalidOperationException ex)
        {
            throw new AppException(AppErrorCodes.ValidationError, ex.Message);
        }
    }
}
