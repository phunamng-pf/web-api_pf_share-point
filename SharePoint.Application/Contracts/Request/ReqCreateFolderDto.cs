using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharePoint.Application.Contracts.Request
{
    public class ReqCreateFolderDto
    {
        public string Name { get; set; } = string.Empty;
        public Guid? ParentId { get; set; }
    }
}
