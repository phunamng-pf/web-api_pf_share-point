using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharePoint.Api.Helper;
using SharePoint.Application.Abstractions;
using SharePoint.Application.Contracts;
using SharePoint.Application.Contracts.Request;
using SharePoint.Application.Contracts.Response;

namespace SharePoint.Api.Controllers;

public class UploadFormModel
{
    public IFormFile File { get; set; } = null!;
    public string? ParentFolderId { get; set; }
}

[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    private readonly IFileService _fileService;

    public FilesController(IFileService fileService)
    {
        _fileService = fileService;
    }

    [HttpPost]
    public async Task<ActionResult<FileItemViewDto>> Create([FromBody] ReqCreateFileDto request, CancellationToken cancellationToken)
    {
        var created = await _fileService.CreateFileMetadataAsync(request, cancellationToken);

        return CreatedAtAction(nameof(Get), new { parentFolderId = created.ParentFolderId }, created);
    }

    [HttpPost("upload")]
    [RequestSizeLimit(100_000_000)]
    public async Task<ActionResult<FileItemViewDto>> Upload([FromForm] UploadFormModel form, CancellationToken cancellationToken)
    {
        if (form.File.Length == 0)
        {
            return BadRequest("Empty file.");
        }

        var parentFolderId = StringHelper.NormalizeOptionalGuidOrRoot(form.ParentFolderId);

        await using var stream = form.File.OpenReadStream();
        var uploaded = await _fileService.UploadFileAsync(new ReqUploadFileDto
        {
            FileName = form.File.FileName,
            ContentType = form.File.ContentType,
            ParentFolderId = parentFolderId,
            Content = stream
        }, cancellationToken);

        return Ok(uploaded);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<FileItemViewDto>>> Get([FromQuery] string? parentFolderId, CancellationToken cancellationToken)
    {
        var normalizedParentFolderId = StringHelper.NormalizeOptionalGuidOrRoot(parentFolderId);
        var files = await _fileService.GetFilesAsync(normalizedParentFolderId, cancellationToken);
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

    [HttpPut]
    public async Task<ActionResult<FileItemViewDto>> Update([FromBody] ReqGuidNameDto request, CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
        {
            return BadRequest("File id is required.");
        }

        var updated = await _fileService.UpdateFileAsync(request, cancellationToken);
        return Ok(updated);
    }
}