using Scrapbook.Core.ViewModels;

namespace Scrapbook.Core.Tests;

public class ItemViewModelTests
{
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeDialogs _dialogs = new();

    private ItemViewModel Vm(string body, bool masked = false, string description = "d") =>
        new(new ItemData(Guid.NewGuid(), description, body, masked), _clipboard, _dialogs);

    [Theory, InlineData(true), InlineData(false)]
    public async Task Copy_PutsBodyOnClipboard_SensitiveOnlyWhenMasked(bool masked)
    {
        var vm = Vm("secret", masked);

        await vm.CopyCommand.ExecuteAsync(null);

        Assert.Equal("secret", _clipboard.LastText);
        Assert.Equal(masked, _clipboard.LastSensitive);
        Assert.True(vm.IsCopied);
    }

    [Fact]
    public async Task Copy_WhenClipboardFails_ShowsErrorWithoutBody()
    {
        _clipboard.Succeeds = false;
        var vm = Vm("secret");

        await vm.CopyCommand.ExecuteAsync(null);

        Assert.Equal([Messages.CopyFailed], _dialogs.Errors);
        Assert.DoesNotContain("secret", _dialogs.Errors.Single());
        Assert.False(vm.IsCopied);
    }

    [Fact]
    public void Copy_CannotExecuteWhenBodyEmpty()
    {
        var vm = Vm("");
        Assert.False(vm.CopyCommand.CanExecute(null));

        vm.IsEditable = true;
        vm.Body = "x";

        Assert.True(vm.CopyCommand.CanExecute(null));
    }

    [Fact]
    public void IsMasked_CannotChangeWhenNotEditable()
    {
        var vm = Vm("secret", masked: true);

        vm.IsMasked = false;
        Assert.True(vm.IsMasked);

        vm.IsEditable = true;
        vm.IsMasked = false;
        Assert.False(vm.IsMasked);
    }

    [Fact]
    public void ToData_ReflectsEdits()
    {
        var vm = Vm("old", masked: false, description: "old desc");
        vm.IsEditable = true;

        vm.Description = "new desc";
        vm.Body = "new";
        vm.IsMasked = true;

        Assert.Equal(new ItemData(vm.Id, "new desc", "new", true), vm.ToData());
    }
}
