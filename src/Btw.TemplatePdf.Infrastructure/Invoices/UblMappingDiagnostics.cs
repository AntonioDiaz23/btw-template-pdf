using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Btw.TemplatePdf.Infrastructure.Invoices;

/// <summary>
/// Diagnostic helpers to distinguish "missing in UBL" vs "mapped empty".
/// </summary>
internal static class UblMappingDiagnostics
{
    private static readonly string[] CatalogPaths =
    [
        "documento.tipo",
        "documento.numero",
        "documento.prefijo",
        "documento.autorizacion",
        "documento.rangoDesde",
        "documento.rangoHasta",
        "documento.vigenciaInicio",
        "documento.vigenciaFin",
        "documento.fechaGeneracion",
        "documento.horaGeneracion",
        "documento.fechaVencimiento",
        "documento.moneda",
        "documento.cufe",
        "documento.qrUrl",
        "emisor.razonSocial",
        "emisor.nit",
        "emisor.dv",
        "emisor.telefono",
        "emisor.direccion",
        "emisor.ciudad",
        "emisor.departamento",
        "emisor.email",
        "cliente.nombre",
        "cliente.nit",
        "cliente.telefono",
        "cliente.direccion",
        "cliente.ciudad",
        "cliente.departamento",
        "cliente.email",
        "cliente.pais",
        "factura.fecha",
        "factura.fechaVencimiento",
        "factura.formaPago",
        "factura.medioPago",
        "factura.nroPedido",
        "pago.forma",
        "pago.medio",
        "pago.fechaVencimiento",
        "observaciones",
        "totales.subtotal",
        "totales.iva",
        "totales.total",
        "totales.descuento",
        "totales.totalItems",
        "software.nombre",
        "software.fabricante",
        "software.fabricanteNit"
    ];

    private static readonly string[] InterestingUblLocalNames =
    [
        "ID", "UUID", "IssueDate", "IssueTime", "DueDate", "InvoiceTypeCode",
        "DocumentCurrencyCode", "RegistrationName", "CompanyID", "Telephone",
        "ElectronicMail", "Line", "CityName", "CountrySubentity", "Description",
        "InvoicedQuantity", "PriceAmount", "LineExtensionAmount", "TaxAmount",
        "PayableAmount", "TaxExclusiveAmount", "PaymentMeansCode", "PaymentDueDate",
        "InvoiceAuthorization", "StartDate", "EndDate", "From", "To",
        "SoftwareName", "ProviderID", "Note", "OrderReference"
    ];

    public static void LogFetchedUbl(
        ILogger logger,
        string nit,
        string cufe,
        string ublXml,
        string source)
    {
        var preview = ublXml.Length <= 800
            ? ublXml
            : ublXml[..800] + "…";

        string rootName = "?";
        var present = new List<string>();
        var missing = new List<string>();
        try
        {
            var doc = XDocument.Parse(ublXml, LoadOptions.PreserveWhitespace);
            rootName = doc.Root?.Name.LocalName ?? "?";
            var localNames = new HashSet<string>(
                doc.Descendants().Select(e => e.Name.LocalName),
                StringComparer.OrdinalIgnoreCase);

            foreach (var name in InterestingUblLocalNames)
            {
                if (localNames.Contains(name))
                    present.Add(name);
                else
                    missing.Add(name);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "UBL diagnostics: XML parse failed for CUFE {Cufe}", cufe);
        }

        logger.LogInformation(
            "UBL fetched source={Source} nit={Nit} cufe={Cufe} root={Root} length={Length} " +
            "ublTagsPresent=[{Present}] ublTagsMissing=[{Missing}]",
            source,
            nit,
            cufe,
            rootName,
            ublXml.Length,
            string.Join(", ", present),
            string.Join(", ", missing));

        logger.LogDebug("UBL preview (first 800 chars) for CUFE {Cufe}: {Preview}", cufe, preview);
    }

    public static void LogMappedViewModel(
        ILogger logger,
        string nit,
        string cufe,
        IReadOnlyDictionary<string, object?> data)
    {
        using var jsonDoc = JsonDocument.Parse(JsonSerializer.Serialize(data));
        var root = jsonDoc.RootElement;

        var filled = new List<string>();
        var empty = new List<string>();
        var samples = new StringBuilder();

        foreach (var path in CatalogPaths)
        {
            if (!TryGetPath(root, path, out var value) || IsEmpty(value))
            {
                empty.Add(path);
                continue;
            }

            filled.Add(path);
            var text = Truncate(ValueToString(value), 80);
            samples.Append(path).Append('=').Append(text).Append("; ");
        }

        var itemCount = 0;
        if (TryGetPath(root, "items", out var items) && items.ValueKind == JsonValueKind.Array)
            itemCount = items.GetArrayLength();

        logger.LogInformation(
            "UBL map result nit={Nit} cufe={Cufe} items={ItemCount} " +
            "filled={FilledCount} empty={EmptyCount} filledPaths=[{Filled}] emptyPaths=[{Empty}]",
            nit,
            cufe,
            itemCount,
            filled.Count,
            empty.Count,
            string.Join(", ", filled),
            string.Join(", ", empty));

        logger.LogInformation(
            "UBL map samples nit={Nit} cufe={Cufe}: {Samples}",
            nit,
            cufe,
            samples.Length == 0 ? "(none)" : samples.ToString());
    }

    private static bool TryGetPath(JsonElement root, string path, out JsonElement value)
    {
        value = root;
        foreach (var key in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(key, out value))
            {
                value = default;
                return false;
            }
        }

        return true;
    }

    private static bool IsEmpty(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => true,
            JsonValueKind.String => string.IsNullOrWhiteSpace(value.GetString()),
            JsonValueKind.Array => value.GetArrayLength() == 0,
            JsonValueKind.Object => !value.EnumerateObject().Any(),
            _ => false
        };

    private static string ValueToString(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Array => $"[{value.GetArrayLength()} items]",
            _ => value.ToString()
        };

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
