namespace Scrapbook.Core;

/// <summary>User-facing text. None of it may ever include an item's body.</summary>
public static class Messages
{
    public const string CopyFailed = "Couldn't copy — try again";
    public const string EmptyList = "No items yet — turn on Edit to add one.";
    public const string LoadFailed = "Scrapbook couldn't read your saved items. To protect them, nothing will be saved until this is fixed.";
    public const string SaveFailed = "Scrapbook couldn't save your changes. It will keep trying.";
    public const string CloseWithUnsavedChanges = "Scrapbook still couldn't save your latest changes. Close anyway and lose them?";
    public const string DeleteButton = "Delete";
    public const string CloseAnywayButton = "Close anyway";

    public static string DeleteConfirm(string description) =>
        string.IsNullOrWhiteSpace(description) ? "Delete this item?" : $"Delete \"{description}\"?";
}
