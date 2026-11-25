namespace Scrapbook.Core.Tests;

public class SettingsStoreTests
{
    [Fact]
    public void Load_WhenMissing_ReturnsDefaults()
    {
        using var dir = new TempDir();

        var s = new SettingsStore(dir.Path).Load();

        Assert.True(s.AlwaysOnTop);
        Assert.True(s.HideFromScreenShare);
        Assert.Null(s.Left);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        using var dir = new TempDir();
        var saved = new WindowSettings(10, 20, 400, 600, AlwaysOnTop: false, HideFromScreenShare: false);

        new SettingsStore(Path.Combine(dir.Path, "nested")).Save(saved);
        var loaded = new SettingsStore(Path.Combine(dir.Path, "nested")).Load();

        Assert.Equal(saved, loaded);
    }

    [Fact]
    public void Load_WhenCorrupt_ReturnsDefaults()
    {
        using var dir = new TempDir();
        var store = new SettingsStore(dir.Path);
        File.WriteAllText(store.SettingsPath, "{not json");

        Assert.Equal(new WindowSettings(), store.Load());
    }

    [Theory]
    [InlineData(100, 100, 400, true)]     // fully inside 0,0,1920,1080
    [InlineData(-350, 100, 400, false)]   // only 50px of title bar visible
    [InlineData(2000, 100, 400, false)]   // monitor unplugged, window off to the right
    [InlineData(100, 1070, 400, false)]   // title bar below bottom edge
    [InlineData(100, -5, 400, false)]     // title bar above top edge
    public void IsOnScreen_ChecksTitleBarVisibility(double left, double top, double width, bool expected)
        => Assert.Equal(expected, WindowPlacement.IsOnScreen(left, top, width, 0, 0, 1920, 1080));
}
