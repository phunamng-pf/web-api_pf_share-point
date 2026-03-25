using SharePoint.Application.Contracts.Response;
using SharePoint.Domain.Entities;

namespace SharePoint.Application.Helper;

public static class DtoMappingHelper
{
    public static Guid NormalizeParentFolderId(Guid? parentFolderId)
    {
        return parentFolderId ?? Guid.Empty;
    }

    public static string ResolveDisplayName(Guid userId, IReadOnlyDictionary<Guid, string> displayNameLookup)
    {
        if (userId == Guid.Empty)
        {
            return "System";
        }
        return displayNameLookup.TryGetValue(userId, out var displayName)
            ? displayName
            : userId.ToString();
    }

    public static string? ResolveDisplayName(Guid? userId, IReadOnlyDictionary<Guid, string> displayNameLookup)
    {
        if (!userId.HasValue)
        {
            return null;
        }
        return ResolveDisplayName(userId.Value, displayNameLookup);
    }

    public static FileItemViewDto MapFile(FileItem file, IReadOnlyDictionary<Guid, string> displayNameLookup)
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

    public static FolderTreeDto MapFolder(Folder folder, IReadOnlyCollection<FolderTreeDto> subFolders, IReadOnlyCollection<FileItemViewDto> files, IReadOnlyDictionary<Guid, string> displayNameLookup)
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
}
