namespace SharePoint.Application.Contracts;

public sealed record UploadFileRequest(string FileName, string ContentType, Guid? ParentFolderId, Stream Content);
