using Scrapbook.Core.ViewModels;

namespace Scrapbook.Core.Tests;

public sealed class MainViewModelTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeDialogs _dialogs = new();

    public void Dispose() => _dir.Dispose();

    private static ItemData Item(string description, string body = "body", bool masked = false) =>
        new(Guid.NewGuid(), description, body, masked);

    private void Seed(params ItemData[] items) => new ItemStore(_dir.Path).Save(items);

    private IReadOnlyList<ItemData> Reload() => new ItemStore(_dir.Path).Load().Items;

    private MainViewModel NewVm(WindowSettings? settings = null) =>
        new(new ItemStore(_dir.Path), settings ?? new WindowSettings(), _clipboard, _dialogs);

    [Fact]
    public void StartsLocked_WithItemsNotEditable()
    {
        Seed(Item("A"), Item("B"));

        var vm = NewVm();

        Assert.False(vm.IsEditing);
        Assert.Equal(2, vm.Items.Count);
        Assert.All(vm.Items, i => Assert.False(i.IsEditable));
    }

    [Fact]
    public void TurningOnEdit_MakesExistingAndNewItemsEditable()
    {
        Seed(Item("A"));
        var vm = NewVm();

        vm.IsEditing = true;
        vm.AddItemCommand.Execute(null);

        Assert.All(vm.Items, i => Assert.True(i.IsEditable));

        vm.IsEditing = false;
        Assert.All(vm.Items, i => Assert.False(i.IsEditable));
    }

    [Fact]
    public void AddItem_CannotExecuteWhenLocked()
    {
        var vm = NewVm();

        Assert.False(vm.AddItemCommand.CanExecute(null));
        vm.IsEditing = true;
        Assert.True(vm.AddItemCommand.CanExecute(null));
    }

    [Fact]
    public void AddItem_AppendsEmptyUnmaskedItem_RaisesItemAdded_AndSaves()
    {
        Seed(Item("A"));
        var vm = NewVm();
        vm.IsEditing = true;
        ItemViewModel? raised = null;
        vm.ItemAdded += (_, item) => raised = item;

        vm.AddItemCommand.Execute(null);

        var added = vm.Items.Last();
        Assert.Equal("", added.Description);
        Assert.Equal("", added.Body);
        Assert.False(added.IsMasked);
        Assert.Same(added, raised);
        Assert.Equal(2, Reload().Count);
    }

    [Fact]
    public void DeleteItem_AsksUsingDescriptionNotBody_AndOnlyDeletesWhenConfirmed()
    {
        Seed(Item("Admin password", "hunter2", masked: true));
        var vm = NewVm();
        vm.IsEditing = true;
        var item = vm.Items[0];

        _dialogs.ConfirmResult = false;
        vm.DeleteItemCommand.Execute(item);

        Assert.Single(vm.Items);
        Assert.Equal(Messages.DeleteConfirm("Admin password"), _dialogs.Confirms[0]);
        Assert.DoesNotContain("hunter2", _dialogs.Confirms[0]);

        _dialogs.ConfirmResult = true;
        vm.DeleteItemCommand.Execute(item);

        Assert.Empty(vm.Items);
        Assert.Empty(Reload());
    }

    [Fact]
    public void DeleteItem_CannotExecuteWhenLocked()
    {
        Seed(Item("A"));
        var vm = NewVm();

        Assert.False(vm.DeleteItemCommand.CanExecute(vm.Items[0]));
    }

    [Fact]
    public void MoveItem_ReordersAndSaves_OnlyWhenEditing()
    {
        Seed(Item("A"), Item("B"), Item("C"));
        var vm = NewVm();

        vm.MoveItem(0, 2);
        Assert.Equal(["A", "B", "C"], vm.Items.Select(i => i.Description));

        vm.IsEditing = true;
        vm.MoveItem(0, 2);

        Assert.Equal(["B", "C", "A"], vm.Items.Select(i => i.Description));
        Assert.Equal(["B", "C", "A"], Reload().Select(i => i.Description));
    }

    [Fact]
    public void EditingBody_MarksDirty_AndSaveIfDirtyPersists()
    {
        Seed(Item("A", "old"));
        var vm = NewVm();
        vm.IsEditing = true;

        vm.Items[0].Body = "new";

        Assert.True(vm.HasUnsavedChanges);
        Assert.Equal("old", Reload()[0].Body);

        vm.SaveIfDirty();

        Assert.Equal("new", Reload()[0].Body);
        Assert.False(vm.HasUnsavedChanges);
    }

    [Fact]
    public void TogglingMask_SavesImmediately()
    {
        Seed(Item("A"));
        var vm = NewVm();
        vm.IsEditing = true;

        vm.Items[0].IsMasked = true;

        Assert.True(Reload()[0].Masked);
    }

    [Fact]
    public void TurningOffEdit_SavesImmediately()
    {
        Seed(Item("A", "old"));
        var vm = NewVm();
        vm.IsEditing = true;
        vm.Items[0].Description = "renamed";

        vm.IsEditing = false;

        Assert.Equal("renamed", Reload()[0].Description);
        Assert.False(vm.HasUnsavedChanges);
    }

    [Fact]
    public void Shutdown_SavesPendingEdits()
    {
        Seed(Item("A", "old"));
        var vm = NewVm();
        vm.IsEditing = true;
        vm.Items[0].Body = "typed just now";

        vm.Shutdown();

        Assert.Equal("typed just now", Reload()[0].Body);
    }

    [Fact]
    public void Shutdown_WhenEverythingSaved_ClosesWithoutAsking()
    {
        Seed(Item("A"));
        var vm = NewVm();
        vm.IsEditing = true;
        vm.Items[0].Body = "typed";

        Assert.True(vm.Shutdown());
        Assert.Empty(_dialogs.Confirms);
    }

    [Theory, InlineData(true), InlineData(false)]
    public void Shutdown_WhenSaveStillFailing_AsksBeforeDiscardingEdits(bool userClosesAnyway)
    {
        Seed(Item("A", "v0"));
        var itemsPath = new ItemStore(_dir.Path).ItemsPath;
        var vm = NewVm();
        vm.IsEditing = true;
        _dialogs.ConfirmResult = userClosesAnyway;

        bool canClose;
        using (new FileStream(itemsPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            vm.Items[0].Body = "v1";
            vm.SaveIfDirty(); // first failure: SaveFailed shown
            canClose = vm.Shutdown();
        }

        Assert.Equal([Messages.CloseWithUnsavedChanges], _dialogs.Confirms);
        Assert.Equal(userClosesAnyway, canClose);
    }

    [Fact]
    public void HideFromScreenShare_OnlyChangesWhileEditing()
    {
        var vm = NewVm(new WindowSettings(HideFromScreenShare: true));

        vm.HideFromScreenShare = false;
        Assert.True(vm.HideFromScreenShare);

        vm.IsEditing = true;
        vm.HideFromScreenShare = false;
        Assert.False(vm.HideFromScreenShare);
    }

    [Fact]
    public void Settings_AreTakenFromWindowSettings()
    {
        var vm = NewVm(new WindowSettings(AlwaysOnTop: false, HideFromScreenShare: false));

        Assert.False(vm.AlwaysOnTop);
        Assert.False(vm.HideFromScreenShare);
    }

    [Fact]
    public void AlwaysOnTop_ChangesWhileLocked()
    {
        var vm = NewVm(new WindowSettings(AlwaysOnTop: true));

        vm.AlwaysOnTop = false;

        Assert.False(vm.IsEditing);
        Assert.False(vm.AlwaysOnTop);
    }

    [Fact]
    public void IsEmpty_TracksItemCount()
    {
        var vm = NewVm();
        Assert.True(vm.IsEmpty);

        vm.IsEditing = true;
        vm.AddItemCommand.Execute(null);
        Assert.False(vm.IsEmpty);

        _dialogs.ConfirmResult = true;
        vm.DeleteItemCommand.Execute(vm.Items[0]);
        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public void LoadFailure_ShowsErrorAndNeverOverwritesFiles()
    {
        var store = new ItemStore(_dir.Path);
        byte[] junkMain = [1, 2, 3], junkBak = [4, 5, 6];
        File.WriteAllBytes(store.ItemsPath, junkMain);
        File.WriteAllBytes(store.BackupPath, junkBak);

        var vm = NewVm();
        Assert.Equal([Messages.LoadFailed], _dialogs.Errors);

        vm.IsEditing = true;
        vm.AddItemCommand.Execute(null);
        vm.Items[0].Body = "new";
        vm.Shutdown();

        Assert.Equal(junkMain, File.ReadAllBytes(store.ItemsPath));
        Assert.Equal(junkBak, File.ReadAllBytes(store.BackupPath));
        Assert.Equal(2, Directory.GetFiles(_dir.Path).Length);
        Assert.Equal([Messages.LoadFailed], _dialogs.Errors);
    }

    [Fact]
    public void SaveFailure_ShowsErrorOnceUntilASaveSucceeds()
    {
        Seed(Item("A", "v0"));
        var itemsPath = new ItemStore(_dir.Path).ItemsPath;
        var vm = NewVm();
        vm.IsEditing = true;

        using (new FileStream(itemsPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            vm.Items[0].Body = "v1";
            vm.SaveIfDirty();
            vm.Items[0].Body = "v2";
            vm.SaveIfDirty();

            Assert.Equal(1, _dialogs.Errors.Count(e => e == Messages.SaveFailed));
            Assert.True(vm.HasUnsavedChanges);
        }

        vm.SaveIfDirty();
        Assert.Equal("v2", Reload()[0].Body);
        Assert.False(vm.HasUnsavedChanges);

        using (new FileStream(itemsPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            vm.Items[0].Body = "v3";
            vm.SaveIfDirty();
        }

        Assert.Equal(2, _dialogs.Errors.Count(e => e == Messages.SaveFailed));
    }
}
