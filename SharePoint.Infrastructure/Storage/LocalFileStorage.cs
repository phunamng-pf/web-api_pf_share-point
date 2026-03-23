using Microsoft.Extensions.Options;
using SharePoint.Application.Abstractions;
using SharePoint.Infrastructure.Options;

namespace SharePoint.Infrastructure.Storage;

public sealed class LocalFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    private readonly string _rootPath = Path.GetFullPath(options.Value.RootPath);

    public async Task<string> SaveAsync(Stream fileStream, string extension, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_rootPath);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(_rootPath, fileName);

        await using var output = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await fileStream.CopyToAsync(output, cancellationToken);

        return fileName;
    }

    public Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken)
    {
        var fullPath = Path.Combine(_rootPath, storagePath);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }
}
