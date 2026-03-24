using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharePoint.Api.Helper;
using SharePoint.Application.Abstractions;
using SharePoint.Application.Contracts;
using SharePoint.Application.Contracts.Response;

namespace SharePoint.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/documents")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;

    public DocumentsController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpGet("me")]
    public async Task<ActionResult<FolderTreeDto>> GetMyDocuments(CancellationToken cancellationToken)
    {
        var documents = await _documentService.GetMyDocumentsAsync(cancellationToken);
        return Ok(documents);
    }
}
