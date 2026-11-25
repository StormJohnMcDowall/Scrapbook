using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        if (!_vm.Shutdown())
        {
            e.Cancel = true; // the user chose to stay open rather than lose unsaved edits
            return;
        }
        _saveTimer.Stop();
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

    // ---- Drag-to-reorder (handle only visible in Edit mode; MoveItem also refuses when locked) ----

    private Point _dragStart;
    private ItemViewModel? _dragCandidate;

    private void OnHandleMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _dragCandidate = (sender as FrameworkElement)?.DataContext as ItemViewModel;
    }

    private void OnHandleMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragCandidate is null || e.LeftButton != MouseButtonState.Pressed || !_vm.IsEditing) return;

        var delta = e.GetPosition(this) - _dragStart;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var item = _dragCandidate;
        _dragCandidate = null;
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(typeof(ItemViewModel), item), DragDropEffects.Move);
    }

    private void OnRowDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(ItemViewModel)) && _vm.IsEditing ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnRowDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(ItemViewModel)) is not ItemViewModel dragged) return;

        // Dropping on a row moves the dragged item into that row's position; below all rows, to the end.
        var target = (e.OriginalSource as FrameworkElement)?.DataContext as ItemViewModel;
        var from = _vm.Items.IndexOf(dragged);
        var to = target is null ? _vm.Items.Count - 1 : _vm.Items.IndexOf(target);
        if (from >= 0 && to >= 0) _vm.MoveItem(from, to);
        e.Handled = true;
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
