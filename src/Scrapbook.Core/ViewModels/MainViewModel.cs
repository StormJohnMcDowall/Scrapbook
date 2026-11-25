using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Scrapbook.Core.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ItemStore _store;
    private readonly IClipboardService _clipboard;
    private readonly IDialogService _dialogs;
    private bool _saveErrorShown;

    public MainViewModel(ItemStore store, WindowSettings settings, IClipboardService clipboard, IDialogService dialogs)
    {
        _store = store;
        _clipboard = clipboard;
        _dialogs = dialogs;
        AlwaysOnTop = settings.AlwaysOnTop;
        _hideFromScreenShare = settings.HideFromScreenShare;

        var result = store.Load();
        foreach (var data in result.Items)
            Track(new ItemViewModel(data, clipboard, dialogs));

        Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));

        if (result.Status == LoadStatus.Failed)
            dialogs.ShowError(Messages.LoadFailed);
    }

    public ObservableCollection<ItemViewModel> Items { get; } = [];

    public bool IsEmpty => Items.Count == 0;

    public event EventHandler<ItemViewModel>? ItemAdded;

    /// <summary>Global edit mode. Always false at startup; never persisted.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddItemCommand), nameof(DeleteItemCommand))]
    public partial bool IsEditing { get; set; }

    [ObservableProperty]
    public partial bool AlwaysOnTop { get; set; }

    [ObservableProperty]
    public partial bool HasUnsavedChanges { get; private set; }

    private bool _hideFromScreenShare;

    /// <summary>Can only be changed while Edit mode is on, so a stray click can't expose the window.</summary>
    public bool HideFromScreenShare
    {
        get => _hideFromScreenShare;
        set
        {
            if (!IsEditing)
            {
                OnPropertyChanged(); // let a binding snap back to the real value
                return;
            }
            SetProperty(ref _hideFromScreenShare, value);
        }
    }

    partial void OnIsEditingChanged(bool value)
    {
        foreach (var item in Items) item.IsEditable = value;
        if (!value) SaveIfDirty();
    }

    [RelayCommand(CanExecute = nameof(IsEditing))]
    private void AddItem()
    {
        var item = new ItemViewModel(new ItemData(Guid.NewGuid(), "", "", false), _clipboard, _dialogs);
        Track(item);
        SaveNow();
        ItemAdded?.Invoke(this, item);
    }

    private bool CanDeleteItem(ItemViewModel? item) => IsEditing && item is not null;

    [RelayCommand(CanExecute = nameof(CanDeleteItem))]
    private void DeleteItem(ItemViewModel? item)
    {
        if (item is null || !IsEditing || !Items.Contains(item)) return;
        if (!_dialogs.Confirm(Messages.DeleteConfirm(item.Description))) return;

        item.PropertyChanged -= OnItemPropertyChanged;
        Items.Remove(item);
        SaveNow();
    }

    public void MoveItem(int fromIndex, int toIndex)
    {
        if (!IsEditing || fromIndex == toIndex) return;
        if (fromIndex < 0 || fromIndex >= Items.Count || toIndex < 0 || toIndex >= Items.Count) return;

        Items.Move(fromIndex, toIndex);
        SaveNow();
    }

    /// <summary>Writes the list if anything changed. Save errors are shown once per failure streak.</summary>
    public void SaveIfDirty()
    {
        if (!HasUnsavedChanges || !_store.CanSave) return;

        try
        {
            if (_store.Save(Items.Select(i => i.ToData()).ToList()))
            {
                HasUnsavedChanges = false;
                _saveErrorShown = false;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            if (_saveErrorShown) return;
            _saveErrorShown = true;
            _dialogs.ShowError(Messages.SaveFailed);
        }
    }

    public void Shutdown() => SaveIfDirty();

    private void SaveNow()
    {
        HasUnsavedChanges = true;
        SaveIfDirty();
    }

    private void Track(ItemViewModel item)
    {
        item.IsEditable = IsEditing;
        item.PropertyChanged += OnItemPropertyChanged;
        Items.Add(item);
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ItemViewModel { IsEditable: true }) return;

        switch (e.PropertyName)
        {
            case nameof(ItemViewModel.Description):
            case nameof(ItemViewModel.Body):
                HasUnsavedChanges = true; // flushed by the app's save timer
                break;
            case nameof(ItemViewModel.IsMasked):
                SaveNow();
                break;
        }
    }
}
