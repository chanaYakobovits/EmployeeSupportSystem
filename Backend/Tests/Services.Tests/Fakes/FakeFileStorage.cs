using System.Collections.Concurrent;
using Common.Interfaces;

namespace Services.Tests.Fakes;

/// <summary>Attachment contents in memory. Records deletions, so tests can check that staged files were removed.</summary>
public sealed class FakeFileStorage : IFileStorage
{
    public ConcurrentDictionary<string, byte[]> Files { get; } = new();

    public List<string> Deleted { get; } = [];

    public async Task<StoredFile> SaveAsync(Stream content, long maxBytes, CancellationToken cancellationToken)
    {
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);
        if (copy.Length > maxBytes)
        {
            throw new InvalidDataException("Too large.");
        }

        var name = Guid.NewGuid().ToString("N");
        Files[name] = copy.ToArray();
        return new StoredFile(name, copy.Length);
    }

    public Task<Stream> OpenReadAsync(string storedName, CancellationToken cancellationToken) =>
        Files.TryGetValue(storedName, out var bytes)
            ? Task.FromResult<Stream>(new MemoryStream(bytes, writable: false))
            : throw new FileNotFoundException();

    public Task DeleteAsync(string storedName)
    {
        Files.TryRemove(storedName, out _);
        Deleted.Add(storedName);
        return Task.CompletedTask;
    }

    public Task ClearAllAsync(CancellationToken cancellationToken)
    {
        Files.Clear();
        return Task.CompletedTask;
    }
}
