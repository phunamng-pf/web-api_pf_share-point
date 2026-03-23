namespace SharePoint.Application.Contracts;

public sealed record DocumentsResponse(
    IReadOnlyCollection<FolderDto> Folders,
    IReadOnlyCollection<FileItemDto> Files);
