using SharePoint.Application.Abstractions;
using SharePoint.Infrastructure.Identity;

namespace SharePoint.Api.Middlewares;

public class AzureAdUserSyncMiddleware
{
    private readonly RequestDelegate _next;

    public AzureAdUserSyncMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var objectId = context.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
            if (!string.IsNullOrEmpty(objectId))
            {
                var userRepository = context.RequestServices.GetRequiredService<SharePoint.Application.Abstractions.IUserRepository>();
                var user = await userRepository.GetByAzureAdObjectIdAsync(objectId, context.RequestAborted);
                context.Items[HttpUserContext.UserItemKey] = user;
            }
        }

        await _next(context);
    }
}
