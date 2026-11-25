using System.Windows;
using Scrapbook.Core;

namespace Scrapbook.Services;

/// <param name="owner">The owning window, or null if none is visible yet.</param>
/// <param name="hideFromCapture">Whether dialogs should currently be hidden from screen capture.</param>
public sealed class WpfDialogService(Func<Window?> owner, Func<bool> hideFromCapture) : IDialogService
{
    public bool Confirm(string message) => ScrapbookDialog.ShowConfirm(owner(), message, hideFromCapture());

    public void ShowError(string message) => ScrapbookDialog.ShowError(owner(), message, hideFromCapture());
}
