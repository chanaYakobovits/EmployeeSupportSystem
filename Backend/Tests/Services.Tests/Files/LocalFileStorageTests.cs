using Services.Implementations.Files;
using Services.Tests.Fakes;

namespace Services.Tests.Files;

/// <summary>The disk storage of attachments, in a temporary directory.</summary>
public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ess-tests-" + Guid.NewGuid().ToString("N"));
    private readonly LocalFileStorage _storage;

    public LocalFileStorageTests() => _storage = new LocalFileStorage(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task A_file_is_saved_under_a_random_name_without_extension_read_back_and_deleted()
    {
        var stored = await _storage.SaveAsync(new MemoryStream(TestFiles.Pdf), 1024, CancellationToken.None);

        Assert.Matches("^[0-9a-f]{32}$", stored.Name);
        Assert.Equal(TestFiles.Pdf.Length, stored.SizeBytes);
        Assert.Equal([stored.Name], Directory.GetFiles(_root).Select(Path.GetFileName));

        await using (var content = await _storage.OpenReadAsync(stored.Name, CancellationToken.None))
        {
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy);
            Assert.Equal(TestFiles.Pdf, copy.ToArray());
        }

        await _storage.DeleteAsync(stored.Name);
        Assert.Empty(Directory.GetFiles(_root));
        await _storage.DeleteAsync(stored.Name); // deleting again is not an error
    }

    [Fact]
    public async Task Content_over_the_limit_is_not_kept()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            _storage.SaveAsync(new MemoryStream(new byte[2049]), 2048, CancellationToken.None));

        Assert.Empty(Directory.GetFiles(_root));
    }

    [Theory]
    [InlineData("../secrets.txt")]
    [InlineData("..\\appsettings.json")]
    [InlineData("C:\\Windows\\win.ini")]
    [InlineData("0123456789abcdef0123456789abcdef.exe")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("")]
    public async Task Only_stored_names_reach_the_file_system(string name)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.OpenReadAsync(name, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.DeleteAsync(name));
    }

    [Fact]
    public async Task A_missing_stored_file_is_not_found()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _storage.OpenReadAsync(Guid.NewGuid().ToString("N"), CancellationToken.None));
    }

    [Fact]
    public async Task Clearing_removes_stored_files_only()
    {
        await _storage.SaveAsync(new MemoryStream(TestFiles.Png), 1024, CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(_root, "keep.txt"), "not ours");

        await _storage.ClearAllAsync(CancellationToken.None);

        Assert.Equal(["keep.txt"], Directory.GetFiles(_root).Select(Path.GetFileName));
    }

    [Fact]
    public void The_directory_must_be_absolute()
    {
        Assert.Throws<ArgumentException>(() => new LocalFileStorage("App_Data/attachments"));
    }
}
