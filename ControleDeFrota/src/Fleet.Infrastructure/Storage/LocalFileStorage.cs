using Fleet.Application.Common;
using Microsoft.Extensions.Options;

namespace Fleet.Infrastructure.Storage;

public sealed class FileStorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Root folder of the local storage. Relative paths are resolved from the application base directory.</summary>
    public string LocalRootPath { get; set; } = "App_Data/files";
}

/// <summary>
/// ADR-022: files on the local disk, outside the web root (never served statically). Swapping to object/cloud
/// storage means another IFileStorage — nothing else changes.
/// </summary>
public sealed class LocalFileStorage(IOptions<FileStorageOptions> options) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(Path.IsPathRooted(options.Value.LocalRootPath)
        ? options.Value.LocalRootPath
        : Path.Combine(AppContext.BaseDirectory, options.Value.LocalRootPath));

    public async Task SaveAsync(string key, Stream content, CancellationToken ct)
    {
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, ct);
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken ct)
    {
        var path = Resolve(key);
        if (!File.Exists(path)) throw new FileNotFoundException("Stored file is missing.", key);
        return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true));
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        var path = Resolve(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    /// <summary>Keys are server-generated, but the path is still confined to the root (defense in depth against traversal).</summary>
    private string Resolve(string key)
    {
        var path = Path.GetFullPath(Path.Combine(_root, key));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid storage key.");
        return path;
    }
}
