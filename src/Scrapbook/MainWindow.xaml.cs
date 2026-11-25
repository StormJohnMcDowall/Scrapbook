using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Scrapbook.Core.ViewModels;

namespace Scrapbook;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly DispatcherTimer _saveTimer;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        vm.ItemAdded += OnItemAdded;

        // Flushes typing in description/body boxes; structural changes save immediately.
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _saveTimer.Tick += (_, _) => _vm.SaveIfDirty();
        _saveTimer.Start();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _saveTimer.Stop();
        _vm.Shutdown();
        base.OnClosing(e);
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
