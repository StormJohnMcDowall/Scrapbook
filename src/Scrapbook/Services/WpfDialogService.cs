using System.Windows;
using Scrapbook.Core;

namespace Scrapbook.Services;

public sealed class WpfDialogService(Func<Window?> owner) : IDialogService
{
    public bool Confirm(string message) => ScrapbookDialog.ShowConfirm(owner(), message);

    public void ShowError(string message) => ScrapbookDialog.ShowError(owner(), message);
}
