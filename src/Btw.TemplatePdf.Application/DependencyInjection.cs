using Btw.TemplatePdf.Application.Pdf;
using Btw.TemplatePdf.Application.Templates;
using Btw.TemplatePdf.Application.Ubl;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Btw.TemplatePdf.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTemplatePdfApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<GeneratePdfByCufeRequestValidator>();

        services.AddScoped<GeneratePdfByCufeUseCase>();
        services.AddScoped<GetUblByCufeUseCase>();
        services.AddScoped<ListTemplatesUseCase>();
        services.AddScoped<GetTemplateUseCase>();
        services.AddScoped<CreateTemplateUseCase>();
        services.AddScoped<SaveDraftUseCase>();
        services.AddScoped<PublishTemplateUseCase>();

        return services;
    }
}
