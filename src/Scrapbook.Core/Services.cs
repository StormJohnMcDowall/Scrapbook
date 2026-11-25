namespace Scrapbook.Core;

public interface IClipboardService
{
    /// <summary>
    /// Puts text on the clipboard. When <paramref name="sensitive"/> is true the text is kept out
    /// of Windows clipboard history and cloud clipboard sync. Returns false if the copy failed.
    /// </summary>
    bool TrySetText(string text, bool sensitive);
}

public interface IDialogService
{
    bool Confirm(string message);
    void ShowError(string message);
}
