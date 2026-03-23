namespace SharePoint.Application.Contracts;

public sealed record CreateFolderRequest(string Name, Guid? ParentId);
