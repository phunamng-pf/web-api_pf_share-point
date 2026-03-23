using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharePoint.Application.Abstractions;
using SharePoint.Infrastructure.Identity;
using SharePoint.Infrastructure.Options;
using SharePoint.Infrastructure.Persistence;
using SharePoint.Infrastructure.Storage;

namespace SharePoint.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));

        services.AddSingleton<IFolderRepository, InMemoryFolderRepository>();
        services.AddSingleton<IFileRepository, InMemoryFileRepository>();
        services.AddSingleton<IUserContext, TrainingUserContext>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        return services;
    }
}
