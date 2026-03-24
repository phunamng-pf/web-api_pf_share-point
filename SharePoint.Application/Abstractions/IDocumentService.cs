using SharePoint.Application.Contracts;
using SharePoint.Application.Contracts.Response;

namespace SharePoint.Application.Abstractions;

public interface IDocumentService
{
    Task<FolderTreeDto> GetMyDocumentsAsync(CancellationToken cancellationToken);
}
