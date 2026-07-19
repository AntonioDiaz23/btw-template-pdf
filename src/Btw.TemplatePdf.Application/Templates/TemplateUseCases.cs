using Btw.TemplatePdf.Application.Common;
using FluentValidation;
using Btw.TemplatePdf.Application;

namespace Btw.TemplatePdf.Application.Templates;

public sealed class ListTemplatesUseCase
{
    private readonly ITemplateCatalog _catalog;

    public ListTemplatesUseCase(ITemplateCatalog catalog) => _catalog = catalog;

    public Task<IReadOnlyList<TemplateDto>> ExecuteAsync(CancellationToken cancellationToken = default) =>
        _catalog.ListAsync(cancellationToken);
}

public sealed class GetTemplateUseCase
{
    private readonly ITemplateCatalog _catalog;

    public GetTemplateUseCase(ITemplateCatalog catalog) => _catalog = catalog;

    public async Task<TemplateBundleDto> ExecuteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var bundle = await _catalog.GetBundleAsync(id, cancellationToken).ConfigureAwait(false);
        if (bundle is null)
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
        return await _catalog.CreateAsync(request, cancellationToken).ConfigureAwait(false);
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
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAppAsync(request, cancellationToken).ConfigureAwait(false);

        var status = NormalizeStatus(request.Status);
        try
        {
            if (status == "published")
            {
                return await _catalog.PublishAsync(id, cancellationToken).ConfigureAwait(false);
            }

            return await _catalog.SaveDraftAsync(id, request, cancellationToken).ConfigureAwait(false);
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

    public async Task ExecuteAsync(Guid id, CancellationToken cancellationToken = default)
    {
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

public sealed class RollbackTemplateVersionUseCase
{
    private readonly ITemplateCatalog _catalog;

    public RollbackTemplateVersionUseCase(ITemplateCatalog catalog) => _catalog = catalog;

    public async Task<TemplateVersionDto> ExecuteAsync(
        Guid id,
        int versionNumber,
        CancellationToken cancellationToken = default)
    {
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
