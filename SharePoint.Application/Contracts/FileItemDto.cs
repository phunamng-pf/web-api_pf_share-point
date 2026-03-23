namespace SharePoint.Application.Contracts;

public sealed record FileItemDto(Guid Id, string Name, string Extension, string ContentType, long SizeInBytes, Guid? ParentFolderId, DateTime CreatedAtUtc);
