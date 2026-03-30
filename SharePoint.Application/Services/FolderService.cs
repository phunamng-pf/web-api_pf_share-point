using SharePoint.Application.Abstractions;
using SharePoint.Application.Contracts;
using SharePoint.Application.Contracts.Request;
using SharePoint.Application.Contracts.Response;
using SharePoint.Domain.Entities;
using SharePoint.Application.Helper;
using System.IO.Compression;

namespace SharePoint.Application.Services;

public class FolderService : IFolderService
{
    private static readonly Guid RootFolderId = Guid.Empty;

    private readonly IFolderRepository _folderRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IFileStorage _fileStorage;
    private readonly IUserRepository _userRepository;
    private readonly IUserContext _userContext;

    public FolderService(
        IFolderRepository folderRepository,
        IFileRepository fileRepository,
        IFileStorage fileStorage,
        IUserRepository userRepository,
        IUserContext userContext)
    {
        _folderRepository = folderRepository;
        _fileRepository = fileRepository;
        _fileStorage = fileStorage;
        _userRepository = userRepository;
        _userContext = userContext;
    }

    public async Task<FolderTreeDto> CreateFolderAsync(ReqCreateFolderDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Folder name is required.", nameof(request.Name));
        }

        var normalizedParentId = DtoMappingHelper.NormalizeParentFolderId(request.ParentId);
        await ValidateParentFolderAccessAsync(normalizedParentId, cancellationToken);
        var now = DateTime.UtcNow;

        var folder = new Folder
        {
            Name = request.Name.Trim(),
            ParentId = normalizedParentId,
            CreatedAt = now,
            ModifiedAt = now,
            CreatedByUserId = _userContext.UserId,
            ModifiedByUserId = _userContext.UserId
        };

        var saved = await _folderRepository.AddAsync(folder, cancellationToken);
        var displayNameLookup = await BuildDisplayNameLookupAsync(new[] { saved }, Array.Empty<FileItem>(), cancellationToken);
        return DtoMappingHelper.MapFolder(saved, Array.Empty<FolderTreeDto>(), Array.Empty<FileItemViewDto>(), displayNameLookup);
    }

    public async Task<FolderTreeDto> GetFolderByIdAsync(Guid folderId, CancellationToken cancellationToken)
    {
        var folders = await _folderRepository.GetByUserAsync(_userContext.UserId, cancellationToken);
        var files = await _fileRepository.GetByUserAsync(_userContext.UserId, cancellationToken);

        var targetFolder = folders.FirstOrDefault(x => x.Id == folderId)
            ?? throw new FileNotFoundException($"Folder '{folderId}' not found.");

        var displayNameLookup = await BuildDisplayNameLookupAsync(folders, files, cancellationToken);
        return BuildFolderTree(targetFolder, folders, files, displayNameLookup);
    }

    public async Task<FolderTreeDto> UpdateFolderAsync(ReqGuidNameDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Folder name is required.", nameof(request));
        }

        if (request.Id == RootFolderId)
        {
            throw new InvalidOperationException("Root folder cannot be modified.");
        }

        var folder = await _folderRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new FileNotFoundException($"Folder '{request.Id}' not found.");

        folder.Name = request.Name.Trim();
        folder.ModifiedAt = DateTime.UtcNow;
        folder.ModifiedByUserId = _userContext.UserId;

        var updated = await _folderRepository.UpdateAsync(folder, cancellationToken);
        var displayNameLookup = await BuildDisplayNameLookupAsync(new[] { updated }, Array.Empty<Domain.Entities.FileItem>(), cancellationToken);
        return DtoMappingHelper.MapFolder(updated, Array.Empty<FolderTreeDto>(), Array.Empty<FileItemViewDto>(), displayNameLookup);
    }

    public async Task DeleteFolderAsync(Guid folderId, CancellationToken cancellationToken)
    {
        if (folderId == RootFolderId)
        {
            throw new InvalidOperationException("Root folder cannot be deleted.");
        }

        var folder = await _folderRepository.GetByIdAsync(folderId, cancellationToken)
                     ?? throw new FileNotFoundException($"Folder '{folderId}' not found.");
        await SoftDeleteFolderTreeAsync(folderId, cancellationToken);
    }

    public async Task RestoreFolderAsync(Guid folderId, CancellationToken cancellationToken)
    {
        if (folderId == RootFolderId)
        {
            throw new InvalidOperationException("Root folder cannot be restored.");
        }

        var folder = await _folderRepository.GetDeletedByIdAsync(folderId, cancellationToken);
        if (folder is null)
        {
            var existingFolder = await _folderRepository.GetByIdAsync(folderId, cancellationToken)
                ?? throw new FileNotFoundException($"Folder '{folderId}' not found.");
            return;
        }

        if (folder.ParentId.HasValue && folder.ParentId.Value != RootFolderId)
        {
            var parentFolder = await _folderRepository.GetByIdAsync(folder.ParentId.Value, cancellationToken);
            if (parentFolder is null)
            {
                var deletedParent = await _folderRepository.GetDeletedByIdAsync(folder.ParentId.Value, cancellationToken)
                    ?? throw new FileNotFoundException($"Folder '{folder.ParentId.Value}' not found.");
                throw new InvalidOperationException("Cannot restore folder while parent folder is deleted.");
            }
        }

        await RestoreFolderTreeAsync(folderId, cancellationToken);
    }

    public async Task DeleteFolderPermanentlyAsync(Guid folderId, CancellationToken cancellationToken)
    {
        if (folderId == RootFolderId)
        {
            throw new InvalidOperationException("Root folder cannot be permanently deleted.");
        }

        var folder = await _folderRepository.GetDeletedByIdAsync(folderId, cancellationToken);
        if (folder is null)
        {
            var existingFolder = await _folderRepository.GetByIdAsync(folderId, cancellationToken)
                ?? throw new FileNotFoundException($"Folder '{folderId}' not found.");

            throw new InvalidOperationException("Folder must be soft-deleted before permanent deletion.");
        }

        await DeleteFolderTreePermanentlyAsync(folderId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<BreadcrumbInfoDto>> GetBreadcrumbAsync(Guid folderId, CancellationToken cancellationToken)
    {
        var folders = await _folderRepository.GetByUserAsync(_userContext.UserId, cancellationToken);
        var lookup = folders.ToDictionary(x => x.Id);

        if (!lookup.TryGetValue(folderId, out var current))
        {
            throw new FileNotFoundException($"Folder '{folderId}' not found.");
        }

        var path = new List<BreadcrumbInfoDto>
        {
            new BreadcrumbInfoDto
            {
                Id = RootFolderId.ToString(),
                Name = "Documents"
            }
        };

        var stack = new Stack<BreadcrumbInfoDto>();
        var walker = current;

        while (true)
        {
            if (walker.Id == RootFolderId)
            {
                break;
            }

            stack.Push(new BreadcrumbInfoDto
            {
                Id = walker.Id.ToString(),
                Name = walker.Name
            });

            if (!walker.ParentId.HasValue)
            {
                break;
            }

            if (!lookup.TryGetValue(walker.ParentId.Value, out walker))
            {
                break;
            }
        }

        while (stack.Count > 0)
        {
            path.Add(stack.Pop());
        }

        return path;
    }

    public async Task<(Stream Stream, string FolderName)> DownloadFolderAsync(Guid folderId, CancellationToken cancellationToken)
    {
        var folder = await _folderRepository.GetByIdAsync(folderId, cancellationToken)
                     ?? throw new DirectoryNotFoundException($"Folder '{folderId}' not found.");

        var memoryStream = new MemoryStream();

        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            await AddFolderToZipAsync(archive, folderId, folder.Name, cancellationToken);
        }

        memoryStream.Position = 0;
        return (memoryStream, folder.Name);
    }

    private async Task AddFolderToZipAsync(ZipArchive archive, Guid folderId, string currentPath, CancellationToken cancellationToken)
    {
        // add files
        var files = await _fileRepository.GetByFolderAsync(folderId, cancellationToken);

        foreach (var file in files)
        {
            var fileStream = await _fileStorage.OpenReadAsync(file.StoragePath, cancellationToken);
            if (fileStream == null) continue;

            var entryPath = $"{currentPath}/{file.Name}{file.Extension}";
            var entry = archive.CreateEntry(entryPath);

            using var entryStream = entry.Open();
            await fileStream.CopyToAsync(entryStream, cancellationToken);
        }

        // recurse sub folders
        var subFolders = await _folderRepository.GetChildrenAsync(folderId, cancellationToken);

        foreach (var folder in subFolders)
        {
            var newPath = $"{currentPath}/{folder.Name}";
            await AddFolderToZipAsync(archive, folder.Id, newPath, cancellationToken);
        }
    }

    private async Task SoftDeleteFolderTreeAsync(Guid folderId, CancellationToken cancellationToken)
    {
        var activeChildren = await _folderRepository.GetChildrenAsync(folderId, cancellationToken);
        var deletedChildren = await _folderRepository.GetDeletedChildrenAsync(folderId, cancellationToken);
        var children = activeChildren.Concat(deletedChildren)
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .ToArray();

        foreach (var child in children)
        {
            await SoftDeleteFolderTreeAsync(child.Id, cancellationToken);
        }

        var activeFiles = await _fileRepository.GetByFolderAsync(folderId, cancellationToken);
        var deletedFiles = await _fileRepository.GetDeletedByFolderAsync(folderId, cancellationToken);
        var files = activeFiles.Concat(deletedFiles)
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .ToArray();

        foreach (var file in files)
        {
            await _fileRepository.SoftDeleteAsync(file.Id, _userContext.UserId, cancellationToken);
        }

        await _folderRepository.SoftDeleteAsync(folderId, _userContext.UserId, cancellationToken);
    }

    private async Task RestoreFolderTreeAsync(Guid folderId, CancellationToken cancellationToken)
    {
        await _folderRepository.RestoreAsync(folderId, _userContext.UserId, cancellationToken);

        var files = await _fileRepository.GetDeletedByFolderAsync(folderId, cancellationToken);
        foreach (var file in files)
        {
            await _fileRepository.RestoreAsync(file.Id, _userContext.UserId, cancellationToken);
        }

        var children = await _folderRepository.GetDeletedChildrenAsync(folderId, cancellationToken);
        foreach (var child in children)
        {
            await RestoreFolderTreeAsync(child.Id, cancellationToken);
        }
    }

    private async Task DeleteFolderTreePermanentlyAsync(Guid folderId, CancellationToken cancellationToken)
    {
        var activeChildren = await _folderRepository.GetChildrenAsync(folderId, cancellationToken);
        var deletedChildren = await _folderRepository.GetDeletedChildrenAsync(folderId, cancellationToken);
        var children = activeChildren.Concat(deletedChildren)
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .ToArray();

        foreach (var child in children)
        {
            await DeleteFolderTreePermanentlyAsync(child.Id, cancellationToken);
        }

        var activeFiles = await _fileRepository.GetByFolderAsync(folderId, cancellationToken);
        var deletedFiles = await _fileRepository.GetDeletedByFolderAsync(folderId, cancellationToken);
        var files = activeFiles.Concat(deletedFiles)
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .ToArray();

        foreach (var file in files)
        {
            if (!string.IsNullOrWhiteSpace(file.StoragePath))
            {
                await _fileStorage.DeleteAsync(file.StoragePath, cancellationToken);
            }

            await _fileRepository.DeletePermanentlyAsync(file.Id, cancellationToken);
        }

        await _folderRepository.DeletePermanentlyAsync(folderId, cancellationToken);
    }

    /// <summary>
    /// Validates that the parent folder exists and that the current user has access to it.
    /// </summary>
    private async Task ValidateParentFolderAccessAsync(Guid normalizedParentFolderId, CancellationToken cancellationToken)
    {
        if (normalizedParentFolderId == RootFolderId)
        {
            return;
        }

        var parentFolder = await _folderRepository.GetByIdAsync(normalizedParentFolderId, cancellationToken)
                           ?? throw new FileNotFoundException($"Folder '{normalizedParentFolderId}' not found.");
    }

    private FolderTreeDto BuildFolderTree(
        Folder folder,
        IReadOnlyCollection<Folder> allFolders,
        IReadOnlyCollection<Domain.Entities.FileItem> allFiles,
        IReadOnlyDictionary<Guid, string> displayNameLookup)
    {
        var folderLookup = allFolders.ToLookup(x => x.ParentId);
        var fileLookup = allFiles.ToLookup(x => x.ParentFolderId);
        return BuildFolderTree(folder, folderLookup, fileLookup, displayNameLookup);
    }

    private static FolderTreeDto BuildFolderTree(
        Folder folder,
        ILookup<Guid?, Folder> folderLookup,
        ILookup<Guid?, Domain.Entities.FileItem> fileLookup,
        IReadOnlyDictionary<Guid, string> displayNameLookup)
    {
        var childFolders = folderLookup[folder.Id]
            .OrderBy(x => x.Name)
            .Select(x => BuildFolderTree(x, folderLookup, fileLookup, displayNameLookup))
            .ToArray();

        var childFiles = fileLookup[folder.Id]
            .OrderBy(x => x.Name)
            .Select(x => DtoMappingHelper.MapFile(x, displayNameLookup))
            .ToArray();

        return DtoMappingHelper.MapFolder(folder, childFolders, childFiles, displayNameLookup);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> BuildDisplayNameLookupAsync(
        IEnumerable<Folder> folders,
        IEnumerable<FileItem> files,
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
