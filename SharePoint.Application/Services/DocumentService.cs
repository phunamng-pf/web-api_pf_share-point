using SharePoint.Application.Abstractions;
using SharePoint.Application.Contracts;
using SharePoint.Application.Contracts.Response;

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
            .Select(x => MapFile(x, displayNameLookup))
            .ToArray();

        var rootFolders = folderLookup[RootFolderId]
            .OrderBy(x => x.Name)
            .Select(x => BuildFolderTree(x, folderLookup, fileLookup, displayNameLookup))
            .ToArray();

        return new FolderTreeDto
        {
            Id = rootFolder.Id.ToString(),
            Name = rootFolder.Name,
            Files = rootFiles,
            SubFolders = rootFolders,
            CreatedAt = rootFolder.CreatedAt,
            CreatedBy = ResolveDisplayName(rootFolder.CreatedByUserId, displayNameLookup),
            ModifiedAt = rootFolder.ModifiedAt,
            ModifiedBy = ResolveDisplayName(rootFolder.ModifiedByUserId, displayNameLookup),
            ParentId = rootFolder.ParentId?.ToString()
        };
    }

    private static FolderTreeDto BuildFolderTree(
        Domain.Entities.Folder folder,
        ILookup<Guid?, Domain.Entities.Folder> folderLookup,
        ILookup<Guid?, Domain.Entities.FileItem> fileLookup,
        IReadOnlyDictionary<Guid, string> displayNameLookup)
    {
        var childFiles = fileLookup[folder.Id]
            .OrderBy(x => x.Name)
            .Select(x => MapFile(x, displayNameLookup))
            .ToArray();

        var childFolders = folderLookup[folder.Id]
            .OrderBy(x => x.Name)
            .Select(x => BuildFolderTree(x, folderLookup, fileLookup, displayNameLookup))
            .ToArray();

        return new FolderTreeDto
        {
            Id = folder.Id.ToString(),
            Name = folder.Name,
            Files = childFiles,
            SubFolders = childFolders,
            CreatedAt = folder.CreatedAt,
            CreatedBy = ResolveDisplayName(folder.CreatedByUserId, displayNameLookup),
            ModifiedAt = folder.ModifiedAt,
            ModifiedBy = ResolveDisplayName(folder.ModifiedByUserId, displayNameLookup),
            ParentId = folder.ParentId?.ToString()
        };
    }

    private static FileItemViewDto MapFile(Domain.Entities.FileItem file, IReadOnlyDictionary<Guid, string> displayNameLookup)
    {
        return new FileItemViewDto
        {
            Id = file.Id.ToString(),
            Name = file.Name,
            Extension = file.Extension,
            ContentType = file.ContentType,
            SizeInBytes = file.SizeInBytes,
            CreatedAt = file.CreatedAt,
            CreatedBy = ResolveDisplayName(file.CreatedByUserId, displayNameLookup),
            ModifiedAt = file.ModifiedAt,
            ModifiedBy = ResolveDisplayName(file.ModifiedByUserId, displayNameLookup),
            ParentFolderId = file.ParentFolderId?.ToString()
        };
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

    private static string ResolveDisplayName(Guid userId, IReadOnlyDictionary<Guid, string> displayNameLookup)
    {
        if (userId == Guid.Empty)
        {
            return "System";
        }

        return displayNameLookup.TryGetValue(userId, out var displayName)
            ? displayName
            : userId.ToString();
    }

    private static string? ResolveDisplayName(Guid? userId, IReadOnlyDictionary<Guid, string> displayNameLookup)
    {
        if (!userId.HasValue)
        {
            return null;
        }

        return ResolveDisplayName(userId.Value, displayNameLookup);
    }
}
