using Btw.TemplatePdf.Domain.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Btw.TemplatePdf.Infrastructure.Invoices;

/// <summary>
/// Resolves UBL XML by CUFE via FE GetDocumentFromDian, with optional in-memory demo fallback.
/// </summary>
public sealed class FeDianUblStore : IUblStore
{
    private readonly FeDianDocumentClient _client;
    private readonly InMemoryUblStore _stub;
    private readonly FeDianOptions _options;
    private readonly UblDiagnosticsWriter _diagnostics;
    private readonly ILogger<FeDianUblStore> _logger;

    public FeDianUblStore(
        FeDianDocumentClient client,
        InMemoryUblStore stub,
        IOptions<FeDianOptions> options,
        UblDiagnosticsWriter diagnostics,
        ILogger<FeDianUblStore> logger)
    {
        _client = client;
        _stub = stub;
        _options = options.Value;
        _diagnostics = diagnostics;
        _logger = logger;
    }

    public async Task<string?> GetUblXmlAsync(
        string nit,
        string cufe,
        CancellationToken cancellationToken = default)
    {
        UblDiagnosticsAmbient.CurrentReportPath = null;

        if (_client.IsConfigured)
        {
            try
            {
                _logger.LogInformation(
                    "Consulting GetDocumentFromDian for UBL nit={Nit} cufe={Cufe} baseUrl={BaseUrl} env={Env}",
                    nit,
                    cufe,
                    _options.BaseUrl,
                    _options.Environment);

                var ubl = await _client
                    .GetUblXmlAsync(cufe, "UBL", cancellationToken)
                    .ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(ubl))
                {
                    var path = _diagnostics.WriteFetchReport(nit, cufe, ubl, source: "GetDocumentFromDian");
                    UblDiagnosticsAmbient.CurrentReportPath = path;
                    UblMappingDiagnostics.LogFetchedUbl(_logger, nit, cufe, ubl, source: "GetDocumentFromDian");
                    return ubl;
                }

                _logger.LogWarning(
                    "No UBL from GetDocumentFromDian for CUFE {Cufe} (NIT {Nit})",
                    cufe,
                    nit);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error calling GetDocumentFromDian for CUFE {Cufe}",
                    cufe);
                if (!_options.AllowStubFallback)
                    throw;
            }
        }
        else
        {
            _logger.LogWarning("FeDian:BaseUrl not set; using stub UBL store if allowed.");
        }

        if (!_options.AllowStubFallback)
            return null;

        var stub = await _stub.GetUblXmlAsync(nit, cufe, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(stub))
        {
            _logger.LogWarning(
                "Using STUB UBL fallback for nit={Nit} cufe={Cufe} — mapped fields will be demo data, not DIAN.",
                nit,
                cufe);
            var path = _diagnostics.WriteFetchReport(nit, cufe, stub, source: "StubFallback");
            UblDiagnosticsAmbient.CurrentReportPath = path;
            UblMappingDiagnostics.LogFetchedUbl(_logger, nit, cufe, stub, source: "StubFallback");
        }

        return stub;
    }
}
