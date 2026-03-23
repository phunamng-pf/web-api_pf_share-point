using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharePoint.Application.Abstractions;
using SharePoint.Domain.Common;
using SharePoint.Infrastructure.Identity;
using SharePoint.Infrastructure.Persistence;
using SharePoint.Infrastructure.Storage;

namespace SharePoint.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddHttpContextAccessor();

        services.AddScoped<IFolderRepository, FolderRepository>();
        services.AddScoped<IFileRepository, FileRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserContext, HttpUserContext>();
        services.AddScoped<IFileStorage, LocalFileStorage>();

        return services;
    }
}
