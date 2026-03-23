using Microsoft.Extensions.DependencyInjection;
using SharePoint.Application.Services;

namespace SharePoint.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IDocumentService, DocumentService>();
        return services;
    }
}
