using Btw.TemplatePdf.Application.Common;
using Btw.TemplatePdf.Domain.Abstractions;
using Btw.TemplatePdf.Domain.Invoices;
using Btw.TemplatePdf.Domain.Templates;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Btw.TemplatePdf.Application.Pdf;

/// <summary>
/// Application use case: load template + UBL, map, render PDF.
/// First render for a CUFE pins the template version so later template edits
/// do not change already-generated invoices (HTML/CSS/logos stay as originally used).
/// Callers may override with <see cref="GeneratePdfByCufeRequest.TemplateId"/> and
/// optionally replace the pin via <see cref="GeneratePdfByCufeRequest.ReplaceBinding"/>.
/// </summary>
public sealed class GeneratePdfByCufeUseCase
{
    private readonly ITemplateStore _templates;
    private readonly IInvoiceTemplateBindingStore _bindings;
    private readonly IUblStore _ublStore;
    private readonly IUblToViewModelMapper _mapper;
    private readonly IAssetStore _assets;
    private readonly IPdfRenderer _renderer;
    private readonly IValidator<GeneratePdfByCufeRequest> _validator;
    private readonly ILogger<GeneratePdfByCufeUseCase> _logger;

    public GeneratePdfByCufeUseCase(
        ITemplateStore templates,
        IInvoiceTemplateBindingStore bindings,
        IUblStore ublStore,
        IUblToViewModelMapper mapper,
        IAssetStore assets,
        IPdfRenderer renderer,
        IValidator<GeneratePdfByCufeRequest> validator,
        ILogger<GeneratePdfByCufeUseCase> logger)
    {
        _templates = templates;
        _bindings = bindings;
        _ublStore = ublStore;
        _mapper = mapper;
        _assets = assets;
        _renderer = renderer;
        _validator = validator;
        _logger = logger;
    }

    public async Task<GeneratePdfByCufeResponse> ExecuteAsync(
        GeneratePdfByCufeRequest request,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAppAsync(request, cancellationToken).ConfigureAwait(false);

        var nit = NormalizeNit(request.Nit);
        var cufe = request.Cufe.Trim();

        if (string.IsNullOrWhiteSpace(nit))
            throw new PdfGenerationException(AppErrorCodes.ValidationError, "nit is required.");

        var bindingTask = _bindings.FindAsync(nit, cufe, cancellationToken);
        var ublTask = _ublStore.GetUblXmlAsync(nit, cufe, cancellationToken);
        await Task.WhenAll(bindingTask, ublTask).ConfigureAwait(false);

        var binding = await bindingTask.ConfigureAwait(false);
        var hasPin = binding is not null;
        var overrideTemplate = request.TemplateId is { } tid && tid != Guid.Empty;

        TemplateDefinition template;
        var reusedPinned = false;

        if (overrideTemplate)
        {
            var chosen = await _templates
                .GetPublishedByIdAsync(request.TemplateId!.Value, cancellationToken)
                .ConfigureAwait(false);
            if (chosen is null)
            {
                throw new PdfGenerationException(
                    AppErrorCodes.TemplateNotFound,
                    $"No published version found for template {request.TemplateId}.");
            }

            template = chosen;
        }
        else if (binding is not null)
        {
            var pinnedTemplate = await _templates
                .GetByVersionAsync(binding.TemplateId, binding.TemplateVersionNumber, cancellationToken)
                .ConfigureAwait(false);
            if (pinnedTemplate is null)
            {
                throw new PdfGenerationException(
                    AppErrorCodes.TemplateNotFound,
                    $"Pinned template {binding.TemplateId} v{binding.TemplateVersionNumber} was not found for CUFE {cufe}.");
            }

            template = pinnedTemplate;
            reusedPinned = true;
        }
        else
        {
            var published = await _templates
                .GetPublishedAsync(nit, request.DocumentType, cancellationToken)
                .ConfigureAwait(false);
            if (published is null)
            {
                throw new PdfGenerationException(
                    AppErrorCodes.TemplateNotFound,
                    $"No published {request.DocumentType} template for NIT {nit}.");
            }

            template = published;
        }

        var ublXml = await ublTask.ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(ublXml))
        {
            throw new PdfGenerationException(
                AppErrorCodes.InvoiceNotFound,
                $"No UBL found for NIT {nit} and CUFE {cufe}.");
        }

        InvoiceViewModel invoice;
        try
        {
            invoice = _mapper.Map(nit, cufe, ublXml);
        }
        catch (Exception ex)
        {
            throw new PdfGenerationException(
                AppErrorCodes.MappingError,
                $"UBL could not be mapped to the invoice view model: {ex.Message}");
        }

        var assetBytes = await _assets.ResolveAsync(template.Assets, cancellationToken)
            .ConfigureAwait(false);

        byte[] pdfBytes;
        try
        {
            pdfBytes = await _renderer
                .RenderAsync(template, invoice, assetBytes, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new PdfGenerationException(
                AppErrorCodes.RenderError,
                $"PDF render failed: {ex.Message}");
        }

        var bindingReplaced = false;
        if (!hasPin)
        {
            await _bindings.SaveAsync(
                    new InvoiceTemplateBinding(
                        nit,
                        cufe,
                        request.DocumentType,
                        template.TemplateId,
                        template.Version,
                        DateTimeOffset.UtcNow),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (overrideTemplate && request.ReplaceBinding)
        {
            await _bindings.ReplaceAsync(
                    new InvoiceTemplateBinding(
                        nit,
                        cufe,
                        request.DocumentType,
                        template.TemplateId,
                        template.Version,
                        DateTimeOffset.UtcNow),
                    cancellationToken)
                .ConfigureAwait(false);
            bindingReplaced = true;
        }

        _logger.LogInformation(
            "PDF by CUFE resolved. Nit={Nit} Cufe={Cufe} TemplateId={TemplateId} TemplateVersion={TemplateVersion} ReusedPinnedTemplate={ReusedPinned} BindingReplaced={BindingReplaced}",
            nit,
            cufe,
            template.TemplateId,
            template.Version,
            reusedPinned,
            bindingReplaced);

        var shortCufe = cufe.Length <= 8 ? cufe : cufe[..8];
        return new GeneratePdfByCufeResponse(
            Nit: nit,
            Cufe: cufe,
            DocumentType: request.DocumentType,
            TemplateId: template.TemplateId,
            TemplateVersion: template.Version,
            ContentType: "application/pdf",
            FileName: $"FE-{nit}-{shortCufe}.pdf",
            PdfBase64: Convert.ToBase64String(pdfBytes),
            ReusedPinnedTemplate: reusedPinned,
            BindingReplaced: bindingReplaced);
    }

    private static string NormalizeNit(string? nit)
    {
        if (string.IsNullOrWhiteSpace(nit)) return string.Empty;
        return new string(nit.Where(char.IsDigit).ToArray());
    }
}
