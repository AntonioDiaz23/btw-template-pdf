using Btw.TemplatePdf.Application.Pdf;
using Btw.TemplatePdf.Application.Templates;
using Btw.TemplatePdf.Application.Ubl;
using Btw.TemplatePdf.Domain.Abstractions;
using Btw.TemplatePdf.Infrastructure.Assets;
using Btw.TemplatePdf.Infrastructure.Auth;
using Btw.TemplatePdf.Infrastructure.Invoices;
using Btw.TemplatePdf.Infrastructure.Pdf;
using Btw.TemplatePdf.Infrastructure.Persistence;
using Btw.TemplatePdf.Infrastructure.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Btw.TemplatePdf.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTemplatePdfInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("TemplatePdf")
            ?? throw new InvalidOperationException("Connection string 'TemplatePdf' is missing.");

        services.AddDbContext<TemplateDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.Configure<FeDianOptions>(configuration.GetSection(FeDianOptions.SectionName));
        services.AddScoped<IFeDianSettings, FeDianSettingsAdapter>();

        services.AddScoped<IFeBearerTokenAccessor, FeBearerTokenAccessor>();

        services.AddHttpClient<FeDianDocumentClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FeDianOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
        });

        services.AddSingleton<InMemoryUblStore>();
        services.AddSingleton<UblDiagnosticsWriter>();
        services.AddScoped<IUblStore, FeDianUblStore>();
        services.AddScoped<ITemplateStore, PostgresTemplateStore>();
        services.AddScoped<ITemplateCatalog, PostgresTemplateCatalog>();
        services.AddSingleton<IUblToViewModelMapper, DianUblToViewModelMapper>();
        services.AddSingleton<IAssetStore, NullAssetStore>();
        services.AddSingleton<IPdfRenderer, PlaywrightPdfRenderer>();
        return services;
    }
}
