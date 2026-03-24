using SharePoint.Application.Abstractions;
using SharePoint.Application.Contracts;
using SharePoint.Application.Contracts.Request;
using SharePoint.Application.Contracts.Response;
using SharePoint.Domain.Entities;

namespace SharePoint.Application.Services;

public class FolderService : IFolderService
{
    private readonly IFolderRepository _folderRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IUserContext _userContext;

    public FolderService(
        IFolderRepository folderRepository,
        IFileRepository fileRepository,
        IUserContext userContext)
    {
        _folderRepository = folderRepository;
        _fileRepository = fileRepository;
        _userContext = userContext;
    }

    public async Task<FolderTreeDto> CreateFolderAsync(ReqCreateFolderDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Folder name is required.", nameof(request.Name));
        }

        await ValidateParentFolderAccessAsync(request.ParentId, cancellationToken);

        var folder = new Folder
        {
            Name = request.Name.Trim(),
            ParentId = request.ParentId,
            CreatedByUserId = _userContext.UserId
        };

        var saved = await _folderRepository.AddAsync(folder, cancellationToken);
        return MapFolder(saved, Array.Empty<FolderTreeDto>(), Array.Empty<FileItemViewDto>());
    }

    public async Task<FolderTreeDto> GetFolderByIdAsync(Guid folderId, CancellationToken cancellationToken)
    {
        var folders = await _folderRepository.GetByUserAsync(_userContext.UserId, cancellationToken);
        var files = await _fileRepository.GetByUserAsync(_userContext.UserId, cancellationToken);

        var targetFolder = folders.FirstOrDefault(x => x.Id == folderId)
            ?? throw new FileNotFoundException($"Folder '{folderId}' not found.");

        return BuildFolderTree(targetFolder, folders, files);
    }

    public async Task<IReadOnlyCollection<FolderTreeDto>> GetFoldersAsync(Guid? parentId, CancellationToken cancellationToken)
    {
        var allFolders = await _folderRepository.GetByUserAsync(_userContext.UserId, cancellationToken);

        if (parentId.HasValue && allFolders.All(x => x.Id != parentId.Value))
        {
            throw new FileNotFoundException($"Folder '{parentId.Value}' not found.");
        }

        var folders = allFolders.Where(x => x.ParentId == parentId).OrderBy(x => x.Name).ToArray();
        return folders.Select(f => new FolderTreeDto
        {
            Id = f.Id.ToString(),
            Name = f.Name,
            ParentId = f.ParentId?.ToString(),
            CreatedAt = f.CreatedAtUtc,
            CreatedBy = f.CreatedByUserId.ToString(),
            ModifiedAt = f.ModifiedAtUtc,
            ModifiedBy = f.ModifiedByUserId?.ToString(),
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

        var folder = await _folderRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new FileNotFoundException($"Folder '{request.Id}' not found.");

        VerifyUserAccess(folder.CreatedByUserId);

        folder.Name = request.Name.Trim();
        folder.ModifiedAtUtc = DateTime.UtcNow;
        folder.ModifiedByUserId = _userContext.UserId;

        var updated = await _folderRepository.UpdateAsync(folder, cancellationToken);
        return MapFolder(updated, Array.Empty<FolderTreeDto>(), Array.Empty<FileItemViewDto>());
    }

    public async Task DeleteFolderAsync(Guid folderId, CancellationToken cancellationToken)
    {
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
                Id = "root",
                Name = "Documents"
            }
        };

        var stack = new Stack<BreadcrumbInfoDto>();
        var walker = current;

        while (true)
        {
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
        if (!parentFolderId.HasValue)
        {
            return;
        }

        var parentFolder = await _folderRepository.GetByIdAsync(parentFolderId.Value, cancellationToken)
                           ?? throw new FileNotFoundException($"Folder '{parentFolderId.Value}' not found.");

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

    private FolderTreeDto BuildFolderTree(Folder folder, IReadOnlyCollection<Folder> allFolders, IReadOnlyCollection<Domain.Entities.FileItem> allFiles)
    {
        var folderLookup = allFolders.ToLookup(x => x.ParentId);
        var fileLookup = allFiles.ToLookup(x => x.ParentFolderId);

        return BuildFolderTree(folder, folderLookup, fileLookup);
    }

    private static FolderTreeDto BuildFolderTree(
        Folder folder,
        ILookup<Guid?, Folder> folderLookup,
        ILookup<Guid?, Domain.Entities.FileItem> fileLookup)
    {
        var childFolders = folderLookup[folder.Id]
            .OrderBy(x => x.Name)
            .Select(x => BuildFolderTree(x, folderLookup, fileLookup))
            .ToArray();

        var childFiles = fileLookup[folder.Id]
            .OrderBy(x => x.Name)
            .Select(MapFile)
            .ToArray();

        return MapFolder(folder, childFolders, childFiles);
    }

    private static FolderTreeDto MapFolder(Folder folder, IReadOnlyCollection<FolderTreeDto> subFolders, IReadOnlyCollection<FileItemViewDto> files)
    {
        return new FolderTreeDto
        {
            Id = folder.Id.ToString(),
            Name = folder.Name,
            Files = files,
            SubFolders = subFolders,
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
