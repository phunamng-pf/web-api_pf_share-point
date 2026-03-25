using SharePoint.Application.Abstractions;
using SharePoint.Application.Contracts;
using SharePoint.Application.Contracts.Request;
using SharePoint.Application.Contracts.Response;
using SharePoint.Domain.Entities;

namespace SharePoint.Application.Services;

public class FolderService : IFolderService
{
    private static readonly Guid RootFolderId = Guid.Empty;

    private readonly IFolderRepository _folderRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUserContext _userContext;

    public FolderService(
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

    public async Task<FolderTreeDto> CreateFolderAsync(ReqCreateFolderDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Folder name is required.", nameof(request.Name));
        }

        var normalizedParentId = NormalizeParentFolderId(request.ParentId);
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
        return MapFolder(saved, Array.Empty<FolderTreeDto>(), Array.Empty<FileItemViewDto>(), displayNameLookup);
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
        var normalizedParentId = NormalizeParentFolderId(parentId);
        var allFolders = await _folderRepository.GetByUserAsync(_userContext.UserId, cancellationToken);

        if (allFolders.All(x => x.Id != normalizedParentId))
        {
            throw new FileNotFoundException($"Folder '{normalizedParentId}' not found.");
        }

        var folders = allFolders.Where(x => x.ParentId == normalizedParentId).OrderBy(x => x.Name).ToArray();
        var displayNameLookup = await BuildDisplayNameLookupAsync(folders, Array.Empty<Domain.Entities.FileItem>(), cancellationToken);

        return folders.Select(f => new FolderTreeDto
        {
            Id = f.Id.ToString(),
            Name = f.Name,
            ParentId = f.ParentId?.ToString(),
            CreatedAt = f.CreatedAt,
            CreatedBy = ResolveDisplayName(f.CreatedByUserId, displayNameLookup),
            ModifiedAt = f.ModifiedAt,
            ModifiedBy = ResolveDisplayName(f.ModifiedByUserId, displayNameLookup),
            Files = Array.Empty<FileItemViewDto>(),
            SubFolders = Array.Empty<FolderTreeDto>()
        }).ToArray();
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
        return MapFolder(updated, Array.Empty<FolderTreeDto>(), Array.Empty<FileItemViewDto>(), displayNameLookup);
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

    /// <summary>
    /// Validates that the parent folder exists and that the current user has access to it.
    /// </summary>
    private async Task ValidateParentFolderAccessAsync(Guid? parentFolderId, CancellationToken cancellationToken)
    {
        var normalizedParentFolderId = NormalizeParentFolderId(parentFolderId);

        if (normalizedParentFolderId == RootFolderId)
        {
            return;
        }

        var parentFolder = await _folderRepository.GetByIdAsync(normalizedParentFolderId, cancellationToken)
                           ?? throw new FileNotFoundException($"Folder '{normalizedParentFolderId}' not found.");

        VerifyUserAccess(parentFolder.CreatedByUserId);
    }

    private static Guid NormalizeParentFolderId(Guid? parentFolderId)
    {
        return parentFolderId ?? RootFolderId;
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
            .Select(x => MapFile(x, displayNameLookup))
            .ToArray();

        return MapFolder(folder, childFolders, childFiles, displayNameLookup);
    }

    private static FolderTreeDto MapFolder(
        Folder folder,
        IReadOnlyCollection<FolderTreeDto> subFolders,
        IReadOnlyCollection<FileItemViewDto> files,
        IReadOnlyDictionary<Guid, string> displayNameLookup)
    {
        return new FolderTreeDto
        {
            Id = folder.Id.ToString(),
            Name = folder.Name,
            Files = files,
            SubFolders = subFolders,
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
