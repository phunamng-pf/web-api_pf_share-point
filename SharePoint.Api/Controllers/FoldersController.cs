using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharePoint.Api.Helper;
using SharePoint.Application.Abstractions;
using SharePoint.Application.Contracts;
using SharePoint.Application.Contracts.Request;
using SharePoint.Application.Contracts.Response;

namespace SharePoint.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/folders")]
public class FoldersController : ControllerBase
{
    private readonly IFolderService _folderService;

    public FoldersController(IFolderService folderService)
    {
        _folderService = folderService;
    }

    [HttpPost]
    public async Task<ActionResult<FolderTreeDto>> Create([FromBody] ReqCreateFolderDto request, CancellationToken cancellationToken)
    {
        var folder = await _folderService.CreateFolderAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = folder.Id }, folder);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FolderTreeDto>> GetById(string id, CancellationToken cancellationToken)
    {
        if (StringHelper.IsRoot(id))
        {
            return BadRequest("Use /api/documents/me for root.");
        }

        var folderId = StringHelper.ParseRequiredGuid(id, nameof(id));
        var folder = await _folderService.GetFolderByIdAsync(folderId, cancellationToken);
        return Ok(folder);
    }

    [HttpPut]
    public async Task<ActionResult<FolderTreeDto>> Update([FromBody] ReqGuidNameDto request, CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
        {
            return BadRequest("Root folder cannot be modified.");
        }

        var updated = await _folderService.UpdateFolderAsync(request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        if (StringHelper.IsRoot(id))
        {
            return BadRequest("Root folder cannot be deleted.");
        }

        var folderId = StringHelper.ParseRequiredGuid(id, nameof(id));
        await _folderService.DeleteFolderAsync(folderId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id}/breadcrumb")]
    public async Task<ActionResult<IReadOnlyCollection<BreadcrumbInfoDto>>> GetBreadcrumb(string id, CancellationToken cancellationToken)
    {
        if (StringHelper.IsRoot(id))
        {
            return Ok(new[]
            {
                new BreadcrumbInfoDto
                {
                    Id = Guid.Empty.ToString(),
                    Name = "Documents"
                }
            });
        }

        var folderId = StringHelper.ParseRequiredGuid(id, nameof(id));
        var breadcrumb = await _folderService.GetBreadcrumbAsync(folderId, cancellationToken);
        return Ok(breadcrumb);
    }

    //[HttpGet]
    //public async Task<ActionResult<IReadOnlyCollection<FolderTreeDto>>> Get([FromQuery] string? parentId, CancellationToken cancellationToken)
    //{
    //    var normalizedParentId = StringHelper.NormalizeOptionalGuidOrRoot(parentId);
    //    var folders = await _folderService.GetFoldersAsync(normalizedParentId, cancellationToken);
    //    return Ok(folders);
    //}
}
