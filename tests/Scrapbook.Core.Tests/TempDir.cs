namespace Scrapbook.Core.Tests;

/// <summary>A unique, empty folder under %TEMP%\ScrapbookTests, deleted on dispose.</summary>
public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "ScrapbookTests", Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Path);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a test may still hold a file open.
        }
    }
}
