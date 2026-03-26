using SharePoint.Application.Abstractions;
using SharePoint.Application.Contracts;
using SharePoint.Application.Contracts.Response;
using SharePoint.Application.Helper;

namespace SharePoint.Application.Services;

public class DocumentService : IDocumentService
{
    private static readonly Guid RootFolderId = Guid.Empty;

    private readonly IFolderRepository _folderRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUserContext _userContext;

    public DocumentService(
        IFolderRepository folderRepository,
        IFileRepository fileRepository,
        IUserRepository userRepository,
        IUserContext userContext)
    {
        _folderRepository = folderRepository;
        _fileRepository = fileRepository;
        _userRepository = userRepository;
        _userContext = userContext;
    }

    public async Task<FolderTreeDto> GetMyDocumentsAsync(CancellationToken cancellationToken)
    {
        var userFolders = await _folderRepository.GetByUserAsync(_userContext.UserId, cancellationToken);
        var userFiles = await _fileRepository.GetByUserAsync(_userContext.UserId, cancellationToken);
        var displayNameLookup = await BuildDisplayNameLookupAsync(userFolders, userFiles, cancellationToken);

        return BuildRootFolder(userFolders, userFiles, displayNameLookup);
    }

    public async Task<FolderTreeDto> GetRecycleBinDocumentsAsync(CancellationToken cancellationToken)
    {
        var rootFolder = await _folderRepository.GetByIdAsync(RootFolderId, cancellationToken)
            ?? throw new FileNotFoundException($"Folder '{RootFolderId}' not found.");

        var deletedUserFolders = await _folderRepository.GetDeletedByUserAsync(_userContext.UserId, cancellationToken);
        var deletedUserFiles = await _fileRepository.GetDeletedByUserAsync(_userContext.UserId, cancellationToken);
        var displayNameLookup = await BuildDisplayNameLookupAsync(deletedUserFolders, deletedUserFiles, cancellationToken);

        return BuildRecycleBinRoot(rootFolder, deletedUserFolders, deletedUserFiles, displayNameLookup);
    }

    private FolderTreeDto BuildRootFolder(
        IReadOnlyCollection<Domain.Entities.Folder> folders,
        IReadOnlyCollection<Domain.Entities.FileItem> files,
        IReadOnlyDictionary<Guid, string> displayNameLookup)
    {
        var folderLookup = folders.ToLookup(x => x.ParentId);
        var fileLookup = files.ToLookup(x => x.ParentFolderId);

        var rootFolder = folders.FirstOrDefault(x => x.Id == RootFolderId)
            ?? throw new FileNotFoundException($"Folder '{RootFolderId}' not found.");

        var rootFiles = fileLookup[RootFolderId]
            .OrderBy(x => x.Name)
            .Select(x => DtoMappingHelper.MapFile(x, displayNameLookup))
            .ToArray();

        var rootFolders = folderLookup[RootFolderId]
            .OrderBy(x => x.Name)
            .Select(x => BuildFolderTree(x, folderLookup, fileLookup, displayNameLookup))
            .ToArray();

        return DtoMappingHelper.MapFolder(rootFolder, rootFolders, rootFiles, displayNameLookup);
    }

    private static FolderTreeDto BuildFolderTree(
        Domain.Entities.Folder folder,
        ILookup<Guid?, Domain.Entities.Folder> folderLookup,
        ILookup<Guid?, Domain.Entities.FileItem> fileLookup,
        IReadOnlyDictionary<Guid, string> displayNameLookup)
    {
        var childFiles = fileLookup[folder.Id]
            .OrderBy(x => x.Name)
            .Select(x => DtoMappingHelper.MapFile(x, displayNameLookup))
            .ToArray();

        var childFolders = folderLookup[folder.Id]
            .OrderBy(x => x.Name)
            .Select(x => BuildFolderTree(x, folderLookup, fileLookup, displayNameLookup))
            .ToArray();

        return DtoMappingHelper.MapFolder(folder, childFolders, childFiles, displayNameLookup);
    }

    private static FolderTreeDto BuildRecycleBinRoot(
        Domain.Entities.Folder rootFolder,
        IReadOnlyCollection<Domain.Entities.Folder> deletedFolders,
        IReadOnlyCollection<Domain.Entities.FileItem> deletedFiles,
        IReadOnlyDictionary<Guid, string> displayNameLookup)
    {
        var deletedFolderIdSet = deletedFolders
            .Select(x => x.Id)
            .ToHashSet();

        var deletedFoldersByParent = deletedFolders
            .Where(x => x.ParentId.HasValue && deletedFolderIdSet.Contains(x.ParentId.Value))
            .ToLookup(x => x.ParentId);

        var deletedFilesByParent = deletedFiles
            .Where(x => x.ParentFolderId.HasValue && deletedFolderIdSet.Contains(x.ParentFolderId.Value))
            .ToLookup(x => x.ParentFolderId);

        var rootFolders = deletedFolders
            .Where(x => !x.ParentId.HasValue || !deletedFolderIdSet.Contains(x.ParentId.Value))
            .OrderBy(x => x.Name)
            .Select(x => BuildFolderTree(x, deletedFoldersByParent, deletedFilesByParent, displayNameLookup))
            .ToArray();

        var rootFiles = deletedFiles
            .Where(x => !x.ParentFolderId.HasValue || !deletedFolderIdSet.Contains(x.ParentFolderId.Value))
            .OrderBy(x => x.Name)
            .Select(x => DtoMappingHelper.MapFile(x, displayNameLookup))
            .ToArray();

        return DtoMappingHelper.MapFolder(rootFolder, rootFolders, rootFiles, displayNameLookup);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> BuildDisplayNameLookupAsync(
        IEnumerable<Domain.Entities.Folder> folders,
        IEnumerable<Domain.Entities.FileItem> files,
        CancellationToken cancellationToken)
    {
        var userIds = new HashSet<Guid>();

        foreach (var folder in folders)
        {
            if (folder.CreatedByUserId != Guid.Empty)
            {
                userIds.Add(folder.CreatedByUserId);
            }

            if (folder.ModifiedByUserId.HasValue && folder.ModifiedByUserId.Value != Guid.Empty)
            {
                userIds.Add(folder.ModifiedByUserId.Value);
            }
        }

        foreach (var file in files)
        {
            if (file.CreatedByUserId != Guid.Empty)
            {
                userIds.Add(file.CreatedByUserId);
            }

            if (file.ModifiedByUserId.HasValue && file.ModifiedByUserId.Value != Guid.Empty)
            {
                userIds.Add(file.ModifiedByUserId.Value);
            }
        }

        return await _userRepository.GetDisplayNamesByIdsAsync(userIds.ToArray(), cancellationToken);
    }
}
