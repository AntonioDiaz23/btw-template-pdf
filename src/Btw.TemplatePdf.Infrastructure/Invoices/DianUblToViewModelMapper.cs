using System.Globalization;
using System.Xml.Linq;
using Btw.TemplatePdf.Domain.Abstractions;
using Btw.TemplatePdf.Domain.Invoices;

namespace Btw.TemplatePdf.Infrastructure.Invoices;

/// <summary>
/// Maps DIAN UBL 2.1 Invoice XML into the same InvoiceViewModel paths
/// used by the studio sample (<c>documento.*</c>, <c>emisor.*</c>, <c>items[]</c>, …).
/// </summary>
public sealed class DianUblToViewModelMapper : IUblToViewModelMapper
{
    private static readonly XNamespace Cbc =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace Cac =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

    public InvoiceViewModel Map(string nit, string cufe, string ublXml)
    {
        if (string.IsNullOrWhiteSpace(ublXml))
            throw new ArgumentException("UBL XML is empty.", nameof(ublXml));

        var doc = XDocument.Parse(ublXml, LoadOptions.PreserveWhitespace);
        var root = doc.Root
            ?? throw new InvalidOperationException("UBL has no root element.");

        var rootName = root.Name.LocalName;
        if (!rootName.Equals("Invoice", StringComparison.OrdinalIgnoreCase) &&
            !rootName.Equals("CreditNote", StringComparison.OrdinalIgnoreCase) &&
            !rootName.Equals("DebitNote", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"UBL root '{rootName}' is not supported yet (expected Invoice/CreditNote/DebitNote).");
        }

        var uuid = First(Val(root, Cbc + "UUID"), cufe);
        var invoiceId = Val(root, Cbc + "ID") ?? string.Empty;
        var (prefijo, numero) = SplitPrefijoNumero(invoiceId);
        var issueDate = Val(root, Cbc + "IssueDate") ?? string.Empty;
        var issueTime = Val(root, Cbc + "IssueTime") ?? string.Empty;
        var dueDate = First(
            Val(root, Cbc + "DueDate"),
            Val(root.Elements(Cac + "PaymentMeans").FirstOrDefault(), Cbc + "PaymentDueDate"));
        var currency = Val(root, Cbc + "DocumentCurrencyCode") ?? "COP";
        var typeCode = Val(root, Cbc + "InvoiceTypeCode")
                       ?? Val(root, Cbc + "CreditNoteTypeCode")
                       ?? Val(root, Cbc + "DebitNoteTypeCode");

        var supplier = root.Element(Cac + "AccountingSupplierParty")?.Element(Cac + "Party");
        var customer = root.Element(Cac + "AccountingCustomerParty")?.Element(Cac + "Party");
        var paymentMeans = root.Elements(Cac + "PaymentMeans").FirstOrDefault();
        var monetary = root.Element(Cac + "LegalMonetaryTotal");

        var autorizacion = First(
            LocalDescendant(root, "InvoiceAuthorization"),
            Val(root.Elements(Cac + "AdditionalDocumentReference").FirstOrDefault(), Cbc + "ID"));
        var rangoDesde = LocalDescendant(root, "InvoiceAuthorizationPeriod") is { } _
            ? LocalDescendant(root, "StartDate")
            : LocalDescendant(root, "From");
        // Prefer STS AuthorizationProvider / InvoiceControl when present
        rangoDesde = First(
            LocalSiblingAfterAuthorization(root, "From"),
            LocalDescendant(root, "From"),
            rangoDesde);
        var rangoHasta = First(
            LocalSiblingAfterAuthorization(root, "To"),
            LocalDescendant(root, "To"));
        var vigenciaInicio = First(
            LocalDescendant(root, "StartDate"),
            issueDate);
        var vigenciaFin = First(
            LocalDescendant(root, "EndDate"),
            dueDate);

        var lineNodes = root.Elements(Cac + "InvoiceLine")
            .Concat(root.Elements(Cac + "CreditNoteLine"))
            .Concat(root.Elements(Cac + "DebitNoteLine"))
            .ToList();

        var items = new List<object?>();
        var lineIndex = 1;
        foreach (var line in lineNodes)
        {
            var qtyEl = line.Element(Cbc + "InvoicedQuantity")
                        ?? line.Element(Cbc + "CreditedQuantity")
                        ?? line.Element(Cbc + "DebitedQuantity");
            var itemEl = line.Element(Cac + "Item");
            var priceEl = line.Element(Cac + "Price");
            var lineTotal = ParseDecimal(Val(line, Cbc + "LineExtensionAmount")) ?? 0m;
            var qty = ParseDecimal(qtyEl?.Value) ?? 0m;
            var unitPrice = ParseDecimal(Val(priceEl, Cbc + "PriceAmount")) ?? 0m;
            var discount = SumAllowance(line.Elements(Cac + "AllowanceCharge"), charge: false);
            var iva = SumTaxByScheme(line.Elements(Cac + "TaxTotal"), "01");

            items.Add(new Dictionary<string, object?>
            {
                ["linea"] = ParseInt(Val(line, Cbc + "ID")) ?? lineIndex,
                ["codigo"] = First(
                    Val(itemEl?.Element(Cac + "StandardItemIdentification"), Cbc + "ID"),
                    Val(itemEl?.Element(Cac + "SellersItemIdentification"), Cbc + "ID")),
                ["descripcion"] = Val(itemEl, Cbc + "Description") ?? string.Empty,
                ["cantidad"] = qty,
                ["unidad"] = Attr(qtyEl, "unitCode") ?? "NIU",
                ["valorUnitario"] = unitPrice,
                ["descuento"] = discount,
                ["iva"] = iva,
                ["total"] = lineTotal,
                ["valor"] = lineTotal
            });
            lineIndex++;
        }

        var subtotal = ParseDecimal(Val(monetary, Cbc + "TaxExclusiveAmount"))
                       ?? ParseDecimal(Val(monetary, Cbc + "LineExtensionAmount"))
                       ?? items.Sum(i => (decimal)((Dictionary<string, object?>)i!)["total"]!);
        var payable = ParseDecimal(Val(monetary, Cbc + "PayableAmount")) ?? subtotal;
        var taxAmount = root.Elements(Cac + "TaxTotal")
            .Select(t => ParseDecimal(Val(t, Cbc + "TaxAmount")) ?? 0m)
            .DefaultIfEmpty(0m)
            .Sum();
        var ivaTotal = SumTaxByScheme(root.Elements(Cac + "TaxTotal"), "01");
        var incTotal = SumTaxByScheme(root.Elements(Cac + "TaxTotal"), "04");
        var discountTotal = SumAllowance(root.Elements(Cac + "AllowanceCharge"), charge: false);
        var paymentCode = Val(paymentMeans, Cbc + "PaymentMeansCode");
        var paymentId = Val(paymentMeans, Cbc + "ID");
        var formaPago = MapFormaPago(paymentId);
        var medioPago = MapMedioPago(paymentCode);

        var emisor = MapParty(supplier, fallbackNit: nit);
        var cliente = MapCustomer(customer);

        var qrUrl =
            $"https://catalogo-vpfe.dian.gov.co/document/searchqr?documentkey={uuid}";

        var softwareName = LocalDescendant(root, "SoftwareName");
        var softwareProviderEl = root.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "SoftwareProvider");
        var softwareProvider = First(
            LocalDescendant(softwareProviderEl, "ProviderID"),
            LocalDescendant(root, "ProviderID"));
        var softwareProviderName = First(
            LocalDescendant(softwareProviderEl, "RegistrationName"),
            "Bythewave S.A.S.");

        var data = new Dictionary<string, object?>
        {
            ["numero"] = invoiceId,
            ["documento"] = new Dictionary<string, object?>
            {
                ["tipo"] = MapDocumentTypeLabel(rootName, typeCode),
                ["numero"] = string.IsNullOrWhiteSpace(numero) ? invoiceId : numero,
                ["prefijo"] = prefijo,
                ["autorizacion"] = autorizacion ?? string.Empty,
                ["rangoDesde"] = rangoDesde ?? string.Empty,
                ["rangoHasta"] = rangoHasta ?? string.Empty,
                ["vigenciaInicio"] = vigenciaInicio ?? string.Empty,
                ["vigenciaFin"] = vigenciaFin ?? string.Empty,
                ["fechaGeneracion"] = issueDate,
                ["horaGeneracion"] = issueTime,
                ["fechaVencimiento"] = dueDate ?? string.Empty,
                ["moneda"] = currency,
                ["cufe"] = uuid,
                ["qrUrl"] = qrUrl
            },
            ["emisor"] = emisor,
            ["cliente"] = cliente,
            ["factura"] = new Dictionary<string, object?>
            {
                ["fecha"] = issueDate,
                ["fechaVencimiento"] = dueDate ?? string.Empty,
                ["formaPago"] = formaPago,
                ["medioPago"] = medioPago,
                ["nroPedido"] = First(
                    Val(root.Elements(Cac + "OrderReference").FirstOrDefault(), Cbc + "ID"),
                    string.Empty),
                ["lineaNegocio"] = string.Empty,
                ["fechaValidacionDian"] = $"{issueDate} {issueTime}".Trim(),
                ["fechaGeneracionErp"] = $"{issueDate} {issueTime}".Trim()
            },
            ["fiscal"] = new Dictionary<string, object?>
            {
                ["autorizacion"] = autorizacion ?? string.Empty,
                ["prefijo"] = prefijo,
                ["rangoDesde"] = rangoDesde ?? string.Empty,
                ["rangoHasta"] = rangoHasta ?? string.Empty,
                ["resolucion"] = BuildResolucion(autorizacion, prefijo, rangoDesde, rangoHasta),
                ["cufe"] = uuid,
                ["qrUrl"] = qrUrl
            },
            ["pago"] = new Dictionary<string, object?>
            {
                ["forma"] = formaPago,
                ["medio"] = medioPago,
                ["plazo"] = string.Empty,
                ["fechaVencimiento"] = dueDate ?? string.Empty
            },
            ["observaciones"] = string.Join(
                " | ",
                root.Elements(Cbc + "Note").Select(n => n.Value?.Trim()).Where(v => !string.IsNullOrWhiteSpace(v))),
            ["items"] = items.ToArray(),
            ["totales"] = new Dictionary<string, object?>
            {
                ["subtotal1"] = subtotal,
                ["descuento"] = discountTotal,
                ["subtotal2"] = subtotal,
                ["subtotal"] = subtotal,
                ["iva"] = ivaTotal > 0 ? ivaTotal : taxAmount,
                ["ivaTarifa"] = GuessIvaRateLabel(root),
                ["inc"] = incTotal,
                ["impuestoConsumo"] = incTotal,
                ["impuestoConsumoTarifa"] = string.Empty,
                ["otrosImpuestos"] = Math.Max(0, taxAmount - ivaTotal - incTotal),
                ["retenciones"] = 0m,
                ["total"] = payable,
                ["totalItems"] = items.Sum(i =>
                {
                    var map = (Dictionary<string, object?>)i!;
                    return Convert.ToDecimal(map["cantidad"] ?? 0, CultureInfo.InvariantCulture);
                }),
                ["valorEnLetras"] = string.Empty
            },
            ["software"] = new Dictionary<string, object?>
            {
                ["fabricante"] = softwareProviderName ?? "Bythewave S.A.S.",
                ["fabricanteNit"] = softwareProvider ?? "900665411",
                ["nombre"] = softwareName ?? "BTW Facturación Electrónica",
                ["proveedor"] = softwareProviderName ?? "Bythewave S.A.S."
            }
        };

        return new InvoiceViewModel
        {
            Nit = nit,
            Cufe = uuid,
            Data = data
        };
    }

    private static Dictionary<string, object?> MapParty(XElement? party, string fallbackNit)
    {
        var taxScheme = party?.Element(Cac + "PartyTaxScheme");
        var legal = party?.Element(Cac + "PartyLegalEntity");
        var contact = party?.Element(Cac + "Contact");
        var address = party?.Element(Cac + "PhysicalLocation")?.Element(Cac + "Address")
                      ?? party?.Element(Cac + "PostalAddress");

        var companyId = First(
            Val(taxScheme, Cbc + "CompanyID"),
            Val(legal, Cbc + "CompanyID"),
            fallbackNit);
        var name = First(
            Val(taxScheme, Cbc + "RegistrationName"),
            Val(legal, Cbc + "RegistrationName"),
            Val(party?.Element(Cac + "PartyName"), Cbc + "Name"));

        return new Dictionary<string, object?>
        {
            ["razonSocial"] = name ?? string.Empty,
            ["nit"] = companyId ?? string.Empty,
            ["dv"] = Attr(
                taxScheme?.Element(Cbc + "CompanyID") ?? legal?.Element(Cbc + "CompanyID"),
                "schemeID") ?? string.Empty,
            ["telefono"] = Val(contact, Cbc + "Telephone") ?? string.Empty,
            ["direccion"] = First(
                Val(address, Cbc + "Line"),
                Val(address?.Element(Cac + "AddressLine"), Cbc + "Line")) ?? string.Empty,
            ["ciudad"] = Val(address, Cbc + "CityName") ?? string.Empty,
            ["departamento"] = Val(address?.Element(Cbc + "CountrySubentity"), null)
                               ?? Val(address, Cbc + "CountrySubentity")
                               ?? string.Empty,
            ["email"] = Val(contact, Cbc + "ElectronicMail") ?? string.Empty,
            ["responsabilidad"] = string.Empty
        };
    }

    private static Dictionary<string, object?> MapCustomer(XElement? party)
    {
        var mapped = MapParty(party, fallbackNit: string.Empty);
        var taxScheme = party?.Element(Cac + "PartyTaxScheme");
        var schemeName = Attr(taxScheme?.Element(Cbc + "CompanyID"), "schemeName")
                         ?? Attr(taxScheme?.Element(Cbc + "CompanyID"), "schemeID");

        return new Dictionary<string, object?>
        {
            ["nombre"] = mapped["razonSocial"],
            ["tipoDocumento"] = schemeName ?? "NIT",
            ["nit"] = mapped["nit"],
            ["telefono"] = mapped["telefono"],
            ["direccion"] = mapped["direccion"],
            ["ciudad"] = mapped["ciudad"],
            ["departamento"] = mapped["departamento"],
            ["pais"] = MapCountry(
                party?.Element(Cac + "PhysicalLocation")?.Element(Cac + "Address")?.Element(Cac + "Country")
                ?? party?.Element(Cac + "PostalAddress")?.Element(Cac + "Country")),
            ["email"] = mapped["email"]
        };
    }

    private static string MapCountry(XElement? country)
    {
        var code = Val(country, Cbc + "IdentificationCode");
        if (string.Equals(code, "CO", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(code))
            return "COLOMBIA";
        return Val(country, Cbc + "Name") ?? code ?? "COLOMBIA";
    }

    private static (string Prefijo, string Numero) SplitPrefijoNumero(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return (string.Empty, string.Empty);

        var trimmed = id.Trim();
        var i = 0;
        while (i < trimmed.Length && !char.IsDigit(trimmed[i]))
            i++;

        if (i <= 0 || i >= trimmed.Length)
            return (string.Empty, trimmed);

        return (trimmed[..i], trimmed[i..]);
    }

    private static string MapDocumentTypeLabel(string rootName, string? typeCode) =>
        rootName.ToLowerInvariant() switch
        {
            "creditnote" => "NOTA CRÉDITO",
            "debitnote" => "NOTA DÉBITO",
            _ => typeCode switch
            {
                "01" => "FACTURA ELECTRÓNICA DE VENTA",
                "02" => "FACTURA ELECTRÓNICA DE EXPORTACIÓN",
                "03" => "FACTURA ELECTRÓNICA DE CONTINGENCIA",
                _ => "FACTURA ELECTRÓNICA DE VENTA"
            }
        };

    private static string MapFormaPago(string? paymentId) =>
        paymentId switch
        {
            "1" => "Contado",
            "2" => "Crédito",
            _ => string.IsNullOrWhiteSpace(paymentId) ? "Crédito" : paymentId
        };

    private static string MapMedioPago(string? code) =>
        code switch
        {
            "10" => "Efectivo",
            "20" => "Cheque",
            "41" => "Transferencia Débito Bancaria",
            "42" => "Consignación bancaria",
            "47" => "Transferencia",
            "48" => "Tarjeta Crédito",
            "49" => "Tarjeta Débito",
            "1" => "Instrumento no definido",
            _ => string.IsNullOrWhiteSpace(code) ? string.Empty : code
        };

    private static string BuildResolucion(
        string? auth,
        string prefijo,
        string? from,
        string? to)
    {
        if (string.IsNullOrWhiteSpace(auth) && string.IsNullOrWhiteSpace(prefijo))
            return string.Empty;
        return $"Autorización {auth} Prefijo {prefijo} desde {from} hasta {to}".Trim();
    }

    private static string GuessIvaRateLabel(XElement root)
    {
        var percent = root.Descendants(Cac + "TaxSubtotal")
            .Where(t => Val(t.Element(Cac + "TaxCategory")?.Element(Cac + "TaxScheme"), Cbc + "ID") == "01"
                        || Val(t.Element(Cac + "TaxCategory"), Cbc + "ID") == "01")
            .Select(t => Val(t.Element(Cac + "TaxCategory"), Cbc + "Percent"))
            .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));
        return string.IsNullOrWhiteSpace(percent) ? string.Empty : $"{percent}%";
    }

    private static decimal SumTaxByScheme(IEnumerable<XElement> taxTotals, string schemeId)
    {
        decimal sum = 0;
        foreach (var taxTotal in taxTotals)
        {
            foreach (var sub in taxTotal.Elements(Cac + "TaxSubtotal"))
            {
                var id = First(
                    Val(sub.Element(Cac + "TaxCategory")?.Element(Cac + "TaxScheme"), Cbc + "ID"),
                    Val(sub.Element(Cac + "TaxCategory"), Cbc + "ID"));
                if (id == schemeId)
                    sum += ParseDecimal(Val(sub, Cbc + "TaxAmount")) ?? 0m;
            }
        }

        return sum;
    }

    private static decimal SumAllowance(IEnumerable<XElement> charges, bool charge)
    {
        decimal sum = 0;
        foreach (var el in charges)
        {
            var indicator = Val(el, Cbc + "ChargeIndicator");
            var isCharge = string.Equals(indicator, "true", StringComparison.OrdinalIgnoreCase);
            if (isCharge != charge)
                continue;
            sum += ParseDecimal(Val(el, Cbc + "Amount")) ?? 0m;
        }

        return sum;
    }

    private static string? LocalDescendant(XElement? root, string localName) =>
        root?.Descendants().FirstOrDefault(e => e.Name.LocalName == localName)?.Value?.Trim();

    private static string? LocalSiblingAfterAuthorization(XElement root, string localName)
    {
        var auth = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "InvoiceControl")
                   ?? root.Descendants().FirstOrDefault(e => e.Name.LocalName == "AuthorizedInvoices");
        return auth?.Descendants().FirstOrDefault(e => e.Name.LocalName == localName)?.Value?.Trim();
    }

    private static string? Val(XElement? parent, XName? name)
    {
        if (parent is null)
            return null;
        if (name is null)
            return parent.Value?.Trim();
        return parent.Element(name)?.Value?.Trim();
    }

    private static string? Attr(XElement? el, string name) =>
        el?.Attribute(name)?.Value?.Trim();

    private static string? First(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static decimal? ParseDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static int? ParseInt(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        return int.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}
