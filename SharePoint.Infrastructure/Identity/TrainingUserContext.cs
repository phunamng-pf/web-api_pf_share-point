using SharePoint.Application.Abstractions;

namespace SharePoint.Infrastructure.Identity;

public sealed class TrainingUserContext : IUserContext
{
    private static readonly Guid TrainingUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public Guid UserId => TrainingUserId;
    public string Email => "training.user@local";
}
