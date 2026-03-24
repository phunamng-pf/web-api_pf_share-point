using SharePoint.Application.Abstractions;
using SharePoint.Application.Contracts;
using SharePoint.Application.Contracts.Response;

namespace SharePoint.Application.Services;

public class DocumentService : IDocumentService
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

    public async Task<FolderTreeDto> GetMyDocumentsAsync(CancellationToken cancellationToken)
    {
        var userFolders = await _folderRepository.GetByUserAsync(_userContext.UserId, cancellationToken);
        var userFiles = await _fileRepository.GetByUserAsync(_userContext.UserId, cancellationToken);

        return BuildRootFolder(userFolders, userFiles);
    }

    private FolderTreeDto BuildRootFolder(IReadOnlyCollection<Domain.Entities.Folder> folders, IReadOnlyCollection<Domain.Entities.FileItem> files)
    {
        var folderLookup = folders.ToLookup(x => x.ParentId);
        var fileLookup = files.ToLookup(x => x.ParentFolderId);

        var rootFiles = fileLookup[null]
            .OrderBy(x => x.Name)
            .Select(MapFile)
            .ToArray();

        var rootFolders = folderLookup[null]
            .OrderBy(x => x.Name)
            .Select(x => BuildFolderTree(x, folderLookup, fileLookup))
            .ToArray();

        return new FolderTreeDto
        {
            Id = "root",
            Name = "Documents",
            Files = rootFiles,
            SubFolders = rootFolders,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _userContext.UserId.ToString(),
            ModifiedAt = null,
            ModifiedBy = null,
            ParentId = null
        };
    }

    private static FolderTreeDto BuildFolderTree(
        Domain.Entities.Folder folder,
        ILookup<Guid?, Domain.Entities.Folder> folderLookup,
        ILookup<Guid?, Domain.Entities.FileItem> fileLookup)
    {
        var childFiles = fileLookup[folder.Id]
            .OrderBy(x => x.Name)
            .Select(MapFile)
            .ToArray();

        var childFolders = folderLookup[folder.Id]
            .OrderBy(x => x.Name)
            .Select(x => BuildFolderTree(x, folderLookup, fileLookup))
            .ToArray();

        return new FolderTreeDto
        {
            Id = folder.Id.ToString(),
            Name = folder.Name,
            Files = childFiles,
            SubFolders = childFolders,
            CreatedAt = folder.CreatedAtUtc,
            CreatedBy = folder.CreatedByUserId.ToString(),
            ModifiedAt = folder.ModifiedAtUtc,
            ModifiedBy = folder.ModifiedByUserId?.ToString(),
            ParentId = folder.ParentId?.ToString()
        };
    }

    private static FileItemViewDto MapFile(Domain.Entities.FileItem file)
    {
        return new FileItemViewDto
        {
            Id = file.Id.ToString(),
            Name = file.Name,
            Extension = file.Extension,
            ContentType = file.ContentType,
            SizeInBytes = file.SizeInBytes,
            CreatedAt = file.CreatedAtUtc,
            CreatedBy = file.CreatedByUserId.ToString(),
            ModifiedAt = file.ModifiedAtUtc,
            ModifiedBy = file.ModifiedByUserId?.ToString(),
            ParentFolderId = file.ParentFolderId?.ToString()
        };
    }
}
