using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharePoint.Application.Abstractions;
using SharePoint.Application.Contracts;

namespace SharePoint.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/documents")]
public sealed class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;

    public DocumentsController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpGet]
    public async Task<ActionResult<DocumentsResponse>> Get([FromQuery] Guid? parentId, CancellationToken cancellationToken)
    {
        var documents = await _documentService.GetDocumentsAsync(parentId, cancellationToken);
        return Ok(documents);
    }
}
