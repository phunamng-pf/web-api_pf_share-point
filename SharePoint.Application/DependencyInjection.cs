using Microsoft.Extensions.DependencyInjection;
using SharePoint.Application.Abstractions;
using SharePoint.Application.Services;

namespace SharePoint.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IFolderService, FolderService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IAzureAdUserSyncService, AzureAdUserSyncService>();
        return services;
    }
}
