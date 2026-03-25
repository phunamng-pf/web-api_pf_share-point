using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharePoint.Application.Contracts.Request
{
    public class ReqUploadFileDto
    {
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public Guid? ParentFolderId { get; set; }
        public long FileSize { get; set; }
        public Stream Content { get; set; } = Stream.Null;
    }
}
