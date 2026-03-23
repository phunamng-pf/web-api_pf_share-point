using SharePoint.Application.Contracts;

namespace SharePoint.Application.Abstractions;

public interface IDocumentService
{
    Task<DocumentsResponse> GetDocumentsAsync(Guid? parentId, CancellationToken cancellationToken);
}
