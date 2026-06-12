using Application.Interfaces.Services;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Services.Storage;

internal sealed class LocalFileService(IHostEnvironment environment) : IFileService
{
    private string GetPhysicalPath(string key) =>
        Path.Combine(environment.ContentRootPath, "wwwroot", "uploads", key.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

    public async Task UploadAsync(Stream stream, string key, string contentType, CancellationToken ct = default)
    {
        var path = GetPhysicalPath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await stream.CopyToAsync(fs, ct);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = GetPhysicalPath(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default) =>
        Task.FromResult(File.Exists(GetPhysicalPath(key)));

    public Task<Stream> DownloadAsync(string key, CancellationToken ct = default) =>
        Task.FromResult<Stream>(new FileStream(GetPhysicalPath(key), FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true));
}
