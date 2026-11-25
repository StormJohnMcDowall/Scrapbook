using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Scrapbook.Core;

public enum LoadStatus { NotFound, Loaded, LoadedFromBackup, Failed }

public sealed record LoadResult(LoadStatus Status, IReadOnlyList<ItemData> Items);

/// <summary>
/// Stores the item list in items.dat, encrypted with DPAPI for the current Windows user.
/// The previous version is kept as items.dat.bak. If neither file can be read, saving is
/// blocked so that nothing is ever written over them.
/// </summary>
public sealed class ItemStore
{
    private const int FormatVersion = 1;
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Scrapbook.items.v1");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly string _directory;

    public ItemStore(string directory)
    {
        _directory = directory;
        ItemsPath = Path.Combine(directory, "items.dat");
        BackupPath = Path.Combine(directory, "items.dat.bak");
    }

    public static string DefaultDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Scrapbook");

    public string ItemsPath { get; }
    public string BackupPath { get; }

    /// <summary>False after a load in which neither items.dat nor its backup could be read.</summary>
    public bool CanSave { get; private set; } = true;

    public LoadResult Load()
    {
        var mainExists = File.Exists(ItemsPath);
        var backupExists = File.Exists(BackupPath);

        if (mainExists && TryRead(ItemsPath, out var items))
            return Loaded(LoadStatus.Loaded, items);

        if (backupExists && TryRead(BackupPath, out items))
            return Loaded(LoadStatus.LoadedFromBackup, items);

        if (!mainExists && !backupExists)
            return Loaded(LoadStatus.NotFound, []);

        CanSave = false;
        return new LoadResult(LoadStatus.Failed, []);
    }

    /// <summary>
    /// Writes the list atomically. Returns false, without touching disk, when saving is blocked.
    /// Throws IOException or UnauthorizedAccessException if the disk write fails.
    /// </summary>
    public bool Save(IReadOnlyList<ItemData> items)
    {
        if (!CanSave) return false;

        Directory.CreateDirectory(_directory);
        var plain = JsonSerializer.SerializeToUtf8Bytes(new FileModel(FormatVersion, items), Json);
        var cipher = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);

        var tempPath = ItemsPath + ".tmp";
        File.WriteAllBytes(tempPath, cipher);

        if (File.Exists(ItemsPath))
            File.Replace(tempPath, ItemsPath, BackupPath);
        else
            File.Move(tempPath, ItemsPath, overwrite: true);

        return true;
    }

    private LoadResult Loaded(LoadStatus status, IReadOnlyList<ItemData> items)
    {
        CanSave = true;
        return new LoadResult(status, items);
    }

    private static bool TryRead(string path, out IReadOnlyList<ItemData> items)
    {
        items = [];
        try
        {
            var plain = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
            var model = JsonSerializer.Deserialize<FileModel>(plain, Json);
            if (model is null || model.Version != FormatVersion || model.Items is null) return false;
            items = model.Items;
            return true;
        }
        catch (Exception e) when (e is CryptographicException or JsonException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private sealed record FileModel(int Version, IReadOnlyList<ItemData>? Items);
}
