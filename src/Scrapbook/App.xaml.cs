using System.Windows;
using Scrapbook.Core;
using Scrapbook.Core.ViewModels;
using Scrapbook.Services;

namespace Scrapbook;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var directory = ItemStore.DefaultDirectory;
        var settingsStore = new SettingsStore(directory);
        var settings = settingsStore.Load();

        // Dialogs are owned by the main window once it is visible (a load error can appear before that).
        var dialogs = new WpfDialogService(() => MainWindow is { IsVisible: true } w ? w : null);
        var vm = new MainViewModel(new ItemStore(directory), settings, new WpfClipboardService(), dialogs);

        var window = new MainWindow(vm);
        MainWindow = window;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        window.Show();
    }
}
