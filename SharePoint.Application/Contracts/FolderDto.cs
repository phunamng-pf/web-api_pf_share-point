namespace SharePoint.Application.Contracts;

public sealed record FolderDto(Guid Id, string Name, Guid? ParentId, DateTime CreatedAtUtc);
