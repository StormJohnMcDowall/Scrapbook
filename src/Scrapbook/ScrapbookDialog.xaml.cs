using System.Windows;
using Scrapbook.Interop;

namespace Scrapbook;

/// <summary>
/// Scrapbook's own confirm/error dialog. Used instead of MessageBox so the app controls the
/// window handle (needed to hide it from screen capture).
/// </summary>
public partial class ScrapbookDialog : Window
{
    private ScrapbookDialog(Window? owner, string message, string primaryText, bool showCancel, bool hideFromCapture)
    {
        InitializeComponent();
        DisplayAffinity.Apply(this, hideFromCapture);
        MessageText.Text = message;
        PrimaryButton.Content = primaryText;
        CancelButton.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;

        if (owner is not null)
        {
            Owner = owner;
            Topmost = owner.Topmost;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    public static bool ShowConfirm(Window? owner, string message, bool hideFromCapture) =>
        new ScrapbookDialog(owner, message, "Delete", showCancel: true, hideFromCapture).ShowDialog() == true;

    public static void ShowError(Window? owner, string message, bool hideFromCapture) =>
        new ScrapbookDialog(owner, message, "OK", showCancel: false, hideFromCapture).ShowDialog();

    private void OnPrimaryClick(object sender, RoutedEventArgs e) => DialogResult = true;
}
