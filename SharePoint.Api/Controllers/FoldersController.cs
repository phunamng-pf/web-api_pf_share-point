using Microsoft.AspNetCore.Mvc;
using SharePoint.Api.Contracts;
using SharePoint.Application.Contracts;
using SharePoint.Application.Services;

namespace SharePoint.Api.Controllers;

[ApiController]
[Route("api/folders")]
public sealed class FoldersController(IDocumentService documentService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFolderHttpRequest request, CancellationToken cancellationToken)
    {
        var folder = await documentService.CreateFolderAsync(new CreateFolderRequest(request.Name, request.ParentId), cancellationToken);
        return Ok(folder);
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] Guid? parentId, CancellationToken cancellationToken)
    {
        var folders = await documentService.GetFoldersAsync(parentId, cancellationToken);
        return Ok(folders);
    }
}
