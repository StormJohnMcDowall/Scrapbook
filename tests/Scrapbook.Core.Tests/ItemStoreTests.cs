using System.Text;

namespace Scrapbook.Core.Tests;

public class ItemStoreTests
{
    private static ItemData Item(string body, bool masked = false, string description = "desc") =>
        new(Guid.NewGuid(), description, body, masked);

    [Fact]
    public void Load_WhenNoFiles_ReturnsNotFoundAndEmpty()
    {
        using var dir = new TempDir();
        var store = new ItemStore(dir.Path);

        var r = store.Load();

        Assert.Equal(LoadStatus.NotFound, r.Status);
        Assert.Empty(r.Items);
        Assert.True(store.CanSave);
    }

    [Fact]
    public void Save_CreatesDirectoryIfMissing()
    {
        using var dir = new TempDir();
        var store = new ItemStore(Path.Combine(dir.Path, "nested"));

        store.Save([Item("a")]);

        Assert.True(File.Exists(store.ItemsPath));
    }

    [Fact]
    public void SaveThenLoad_RoundTripsItemsInOrder()
    {
        using var dir = new TempDir();
        ItemData[] items = [Item("https://demo.example"), Item("admin"), Item("hunter2", masked: true)];

        new ItemStore(dir.Path).Save(items);
        var r = new ItemStore(dir.Path).Load();

        Assert.Equal(LoadStatus.Loaded, r.Status);
        Assert.Equal(items, r.Items);
    }

    [Fact]
    public void SaveThenLoad_PreservesTextExactly()
    {
        using var dir = new TempDir();
        string[] bodies = ["  pa ss\t ", "line1\r\nline2", "pässwörd🔑", new string('x', 10_000)];

        new ItemStore(dir.Path).Save(bodies.Select(b => Item(b)).ToList());
        var loaded = new ItemStore(dir.Path).Load().Items;

        Assert.Equal(bodies.Length, loaded.Count);
        for (var i = 0; i < bodies.Length; i++)
            Assert.Equal(bodies[i], loaded[i].Body);
    }

    [Fact]
    public void Save_DoesNotWritePlaintext()
    {
        using var dir = new TempDir();
        var store = new ItemStore(dir.Path);

        store.Save([Item("BodyMarker456", description: "DescMarker123")]);
        var bytes = File.ReadAllBytes(store.ItemsPath);

        foreach (var marker in new[] { "DescMarker123", "BodyMarker456" })
        {
            Assert.False(Contains(bytes, Encoding.UTF8.GetBytes(marker)), $"{marker} found as UTF-8");
            Assert.False(Contains(bytes, Encoding.Unicode.GetBytes(marker)), $"{marker} found as UTF-16");
        }
    }

    [Fact]
    public void Save_SecondSave_KeepsPreviousVersionAsBackup()
    {
        using var dir = new TempDir();
        var store = new ItemStore(dir.Path);
        var a = Item("A");

        store.Save([a]);
        store.Save([Item("B")]);
        File.Delete(store.ItemsPath);
        var r = new ItemStore(dir.Path).Load();

        Assert.Equal(LoadStatus.LoadedFromBackup, r.Status);
        Assert.Equal([a], r.Items);
    }

    [Fact]
    public void Load_WhenMainFileCorrupt_FallsBackToBackup()
    {
        using var dir = new TempDir();
        var store = new ItemStore(dir.Path);
        var a = Item("A");
        store.Save([a]);
        store.Save([Item("B")]);
        File.WriteAllBytes(store.ItemsPath, [1, 2, 3]);

        var fresh = new ItemStore(dir.Path);
        var r = fresh.Load();

        Assert.Equal(LoadStatus.LoadedFromBackup, r.Status);
        Assert.Equal([a], r.Items);
        Assert.True(fresh.CanSave);
    }

    [Fact]
    public void Load_WhenBothFilesCorrupt_FailsAndNeverWrites()
    {
        using var dir = new TempDir();
        var store = new ItemStore(dir.Path);
        byte[] junkMain = [1, 2, 3], junkBak = [4, 5, 6];
        File.WriteAllBytes(store.ItemsPath, junkMain);
        File.WriteAllBytes(store.BackupPath, junkBak);

        var r = store.Load();

        Assert.Equal(LoadStatus.Failed, r.Status);
        Assert.Empty(r.Items);
        Assert.False(store.CanSave);
        Assert.False(store.Save([Item("A")]));
        Assert.Equal(junkMain, File.ReadAllBytes(store.ItemsPath));
        Assert.Equal(junkBak, File.ReadAllBytes(store.BackupPath));
        Assert.Equal(2, Directory.GetFiles(dir.Path).Length);
    }

    private static bool Contains(byte[] haystack, byte[] needle) =>
        haystack.AsSpan().IndexOf(needle) >= 0;
}
