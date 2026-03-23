namespace SharePoint.Application.Abstractions;

public interface IFileStorage
{
    Task<string> SaveAsync(Stream fileStream, string extension, CancellationToken cancellationToken);
    Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken);
}
