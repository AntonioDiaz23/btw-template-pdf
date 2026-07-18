using Btw.TemplatePdf.Application.Common;
using Btw.TemplatePdf.Domain.Abstractions;
using Btw.TemplatePdf.Application;
using FluentValidation;

namespace Btw.TemplatePdf.Application.Ubl;

public sealed record GetUblByCufeRequest(
    string Cufe,
    string? Nit = null,
    string TypeDocument = "UBL");

public sealed record GetUblByCufeResponse(
    string Cufe,
    string? Nit,
    string Environment,
    string TypeDocument,
    string UblXml,
    bool FeConfigured);

public interface IFeDianSettings
{
    string Environment { get; }
    bool IsConfigured { get; }
}

public sealed class GetUblByCufeUseCase
{
    private readonly IUblStore _ublStore;
    private readonly IFeDianSettings _feSettings;
    private readonly IValidator<GetUblByCufeRequest> _validator;

    public GetUblByCufeUseCase(
        IUblStore ublStore,
        IFeDianSettings feSettings,
        IValidator<GetUblByCufeRequest> validator)
    {
        _ublStore = ublStore;
        _feSettings = feSettings;
        _validator = validator;
    }

    public async Task<GetUblByCufeResponse> ExecuteAsync(
        GetUblByCufeRequest request,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAppAsync(request, cancellationToken).ConfigureAwait(false);

        var cufe = request.Cufe.Trim();
        var nit = string.IsNullOrWhiteSpace(request.Nit) ? "900000000" : request.Nit.Trim();
        var typeDocument = string.IsNullOrWhiteSpace(request.TypeDocument)
            ? "UBL"
            : request.TypeDocument.Trim().ToUpperInvariant();

        string? ublXml;
        try
        {
            ublXml = await _ublStore
                .GetUblXmlAsync(nit, cufe, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new AppException(
                AppErrorCodes.DianUpstreamError,
                ex.Message,
                ex);
        }

        if (string.IsNullOrWhiteSpace(ublXml))
        {
            throw new AppException(
                AppErrorCodes.InvoiceNotFound,
                $"No UBL found for CUFE {cufe}.");
        }

        return new GetUblByCufeResponse(
            Cufe: cufe,
            Nit: request.Nit,
            Environment: _feSettings.Environment,
            TypeDocument: typeDocument,
            UblXml: ublXml,
            FeConfigured: _feSettings.IsConfigured);
    }
}
