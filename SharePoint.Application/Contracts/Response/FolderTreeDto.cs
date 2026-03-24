using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharePoint.Application.Contracts.Response
{
    public class FolderTreeDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public IReadOnlyCollection<FileItemViewDto> Files { get; set; } = Array.Empty<FileItemViewDto>();
        public IReadOnlyCollection<FolderTreeDto> SubFolders { get; set; } = Array.Empty<FolderTreeDto>();
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? ModifiedAt { get; set; }
        public string? ModifiedBy { get; set; }
        public string? ParentId { get; set; }
    }
}
