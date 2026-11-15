namespace Common.Interfaces;

/// <summary>
/// Storage of attachment contents (spec 6.3): outside wwwroot, under a random name. The original file name lives only in
/// the database. Stored names are opaque ids created by <see cref="SaveAsync"/>; any other value is rejected.
/// Attachments are never sent to the AI (spec 4.3, 5.1).
/// </summary>
public interface IFileStorage
{
    /// <summary>Writes the content under a new random name and returns that name with the number of bytes written.</summary>
    /// <exception cref="InvalidDataException">The content is longer than <paramref name="maxBytes"/> (nothing is kept).</exception>
    Task<StoredFile> SaveAsync(Stream content, long maxBytes, CancellationToken cancellationToken);

    /// <exception cref="FileNotFoundException">No such stored file.</exception>
    /// <exception cref="ArgumentException"><paramref name="storedName"/> is not a stored name.</exception>
    Task<Stream> OpenReadAsync(string storedName, CancellationToken cancellationToken);

    /// <summary>Deletes the file if it exists.</summary>
    Task DeleteAsync(string storedName);

    /// <summary>Deletes every stored file. Only for the Development seeder, which recreates the database.</summary>
    Task ClearAllAsync(CancellationToken cancellationToken);
}

/// <param name="Name">The stored (random) name – kept in TicketAttachments.StoredFileName.</param>
/// <param name="SizeBytes">Bytes actually written (not the length the client declared).</param>
public sealed record StoredFile(string Name, long SizeBytes);
