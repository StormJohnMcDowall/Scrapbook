using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Scrapbook.Core;
using Scrapbook.Core.ViewModels;
using Scrapbook.Interop;

namespace Scrapbook;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly SettingsStore _settingsStore;
    private readonly DispatcherTimer _saveTimer;

    public MainWindow(MainViewModel vm, SettingsStore settingsStore, WindowSettings settings)
    {
        InitializeComponent();
        _vm = vm;
        _settingsStore = settingsStore;
        DataContext = vm;

        RestorePlacement(settings);
        DisplayAffinity.Apply(this, vm.HideFromScreenShare);

        vm.ItemAdded += OnItemAdded;
        vm.PropertyChanged += OnViewModelPropertyChanged;

        // Flushes typing in description/body boxes; structural changes save immediately.
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _saveTimer.Tick += (_, _) => _vm.SaveIfDirty();
        _saveTimer.Start();
    }

    /// <summary>Brings the window to the front, e.g. when a second copy of the app is launched.</summary>
    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Show();
        Activate();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _saveTimer.Stop();
        _vm.Shutdown();
        SaveSettings();
        base.OnClosing(e);
    }

    private void RestorePlacement(WindowSettings s)
    {
        if (s is not { Left: { } left, Top: { } top, Width: { } width, Height: { } height }) return;
        if (!WindowPlacement.IsOnScreen(left, top, width,
                SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight)) return;

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = left;
        Top = top;
        Width = Math.Max(width, MinWidth);
        Height = Math.Max(height, MinHeight);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.HideFromScreenShare):
                DisplayAffinity.Apply(this, _vm.HideFromScreenShare);
                SaveSettings();
                break;
            case nameof(MainViewModel.AlwaysOnTop):
                SaveSettings();
                break;
        }
    }

    private void SaveSettings()
    {
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        try
        {
            _settingsStore.Save(new WindowSettings(bounds.Left, bounds.Top, bounds.Width, bounds.Height,
                _vm.AlwaysOnTop, _vm.HideFromScreenShare));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Window settings are a convenience; losing them is not worth interrupting a demo.
        }
    }

    private void OnItemAdded(object? sender, ItemViewModel item)
    {
        // Wait for the row to be generated, then scroll to it and focus its description box.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (ItemsList.ItemContainerGenerator.ContainerFromItem(item) is not ContentPresenter row) return;
            row.BringIntoView();
            if (row.ContentTemplate?.FindName("DescriptionBox", row) is TextBox box) box.Focus();
        });
    }
}
