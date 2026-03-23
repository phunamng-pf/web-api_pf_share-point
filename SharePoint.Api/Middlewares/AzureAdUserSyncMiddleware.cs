using SharePoint.Application.Abstractions;
using SharePoint.Infrastructure.Identity;

namespace SharePoint.Api.Middlewares;

public sealed class AzureAdUserSyncMiddleware
{
    private readonly RequestDelegate _next;

    public AzureAdUserSyncMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAzureAdUserSyncService userSyncService)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var user = await userSyncService.EnsureUserAsync(context.User, context.RequestAborted);
            context.Items[HttpUserContext.UserItemKey] = user;
        }

        await _next(context);
    }
}
