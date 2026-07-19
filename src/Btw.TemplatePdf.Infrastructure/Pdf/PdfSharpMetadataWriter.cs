using System.Text;
using System.Text.RegularExpressions;
using Btw.TemplatePdf.Domain.Abstractions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Btw.TemplatePdf.Infrastructure.Pdf;

/// <summary>
/// Applies PDF Info metadata with PDFsharp (MIT). Creator and Producer are branded
/// as BTW Template PDF so viewers do not surface the toolkit name (Info + XMP).
/// </summary>
public sealed class PdfSharpMetadataWriter : IPdfMetadataWriter
{
    private static readonly Regex ProducerLiteral = new(
        @"/Producer\s*\(((?:\\.|[^\\)])*)\)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PdfProducerXmp = new(
        @"<pdf:Producer>([^<]*)</pdf:Producer>",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex XmpCreatorTool = new(
        @"<xmp:CreatorTool>([^<]*)</xmp:CreatorTool>",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    static PdfSharpMetadataWriter()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public byte[] Apply(byte[] pdfBytes, PdfFileMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        ArgumentNullException.ThrowIfNull(metadata);

        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes are empty.", nameof(pdfBytes));

        using var input = new MemoryStream(pdfBytes);
        using var document = PdfReader.Open(input, PdfDocumentOpenMode.Modify);

        var info = document.Info;
        info.Title = metadata.Title;
        info.Author = metadata.Author;
        info.Subject = metadata.Subject;
        info.Keywords = metadata.Keywords;
        info.Creator = metadata.Creator;
        info.CreationDate = metadata.CreationDateUtc;
        info.ModificationDate = metadata.ModificationDateUtc;
        info.Elements["/Producer"] = new PdfString(metadata.Producer);

        using var output = new MemoryStream();
        document.Save(output, closeStream: false);

        // PDFsharp overwrites /Producer and XMP Producer on Save. Replace in-place
        // with the same byte length so the xref table stays valid.
        return BrandProducerLiterals(output.ToArray(), metadata.Producer, metadata.Creator);
    }

    internal static byte[] BrandProducerLiterals(byte[] pdfBytes, string producer, string creator)
    {
        var text = Encoding.Latin1.GetString(pdfBytes);
        text = ReplaceAllGroupsSameLength(text, ProducerLiteral, producer);
        text = ReplaceAllGroupsSameLength(text, PdfProducerXmp, producer);
        text = ReplaceAllGroupsSameLength(text, XmpCreatorTool, creator);
        // PDFsharp also embeds a plain comment like "% Created with PDFsharp 6.2.3 (CORE)".
        text = text.Replace("PDFsharp", "BTW-PDF ", StringComparison.Ordinal);
        return Encoding.Latin1.GetBytes(text);
    }

    private static string ReplaceAllGroupsSameLength(string text, Regex pattern, string value)
    {
        var matches = pattern.Matches(text);
        if (matches.Count == 0)
            return text;

        // Replace from the end so indices stay valid.
        for (var i = matches.Count - 1; i >= 0; i--)
        {
            var match = matches[i];
            if (match.Groups.Count < 2)
                continue;

            var group = match.Groups[1];
            var fitted = FitToLength(EscapePdfLiteral(value), group.Length);
            if (fitted.Length != group.Length)
                continue;

            text = string.Concat(
                text.AsSpan(0, group.Index),
                fitted,
                text.AsSpan(group.Index + group.Length));
        }

        return text;
    }

    private static string FitToLength(string value, int length)
    {
        if (length <= 0) return string.Empty;
        if (value.Length == length) return value;
        if (value.Length > length) return value[..length];
        return value.PadRight(length, ' ');
    }

    private static string EscapePdfLiteral(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
}
