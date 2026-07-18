using Btw.TemplatePdf.Application.Common;
using Btw.TemplatePdf.Domain.Abstractions;
using Btw.TemplatePdf.Domain.Invoices;
using Btw.TemplatePdf.Domain.Templates;
using FluentValidation;

namespace Btw.TemplatePdf.Application.Pdf;

/// <summary>
/// Application use case: load template + UBL, map, render PDF.
/// First render for a CUFE pins the template version so later template edits
/// do not change already-generated invoices (HTML/CSS/logos stay as originally used).
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

    public GeneratePdfByCufeUseCase(
        ITemplateStore templates,
        IInvoiceTemplateBindingStore bindings,
        IUblStore ublStore,
        IUblToViewModelMapper mapper,
        IAssetStore assets,
        IPdfRenderer renderer,
        IValidator<GeneratePdfByCufeRequest> validator)
    {
        _templates = templates;
        _bindings = bindings;
        _ublStore = ublStore;
        _mapper = mapper;
        _assets = assets;
        _renderer = renderer;
        _validator = validator;
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
        var pinned = binding is not null;

        TemplateDefinition template;
        if (binding is not null)
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

        if (!pinned)
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
            ReusedPinnedTemplate: pinned);
    }

    private static string NormalizeNit(string? nit)
    {
        if (string.IsNullOrWhiteSpace(nit)) return string.Empty;
        return new string(nit.Where(char.IsDigit).ToArray());
    }
}
