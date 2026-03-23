using SharePoint.Application.Abstractions;
using SharePoint.Application.Contracts;

namespace SharePoint.Application.Services;

public sealed class DocumentService : IDocumentService
{
    private readonly IFolderRepository _folderRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IUserContext _userContext;

    public DocumentService(
        IFolderRepository folderRepository,
        IFileRepository fileRepository,
        IUserContext userContext)
    {
        _folderRepository = folderRepository;
        _fileRepository = fileRepository;
        _userContext = userContext;
    }

    public async Task<DocumentsResponse> GetDocumentsAsync(Guid? parentId, CancellationToken cancellationToken)
    {
        if (parentId.HasValue)
        {
            var parentFolder = await _folderRepository.GetByIdAsync(parentId.Value, cancellationToken)
                               ?? throw new FileNotFoundException($"Folder '{parentId.Value}' not found.");

            if (parentFolder.CreatedByUserId != _userContext.UserId)
                throw new UnauthorizedAccessException("You do not have permission to access this folder.");
        }

        var folders = await _folderRepository.GetChildrenAsync(parentId, cancellationToken);
        var files = await _fileRepository.GetByFolderAsync(parentId, cancellationToken);

        var folderDtos = folders
            .Select(f => new FolderDto(f.Id, f.Name, f.ParentId, f.CreatedAtUtc))
            .ToArray();

        var fileDtos = files
            .Select(f => new FileItemDto(f.Id, f.Name, f.Extension, f.ContentType, f.SizeInBytes, f.ParentFolderId, f.CreatedAtUtc))
            .ToArray();

        return new DocumentsResponse(folderDtos, fileDtos);
    }
}
