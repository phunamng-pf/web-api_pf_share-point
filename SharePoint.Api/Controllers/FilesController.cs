using Microsoft.AspNetCore.Mvc;
using SharePoint.Application.Contracts;
using SharePoint.Application.Services;

namespace SharePoint.Api.Controllers;

[ApiController]
[Route("api/files")]
public sealed class FilesController(IDocumentService documentService) : ControllerBase
{
    //[HttpPost("upload")]
    //[RequestSizeLimit(100_000_000)]
    //public async Task<IActionResult> Upload([FromForm] IFormFile file, [FromForm] Guid? parentFolderId, CancellationToken cancellationToken)
    //{
    //    if (file.Length == 0)
    //    {
    //        return BadRequest("Empty file.");
    //    }

    //    await using var stream = file.OpenReadStream();
    //    var uploaded = await documentService.UploadFileAsync(
    //        new UploadFileRequest(file.FileName, file.ContentType, parentFolderId, stream),
    //        cancellationToken);

    //    return Ok(uploaded);
    //}

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] Guid? parentFolderId, CancellationToken cancellationToken)
    {
        var files = await documentService.GetFilesAsync(parentFolderId, cancellationToken);
        return Ok(files);
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var (stream, file) = await documentService.DownloadFileAsync(id, cancellationToken);
        return File(stream, file.ContentType, $"{file.Name}{file.Extension}");
    }
}
