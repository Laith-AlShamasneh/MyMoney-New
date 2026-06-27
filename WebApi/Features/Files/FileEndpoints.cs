using Application.Interfaces.Services;

namespace WebApi.Features.Files;

public static class FileEndpoints
{
    public static void MapFileEndpoints(this WebApplication app)
    {
        // Anonymous but capability-protected. The signed link is only ever minted
        // for a user who was authorized to see the file (the originating response
        // was produced inside an ownership-scoped query). The HMAC signature makes
        // the URL unforgeable and the embedded expiry makes it self-revoking, so a
        // plain <img src> can load it without exposing the folder to static serving.
        app.MapGet("/api/files/view", async (
            string            k,
            long              e,
            string            s,
            IFileLinkService  linkService,
            IFileService      fileService,
            CancellationToken ct) =>
        {
            if (!linkService.TryValidate(k, e, s))
                return Results.NotFound();

            // Defense in depth: a valid signature implies a server-minted key, but
            // reject traversal sequences regardless.
            if (k.Contains("..", StringComparison.Ordinal))
                return Results.NotFound();

            if (!await fileService.ExistsAsync(k, ct))
                return Results.NotFound();

            var stream = await fileService.DownloadAsync(k, ct);
            return Results.Stream(stream, ResolveContentType(k), enableRangeProcessing: true);
        })
        .AllowAnonymous()
        .WithTags("Files");
    }

    private static string ResolveContentType(string key) => Path.GetExtension(key).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png"            => "image/png",
        ".gif"            => "image/gif",
        ".webp"           => "image/webp",
        ".heic"           => "image/heic",
        ".pdf"            => "application/pdf",
        _                 => "application/octet-stream"
    };
}
