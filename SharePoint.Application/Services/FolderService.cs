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
        var displayNameLookup = await BuildDisplayNameLookupAsync(new[] { saved }, Array.Empty<Domain.Entities.FileItem>(), cancellationToken);
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

    public async Task<IReadOnlyCollection<FolderTreeDto>> GetFoldersAsync(Guid? parentId, CancellationToken cancellationToken)
    {
        var normalizedParentId = DtoMappingHelper.NormalizeParentFolderId(parentId);
        var allFolders = await _folderRepository.GetByUserAsync(_userContext.UserId, cancellationToken);

        if (allFolders.All(x => x.Id != normalizedParentId))
        {
            throw new FileNotFoundException($"Folder '{normalizedParentId}' not found.");
        }

        var folders = allFolders.Where(x => x.ParentId == normalizedParentId).OrderBy(x => x.Name).ToArray();
        var displayNameLookup = await BuildDisplayNameLookupAsync(folders, Array.Empty<Domain.Entities.FileItem>(), cancellationToken);

        return folders.Select(f => DtoMappingHelper.MapFolder(f, Array.Empty<FolderTreeDto>(), Array.Empty<FileItemViewDto>(), displayNameLookup)).ToArray();
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

        VerifyUserAccess(folder.CreatedByUserId);

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

        VerifyUserAccess(folder.CreatedByUserId);
        await _folderRepository.SoftDeleteAsync(folderId, cancellationToken);
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

        VerifyUserAccess(folder.CreatedByUserId);

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
            VerifyUserAccess(file.CreatedByUserId);

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
            VerifyUserAccess(folder.CreatedByUserId);

            var newPath = $"{currentPath}/{folder.Name}";
            await AddFolderToZipAsync(archive, folder.Id, newPath, cancellationToken);
        }
    }

    /// <summary>
    /// Validates that the parent folder exists and that the current user has access to it.
    /// </summary>
    private async Task ValidateParentFolderAccessAsync(Guid? parentFolderId, CancellationToken cancellationToken)
    {
        var normalizedParentFolderId = DtoMappingHelper.NormalizeParentFolderId(parentFolderId);

        if (normalizedParentFolderId == RootFolderId)
        {
            return;
        }

        var parentFolder = await _folderRepository.GetByIdAsync(normalizedParentFolderId, cancellationToken)
                           ?? throw new FileNotFoundException($"Folder '{normalizedParentFolderId}' not found.");

        VerifyUserAccess(parentFolder.CreatedByUserId);
    }
    /// <summary>
    /// Verifies that the resource owner (createdByUserId) matches the current user.
    /// Throws UnauthorizedAccessException if access is denied.
    /// </summary>
    private void VerifyUserAccess(Guid createdByUserId)
    {
        if (createdByUserId != _userContext.UserId)
        {
            throw new UnauthorizedAccessException("You do not have permission to access this resource.");
        }
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
