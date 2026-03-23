using SharePoint.Application.Abstractions;
using SharePoint.Application.Contracts;
using SharePoint.Domain.Entities;

namespace SharePoint.Application.Services;

public sealed class FolderService : IFolderService
{
    private readonly IFolderRepository _folderRepository;
    private readonly IUserContext _userContext;

    public FolderService(
        IFolderRepository folderRepository,
        IUserContext userContext)
    {
        _folderRepository = folderRepository;
        _userContext = userContext;
    }

    public async Task<FolderDto> CreateFolderAsync(CreateFolderRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Folder name is required.", nameof(request));
        }

        await ValidateParentFolderAccessAsync(request.ParentId, cancellationToken);

        var folder = new Folder
        {
            Name = request.Name.Trim(),
            ParentId = request.ParentId,
            CreatedByUserId = _userContext.UserId
        };

        var saved = await _folderRepository.AddAsync(folder, cancellationToken);
        return new FolderDto(saved.Id, saved.Name, saved.ParentId, saved.CreatedAtUtc);
    }

    public async Task<IReadOnlyCollection<FolderDto>> GetFoldersAsync(Guid? parentId, CancellationToken cancellationToken)
    {
        await ValidateParentFolderAccessAsync(parentId, cancellationToken);

        var folders = await _folderRepository.GetChildrenAsync(parentId, cancellationToken);
        return folders.Select(f => new FolderDto(f.Id, f.Name, f.ParentId, f.CreatedAtUtc)).ToArray();
    }

    public async Task DeleteFolderAsync(Guid folderId, CancellationToken cancellationToken)
    {
        var folder = await _folderRepository.GetByIdAsync(folderId, cancellationToken)
                     ?? throw new FileNotFoundException($"Folder '{folderId}' not found.");

        VerifyUserAccess(folder.CreatedByUserId);
        await _folderRepository.SoftDeleteAsync(folderId, cancellationToken);
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
}
