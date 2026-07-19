using Btw.TemplatePdf.Domain.Abstractions;
using Btw.TemplatePdf.Domain.Invoices;
using Btw.TemplatePdf.Domain.Templates;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Btw.TemplatePdf.Infrastructure.Pdf;

/// <summary>
/// Renders bound HTML/CSS to PDF via a shared Chromium (Microsoft Playwright, MIT).
/// </summary>
public sealed class PlaywrightPdfRenderer : IPdfRenderer
{
    private readonly PlaywrightBrowserPool _pool;
    private readonly ILogger<PlaywrightPdfRenderer> _logger;

    public PlaywrightPdfRenderer(
        PlaywrightBrowserPool pool,
        ILogger<PlaywrightPdfRenderer> logger)
    {
        _pool = pool;
        _logger = logger;
    }

    public async Task<byte[]> RenderAsync(
        TemplateDefinition template,
        InvoiceViewModel invoice,
        IReadOnlyDictionary<string, byte[]> assets,
        CancellationToken cancellationToken = default)
    {
        var html = HtmlTemplateBinder.Bind(
            template.Html,
            template.Css,
            invoice,
            assets);

        if (string.IsNullOrWhiteSpace(template.Html))
        {
            _logger.LogWarning(
                "Template {TemplateId} has empty Html; PDF will be mostly blank.",
                template.TemplateId);
        }

        var page = await _pool.NewPageAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await page.SetContentAsync(
                    html,
                    new PageSetContentOptions { WaitUntil = WaitUntilState.NetworkIdle })
                .ConfigureAwait(false);

            var widthMm = template.Page.WidthMm <= 0 ? 216 : template.Page.WidthMm;
            var heightMm = template.Page.HeightMm <= 0 ? 279 : template.Page.HeightMm;
            var margins = template.Page.Margins;

            return await page.PdfAsync(new PagePdfOptions
                {
                    Width = $"{widthMm.ToString(System.Globalization.CultureInfo.InvariantCulture)}mm",
                    Height = $"{heightMm.ToString(System.Globalization.CultureInfo.InvariantCulture)}mm",
                    PrintBackground = true,
                    Margin = new Margin
                    {
                        Top = $"{margins.Top.ToString(System.Globalization.CultureInfo.InvariantCulture)}mm",
                        Right = $"{margins.Right.ToString(System.Globalization.CultureInfo.InvariantCulture)}mm",
                        Bottom = $"{margins.Bottom.ToString(System.Globalization.CultureInfo.InvariantCulture)}mm",
                        Left = $"{margins.Left.ToString(System.Globalization.CultureInfo.InvariantCulture)}mm"
                    }
                })
                .ConfigureAwait(false);
        }
        finally
        {
            await page.CloseAsync().ConfigureAwait(false);
        }
    }
}
