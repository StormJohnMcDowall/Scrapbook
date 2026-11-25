namespace Scrapbook.Core.Tests;

public sealed class FakeClipboard : IClipboardService
{
    public bool Succeeds { get; set; } = true;
    public string? LastText { get; private set; }
    public bool? LastSensitive { get; private set; }

    public bool TrySetText(string text, bool sensitive)
    {
        if (!Succeeds) return false;
        LastText = text;
        LastSensitive = sensitive;
        return true;
    }
}

public sealed class FakeDialogs : IDialogService
{
    public bool ConfirmResult { get; set; }
    public List<string> Confirms { get; } = [];
    public List<string> Errors { get; } = [];

    public bool Confirm(string message)
    {
        Confirms.Add(message);
        return ConfirmResult;
    }

    public void ShowError(string message) => Errors.Add(message);
}
