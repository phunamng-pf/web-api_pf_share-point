using Microsoft.AspNetCore.Http;
using SharePoint.Application.Abstractions;
using SharePoint.Domain.Entities;

namespace SharePoint.Infrastructure.Identity;

public sealed class HttpUserContext : IUserContext
{
    public const string UserItemKey = "CurrentUser";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId
    {
        get
        {
            var user = GetCurrentUser();
            return user.Id;
        }
    }

    public string Email
    {
        get
        {
            var user = GetCurrentUser();
            return user.Email;
        }
    }

    private AppUser GetCurrentUser()
    {
        var context = _httpContextAccessor.HttpContext
            ?? throw new UnauthorizedAccessException("Request context is not available.");

        if (context.Items.TryGetValue(UserItemKey, out var value) && value is AppUser user)
        {
            return user;
        }

        throw new UnauthorizedAccessException("User is not initialized for this request.");
    }
}
