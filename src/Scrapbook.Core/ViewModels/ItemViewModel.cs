using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Scrapbook.Core.ViewModels;

public partial class ItemViewModel : ObservableObject
{
    private static readonly TimeSpan CopiedFeedbackDuration = TimeSpan.FromMilliseconds(1500);

    private readonly IClipboardService _clipboard;
    private readonly IDialogService _dialogs;
    private int _copyGeneration;

    public ItemViewModel(ItemData data, IClipboardService clipboard, IDialogService dialogs)
    {
        _clipboard = clipboard;
        _dialogs = dialogs;
        Id = data.Id;
        Description = data.Description;
        Body = data.Body;
        _isMasked = data.Masked;
    }

    public Guid Id { get; }

    [ObservableProperty]
    public partial string Description { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyCommand))]
    public partial string Body { get; set; }

    [ObservableProperty]
    public partial bool IsEditable { get; set; }

    [ObservableProperty]
    public partial bool IsCopied { get; set; }

    private bool _isMasked;

    /// <summary>Can only be changed while the item is editable (Edit mode on).</summary>
    public bool IsMasked
    {
        get => _isMasked;
        set
        {
            if (!IsEditable)
            {
                OnPropertyChanged(); // let a binding snap back to the real value
                return;
            }
            SetProperty(ref _isMasked, value);
        }
    }

    public ItemData ToData() => new(Id, Description, Body, IsMasked);

    private bool CanCopy() => !string.IsNullOrEmpty(Body);

    [RelayCommand(CanExecute = nameof(CanCopy))]
    private Task CopyAsync()
    {
        if (!_clipboard.TrySetText(Body, sensitive: IsMasked))
        {
            IsCopied = false;
            _dialogs.ShowError(Messages.CopyFailed);
            return Task.CompletedTask;
        }

        IsCopied = true;
        _ = ClearCopiedLaterAsync(++_copyGeneration);
        return Task.CompletedTask;
    }

    private async Task ClearCopiedLaterAsync(int generation)
    {
        await Task.Delay(CopiedFeedbackDuration);
        if (generation == _copyGeneration) IsCopied = false;
    }
}
