using Btw.TemplatePdf.Application.Common;

namespace Btw.TemplatePdf.Application.BrandAssets;

public sealed class ListBrandAssetsUseCase
{
    private readonly IBrandAssetStore _store;

    public ListBrandAssetsUseCase(IBrandAssetStore store) => _store = store;

    public Task<IReadOnlyList<BrandAssetDto>> ExecuteAsync(
        string? nit,
        CancellationToken cancellationToken = default)
    {
        var normalized = string.IsNullOrWhiteSpace(nit)
            ? null
            : NitNormalizer.Normalize(nit);
        return _store.ListAsync(
            string.IsNullOrEmpty(normalized) ? null : normalized,
            cancellationToken);
    }
}

public sealed class GetBrandAssetContentUseCase
{
    private readonly IBrandAssetStore _store;

    public GetBrandAssetContentUseCase(IBrandAssetStore store) => _store = store;

    public async Task<BrandAssetContent> ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var content = await _store.GetContentAsync(id, cancellationToken).ConfigureAwait(false);
        if (content is null)
        {
            throw new AppException(
                AppErrorCodes.TemplateNotFound,
                $"Brand asset '{id}' was not found.");
        }

        return content;
    }
}

public sealed class UploadBrandAssetUseCase
{
    private readonly IBrandAssetStore _store;

    public UploadBrandAssetUseCase(IBrandAssetStore store) => _store = store;

    public async Task<BrandAssetDto> ExecuteAsync(
        UploadBrandAssetRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Bytes.Length == 0)
            throw new AppException(AppErrorCodes.ValidationError, "file is required.");

        if (!request.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            throw new AppException(AppErrorCodes.ValidationError, "Only image files are allowed.");

        if (request.Bytes.LongLength > BrandAssetLimits.MaxBytes)
        {
            throw new AppException(
                AppErrorCodes.ValidationError,
                "Image exceeds 1.5 MB. Use a smaller file.");
        }

        var nit = NitNormalizer.Normalize(request.Nit);
        if (string.IsNullOrEmpty(nit))
            nit = BrandAssetLimits.DefaultDemoNit;

        var count = await _store.CountByNitAsync(nit, cancellationToken).ConfigureAwait(false);
        if (count >= BrandAssetLimits.MaxAssetsPerNit)
        {
            throw new AppException(
                AppErrorCodes.ValidationError,
                $"Maximum of {BrandAssetLimits.MaxAssetsPerNit} images per company. Delete one to upload another.");
        }

        return await _store
            .CreateAsync(request with { Nit = nit }, cancellationToken)
            .ConfigureAwait(false);
    }
}

public sealed class DeleteBrandAssetUseCase
{
    private readonly IBrandAssetStore _store;

    public DeleteBrandAssetUseCase(IBrandAssetStore store) => _store = store;

    public async Task ExecuteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deleted = await _store.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        if (!deleted)
        {
            throw new AppException(
                AppErrorCodes.TemplateNotFound,
                $"Brand asset '{id}' was not found.");
        }
    }
}
