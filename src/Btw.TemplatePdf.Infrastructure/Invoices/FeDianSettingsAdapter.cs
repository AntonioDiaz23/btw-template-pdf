using Btw.TemplatePdf.Application.Ubl;
using Microsoft.Extensions.Options;

namespace Btw.TemplatePdf.Infrastructure.Invoices;

public sealed class FeDianSettingsAdapter : IFeDianSettings
{
    private readonly FeDianOptions _options;

    public FeDianSettingsAdapter(IOptions<FeDianOptions> options)
    {
        _options = options.Value;
    }

    public string Environment =>
        string.IsNullOrWhiteSpace(_options.Environment) ? "UAT" : _options.Environment.Trim().ToUpperInvariant();

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.BaseUrl);
}
