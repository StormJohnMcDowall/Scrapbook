using System.Runtime.InteropServices;
using System.Windows;
using Scrapbook.Core;
using Scrapbook.Core.ViewModels;
using Scrapbook.Services;

namespace Scrapbook;

public partial class App : Application
{
    private const string InstanceMutexName = @"Local\Scrapbook.SingleInstance";
    private const string ActivateEventName = @"Local\Scrapbook.Activate";
    private const int ASFW_ANY = -1;

    private Mutex? _instanceMutex;
    private EventWaitHandle? _activateEvent;
    private RegisteredWaitHandle? _activateWait;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Only one Scrapbook at a time, so two copies can't overwrite each other's saves.
        _instanceMutex = new Mutex(initiallyOwned: true, InstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            ActivateExistingInstance();
            Shutdown();
            return;
        }

        var directory = ItemStore.DefaultDirectory;
        var settingsStore = new SettingsStore(directory);
        var settings = settingsStore.Load();

        // Dialogs are owned by the main window once it is visible (a load error can appear before that).
        MainViewModel? vm = null;
        var dialogs = new WpfDialogService(
            owner: () => MainWindow is { IsVisible: true } w ? w : null,
            hideFromCapture: () => vm?.HideFromScreenShare ?? settings.HideFromScreenShare);
        vm = new MainViewModel(new ItemStore(directory), settings, new WpfClipboardService(), dialogs);

        var window = new MainWindow(vm, settingsStore, settings);
        MainWindow = window;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        window.Show();

        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        _activateWait = ThreadPool.RegisterWaitForSingleObject(_activateEvent,
            (_, _) => Dispatcher.BeginInvoke(window.BringToFront), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _activateWait?.Unregister(null);
        _activateEvent?.Dispose();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }

    private static void ActivateExistingInstance()
    {
        // This process was just launched by the user, so it may hand foreground rights to the first one.
        AllowSetForegroundWindow(ASFW_ANY);
        try
        {
            using var activate = EventWaitHandle.OpenExisting(ActivateEventName);
            activate.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // The first instance is still starting up; nothing to activate yet.
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);
}
