using System.Text.RegularExpressions;
using Common.Interfaces;

namespace Services.Implementations.Files;

/// <summary>
/// Attachments on the local disk, in one directory outside wwwroot (spec 6.3). Each file gets a random name with no
/// extension, so nothing on the server ever treats it as something to run or serve. Stored names are checked against
/// their exact format before any path is built – no other value can reach the file system.
/// </summary>
public sealed partial class LocalFileStorage : IFileStorage
{
    private const int BufferSize = 81920;

    private readonly string _root;

    /// <param name="rootDirectory">Absolute path of the attachments directory (created on first write).</param>
    public LocalFileStorage(string rootDirectory)
    {
        if (!Path.IsPathFullyQualified(rootDirectory))
        {
            throw new ArgumentException("The attachments directory must be an absolute path.", nameof(rootDirectory));
        }

        _root = Path.GetFullPath(rootDirectory);
    }

    public async Task<StoredFile> SaveAsync(Stream content, long maxBytes, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_root);
        var name = Guid.NewGuid().ToString("N");
        var path = PathOf(name);

        try
        {
            long written = 0;
            await using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             BufferSize, useAsync: true))
            {
                var buffer = new byte[BufferSize];
                int read;
                while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    written += read;
                    // Counted here too, not only from the declared length: the content is what is written.
                    if (written > maxBytes)
                    {
                        throw new InvalidDataException("The content is larger than allowed.");
                    }

                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }

            return new StoredFile(name, written);
        }
        catch
        {
            File.Delete(path);
            throw;
        }
    }

    public Task<Stream> OpenReadAsync(string storedName, CancellationToken cancellationToken)
    {
        var path = PathOf(storedName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The stored file does not exist.");
        }

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storedName)
    {
        File.Delete(PathOf(storedName)); // no error if it does not exist
        return Task.CompletedTask;
    }

    public Task ClearAllAsync(CancellationToken cancellationToken)
    {
        if (Directory.Exists(_root))
        {
            // Only files this storage created – never anything else that may have been put in the directory.
            foreach (var path in Directory.EnumerateFiles(_root).Where(p => StoredNamePattern().IsMatch(Path.GetFileName(p))))
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Delete(path);
            }
        }

        return Task.CompletedTask;
    }

    private string PathOf(string storedName)
    {
        if (!StoredNamePattern().IsMatch(storedName))
        {
            throw new ArgumentException("Not a stored file name.", nameof(storedName));
        }

        return Path.Combine(_root, storedName);
    }

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex StoredNamePattern();
}
