using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharePoint.Application.Contracts;
using SharePoint.Application.Abstractions;

namespace SharePoint.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/folders")]
public sealed class FoldersController : ControllerBase
{
    private readonly IFolderService _folderService;

    public FoldersController(IFolderService folderService)
    {
        _folderService = folderService;
    }

    [HttpPost]
    public async Task<ActionResult<FolderDto>> Create([FromBody] CreateFolderRequest request, CancellationToken cancellationToken)
    {
        var folder = await _folderService.CreateFolderAsync(new CreateFolderRequest(request.Name, request.ParentId), cancellationToken);
        return CreatedAtAction(nameof(Get), new { parentId = folder.ParentId }, folder);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<FolderDto>>> Get([FromQuery] Guid? parentId, CancellationToken cancellationToken)
    {
        var folders = await _folderService.GetFoldersAsync(parentId, cancellationToken);
        return Ok(folders);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _folderService.DeleteFolderAsync(id, cancellationToken);
        return NoContent();
    }
}
