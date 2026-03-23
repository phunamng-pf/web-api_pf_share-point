namespace SharePoint.Api.Contracts;

public sealed record CreateFolderHttpRequest(string Name, Guid? ParentId);
