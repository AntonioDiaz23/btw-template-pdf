using Btw.TemplatePdf.Application.Pdf;
using Btw.TemplatePdf.Domain.Abstractions;
using Btw.TemplatePdf.Infrastructure.Assets;
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

        services.AddScoped<ITemplateStore, PostgresTemplateStore>();
        services.AddScoped<TemplateCatalogService>();
        services.AddSingleton<IUblStore, InMemoryUblStore>();
        services.AddSingleton<IUblToViewModelMapper, StubUblToViewModelMapper>();
        services.AddSingleton<IAssetStore, NullAssetStore>();
        services.AddSingleton<IPdfRenderer, StubPdfRenderer>();
        services.AddScoped<GeneratePdfByCufeUseCase>();
        return services;
    }
}
