using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharePoint.Application.Contracts;
using SharePoint.Application.Abstractions;

namespace SharePoint.Api.Controllers;

public sealed class UploadFormModel
{
    public IFormFile File { get; set; } = null!;
    public Guid? ParentFolderId { get; set; }
}

[ApiController]
[Authorize]
[Route("api/files")]
public sealed class FilesController : ControllerBase
{
    private readonly IFileService _fileService;

    public FilesController(IFileService fileService)
    {
        _fileService = fileService;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(100_000_000)]
    public async Task<ActionResult<FileItemDto>> Upload([FromForm] UploadFormModel form, CancellationToken cancellationToken)
    {
        if (form.File.Length == 0)
        {
            return BadRequest("Empty file.");
        }

        await using var stream = form.File.OpenReadStream();
        var uploaded = await _fileService.UploadFileAsync(
            new UploadFileRequest(form.File.FileName, form.File.ContentType, form.ParentFolderId, stream),
            cancellationToken);

        return Ok(uploaded);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<FileItemDto>>> Get([FromQuery] Guid? parentFolderId, CancellationToken cancellationToken)
    {
        var files = await _fileService.GetFilesAsync(parentFolderId, cancellationToken);
        return Ok(files);
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var (stream, file) = await _fileService.DownloadFileAsync(id, cancellationToken);
        return File(stream, file.ContentType, $"{file.Name}{file.Extension}");
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _fileService.DeleteFileAsync(id, cancellationToken);
        return NoContent();
    }
}
