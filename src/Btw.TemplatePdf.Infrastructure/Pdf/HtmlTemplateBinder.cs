using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Btw.TemplatePdf.Domain.Invoices;
using QRCoder;

namespace Btw.TemplatePdf.Infrastructure.Pdf;

/// <summary>
/// Fills studio HTML placeholders ({{path}}, {{path|moneda}}, {{#each}}) with invoice data.
/// Mirrors btw-template-studio <c>renderPreviewHtml</c>.
/// </summary>
public static class HtmlTemplateBinder
{
    private static readonly Regex EachRegex = new(
        @"\{\{#each\s+([\w.]+)\}\}([\s\S]*?)\{\{/each\}\}",
        RegexOptions.Compiled);

    private static readonly Regex TokenRegex = new(
        @"\{\{([\w.]+)(?:\|(\w+))?\}\}",
        RegexOptions.Compiled);

    private static readonly Regex AssetRegex = new(
        @"\{\{asset:([a-zA-Z0-9_-]+)\}\}",
        RegexOptions.Compiled);

    private static readonly Regex QrFixedRegex = new(
        @"\{\{qrFixed:([^}]+)\}\}",
        RegexOptions.Compiled);

    public static string Bind(
        string html,
        string css,
        InvoiceViewModel invoice,
        IReadOnlyDictionary<string, byte[]> assets)
    {
        using var dataDoc = ToJsonDocument(invoice.Data);
        var root = dataDoc.RootElement;

        var body = EachRegex.Replace(html, match =>
        {
            var path = match.Groups[1].Value;
            var inner = match.Groups[2].Value;
            if (!TryGetPath(root, path, out var list) || list.ValueKind != JsonValueKind.Array)
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var item in list.EnumerateArray())
            {
                sb.Append(TokenRegex.Replace(inner, m =>
                    FormatValue(GetPath(item, m.Groups[1].Value), m.Groups[2].Value)));
            }

            return sb.ToString();
        });

        body = AssetRegex.Replace(body, match =>
        {
            var id = match.Groups[1].Value;
            if (!assets.TryGetValue(id, out var bytes) || bytes.Length == 0)
                return string.Empty;
            var mime = GuessMime(bytes);
            return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
        });

        body = QrFixedRegex.Replace(body, match =>
        {
            try
            {
                var decoded = Uri.UnescapeDataString(match.Groups[1].Value);
                return QrDataUrl(decoded);
            }
            catch
            {
                return string.Empty;
            }
        });

        body = TokenRegex.Replace(body, match =>
            FormatValue(GetPath(root, match.Groups[1].Value), match.Groups[2].Value));

        var styleTag = $"<style>{css}</style>";
        if (body.Contains("</head>", StringComparison.OrdinalIgnoreCase))
            return body.Replace("</head>", $"{styleTag}</head>", StringComparison.OrdinalIgnoreCase);

        return $"""
            <!DOCTYPE html>
            <html>
            <head>
              <meta charset="utf-8" />
              {styleTag}
            </head>
            <body>
              {body}
            </body>
            </html>
            """;
    }

    private static JsonDocument ToJsonDocument(IReadOnlyDictionary<string, object?> data)
    {
        var json = JsonSerializer.Serialize(data);
        return JsonDocument.Parse(json);
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

    private static JsonElement GetPath(JsonElement root, string path)
    {
        return TryGetPath(root, path, out var value) ? value : default;
    }

    private static string FormatValue(JsonElement value, string filter)
    {
        if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return string.Empty;

        if (filter == "moneda")
        {
            if (value.TryGetDecimal(out var amount) ||
                (value.ValueKind == JsonValueKind.String &&
                 decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out amount)))
            {
                return amount.ToString("C0", CultureInfo.GetCultureInfo("es-CO"));
            }
        }

        if (filter == "fecha")
        {
            if (DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date))
                return date.ToString("d", CultureInfo.GetCultureInfo("es-CO"));
        }

        if (filter == "qr")
            return QrDataUrl(value.ToString());

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.TryGetDecimal(out var n)
                ? n.ToString("N0", CultureInfo.GetCultureInfo("es-CO"))
                : value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => value.ToString()
        };
    }

    /// <summary>
    /// Embed QR as data-URL PNG (no external api.qrserver.com — Playwright often cannot load it).
    /// </summary>
    private static string QrDataUrl(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return string.Empty;

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload.Trim(), QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        var bytes = png.GetGraphic(pixelsPerModule: 8);
        return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
    }

    private static string GuessMime(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50)
            return "image/png";
        return "application/octet-stream";
    }
}
