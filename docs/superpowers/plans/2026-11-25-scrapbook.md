# Scrapbook Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A single-window WPF app holding an encrypted, editable list of text snippets with one-click copy, masking, always-on-top, screen-share hiding and a global Edit lock.

**Architecture:** All logic lives in a UI-free class library, `Scrapbook.Core`: storage, settings and view models (CommunityToolkit.Mvvm), all unit-tested. The WPF app, `Scrapbook`, is a thin shell. It holds the XAML, the Win32 interop (display affinity, clipboard formats) and the implementations of the two service interfaces Core defines (`IClipboardService`, `IDialogService`).

**Tech Stack:** .NET 10, WPF (`ThemeMode="System"`), CommunityToolkit.Mvvm, System.Security.Cryptography.ProtectedData (DPAPI), System.Text.Json, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-25-scrapbook-design.md`

## Global Constraints

- All projects target `net10.0-windows`. The app is WPF with `ThemeMode="System"`. Suppress the experimental warning `WPF0001`.
- Data folder: `%LOCALAPPDATA%\Scrapbook\`. Files: `items.dat` (encrypted), `items.dat.bak` (previous version), `settings.json` (plain).
- Encryption: `ProtectedData.Protect/Unprotect` with `DataProtectionScope.CurrentUser` and entropy `Encoding.UTF8.GetBytes("Scrapbook.items.v1")`. The whole item list is encrypted, not just bodies.
- The app **always starts with Edit off**. Edit mode is never persisted.
- Hide from screen share: **on at first launch, then remembered**. Always on top: on at first launch (matches the approved mockup), then remembered.
- While locked, only Copy works in the list. Always on top stays clickable; **Hide from screen share is disabled**.
- Body text is never logged and never appears in any message, tooltip or dialog.
- Exact UI copy: `Always on top`, `Hide from screen share`, `Edit`, `Mask`, `Copy`, `Copied ✓`, `+ Add item`, `No items yet — turn on Edit to add one.`, `Couldn't copy — try again`.
- Release output: a single framework-dependent `.exe` (`win-x64`).

## Review Focus

1. **Body text with leading/trailing spaces, tabs, line breaks, non-ASCII characters/emoji, or 10,000+ characters.** Copy must return exactly what was saved, byte for byte. → Task 1 `SaveThenLoad_PreservesTextExactly`.
2. **Clipboard held by another app when Copy is clicked.** Show `Couldn't copy — try again`, don't show "Copied ✓", never include the body. → Task 3 `Copy_WhenClipboardFails_ShowsErrorWithoutBody`.
3. **Closing the window less than a second after typing.** The last edit must be saved. → Task 4 `Shutdown_SavesPendingEdits`.
4. **Both save files unreadable, and the user keeps working and then exits.** Neither file is ever overwritten. → Task 4 `LoadFailure_ShowsErrorAndNeverOverwritesFiles`.
5. **Save fails (file locked by antivirus/backup software).** Show an error once, not every second, and keep the edits in memory. → Task 4 `SaveFailure_ShowsErrorOnceUntilASaveSucceeds`.

---

## File Structure

```
Scrapbook.slnx
.gitignore
src/Scrapbook.Core/                 UI-free logic (class library)
  ItemData.cs                       record ItemData
  ItemStore.cs                      encrypted items.dat + .bak, LoadResult/LoadStatus
  WindowSettings.cs                 record WindowSettings
  SettingsStore.cs                  settings.json
  WindowPlacement.cs                off-screen check
  Messages.cs                       user-facing copy constants
  Services.cs                       IClipboardService, IDialogService
  ViewModels/ItemViewModel.cs
  ViewModels/MainViewModel.cs
src/Scrapbook/                      WPF app
  App.xaml(.cs)                     theme, single instance, composition
  MainWindow.xaml(.cs)              layout, bindings, placement, timers, drag
  ScrapbookDialog.xaml(.cs)         custom confirm/error dialog
  Interop/DisplayAffinity.cs        SetWindowDisplayAffinity
  Services/WpfClipboardService.cs
  Services/WpfDialogService.cs
  Behaviors/PasswordBoxAssist.cs    bindable PasswordBox.Password
tests/Scrapbook.Core.Tests/
  ItemStoreTests.cs
  SettingsStoreTests.cs
  ItemViewModelTests.cs
  MainViewModelTests.cs
  Fakes.cs                          FakeClipboard, FakeDialogs
  TempDir.cs                        disposable temp folder helper
```

---

### Task 1: Encrypted item storage

**Files:**
- Create: `Scrapbook.slnx`, `.gitignore`, `src/Scrapbook.Core/Scrapbook.Core.csproj`, `src/Scrapbook.Core/ItemData.cs`, `src/Scrapbook.Core/ItemStore.cs`
- Test: `tests/Scrapbook.Core.Tests/Scrapbook.Core.Tests.csproj`, `tests/Scrapbook.Core.Tests/TempDir.cs`, `tests/Scrapbook.Core.Tests/ItemStoreTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record ItemData(Guid Id, string Description, string Body, bool Masked);`
  - `public enum LoadStatus { NotFound, Loaded, LoadedFromBackup, Failed }`
  - `public sealed record LoadResult(LoadStatus Status, IReadOnlyList<ItemData> Items);`
  - `public sealed class ItemStore`
    - `ItemStore(string directory)`
    - `static string DefaultDirectory` → `Path.Combine(Environment.GetFolderPath(SpecialFolder.LocalApplicationData), "Scrapbook")`
    - `string ItemsPath`, `string BackupPath`
    - `LoadResult Load()`
    - `bool CanSave` (false after a `Failed` load)
    - `bool Save(IReadOnlyList<ItemData> items)`: returns false without touching disk when `!CanSave`, and throws `IOException`/`UnauthorizedAccessException` on disk failure.
  - Test helper `TempDir : IDisposable` with `string Path`, which creates a unique folder under `%TEMP%\ScrapbookTests` and deletes it on dispose.

- [ ] **Step 1: Scaffold**

```bash
dotnet new gitignore
echo "publish/" >> .gitignore
dotnet new sln -n Scrapbook
dotnet new classlib -n Scrapbook.Core -o src/Scrapbook.Core
dotnet new xunit -n Scrapbook.Core.Tests -o tests/Scrapbook.Core.Tests
```
Set `<TargetFramework>net10.0-windows</TargetFramework>` and `<Nullable>enable</Nullable>` in both csproj files. Delete template `Class1.cs`/`UnitTest1.cs`. Then:
```bash
dotnet add src/Scrapbook.Core package System.Security.Cryptography.ProtectedData
dotnet add tests/Scrapbook.Core.Tests reference src/Scrapbook.Core
dotnet sln add src/Scrapbook.Core tests/Scrapbook.Core.Tests
```

- [ ] **Step 2: Write the failing tests** in `ItemStoreTests.cs` (each test uses `using var dir = new TempDir();`)

```csharp
[Fact] public void Load_WhenNoFiles_ReturnsNotFoundAndEmpty()
{ var store = new ItemStore(dir.Path); var r = store.Load(); Assert.Equal(LoadStatus.NotFound, r.Status); Assert.Empty(r.Items); Assert.True(store.CanSave); }

[Fact] public void Save_CreatesDirectoryIfMissing()
{ var store = new ItemStore(Path.Combine(dir.Path, "nested")); store.Save([Item("a")]); Assert.True(File.Exists(store.ItemsPath)); }

[Fact] public void SaveThenLoad_RoundTripsItemsInOrder()
// save 3 items (one Masked=true); new ItemStore(same dir).Load() → Status Loaded, Items sequence-equal to saved (records compare by value)

[Fact] public void SaveThenLoad_PreservesTextExactly()
// bodies: "  pa ss\t ", "line1\r\nline2", "pässwörd🔑", new string('x', 10_000) → loaded bodies Assert.Equal each exactly

[Fact] public void Save_DoesNotWritePlaintext()
// description "DescMarker123", body "BodyMarker456"; bytes = File.ReadAllBytes(ItemsPath)
// assert neither marker found in bytes as UTF-8 nor as UTF-16 (Encoding.Unicode)

[Fact] public void Save_SecondSave_KeepsPreviousVersionAsBackup()
// Save([A]); Save([B]); File.Delete(ItemsPath); Load() → LoadedFromBackup, Items == [A]

[Fact] public void Load_WhenMainFileCorrupt_FallsBackToBackup()
// Save([A]); Save([B]); File.WriteAllBytes(ItemsPath, [1,2,3]); Load() → LoadedFromBackup, Items == [A]; CanSave true

[Fact] public void Load_WhenBothFilesCorrupt_FailsAndNeverWrites()
// write junk to ItemsPath and BackupPath; Load() → Failed, empty; CanSave false;
// Save([A]) returns false; both files' bytes unchanged; no other files created in dir
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/Scrapbook.Core.Tests --filter "FullyQualifiedName~ItemStoreTests"`
Expected: build fails, because `ItemStore` is not defined.

- [ ] **Step 4: Implement `ItemData` and `ItemStore`**

The on-disk plaintext before encryption is JSON `{"version":1,"items":[{"id":…,"description":…,"body":…,"masked":…}]}`, using System.Text.Json camelCase. A `version` other than 1, a decrypt failure (`CryptographicException`) or a parse failure all count as "unreadable" for that file. Load order: main file, then backup, then NotFound when neither exists. A missing main file with a present backup loads from the backup. Save writes `items.dat.tmp`. It then calls `File.Replace(tmp, ItemsPath, BackupPath)` when `ItemsPath` exists, and otherwise `File.Move(tmp, ItemsPath)`.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Scrapbook.Core.Tests --filter "FullyQualifiedName~ItemStoreTests"`
Expected: 8 passed.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: encrypted item storage with backup recovery"
```

---

### Task 2: Window settings storage and placement check

**Files:**
- Create: `src/Scrapbook.Core/WindowSettings.cs`, `src/Scrapbook.Core/SettingsStore.cs`, `src/Scrapbook.Core/WindowPlacement.cs`
- Test: `tests/Scrapbook.Core.Tests/SettingsStoreTests.cs`

**Interfaces:**
- Consumes: `TempDir` (Task 1).
- Produces:
  - `public sealed record WindowSettings(double? Left = null, double? Top = null, double? Width = null, double? Height = null, bool AlwaysOnTop = true, bool HideFromScreenShare = true);`
  - `public sealed class SettingsStore`: `SettingsStore(string directory)`, `string SettingsPath` (`settings.json`), `WindowSettings Load()` (returns `new WindowSettings()` when the file is missing or unreadable), `void Save(WindowSettings settings)` (creates the directory).
  - `public static class WindowPlacement`: `static bool IsOnScreen(double left, double top, double width, double screenLeft, double screenTop, double screenWidth, double screenHeight)`. It returns true when at least a 100×30 strip of the window's top edge (its title bar) lies inside the screen rectangle.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Load_WhenMissing_ReturnsDefaults()
{ var s = new SettingsStore(dir.Path).Load(); Assert.True(s.AlwaysOnTop); Assert.True(s.HideFromScreenShare); Assert.Null(s.Left); }

[Fact] public void SaveThenLoad_RoundTrips()
// new WindowSettings(10, 20, 400, 600, AlwaysOnTop: false, HideFromScreenShare: false) → Load() equals it

[Fact] public void Load_WhenCorrupt_ReturnsDefaults()
// File.WriteAllText(SettingsPath, "{not json") → Load() == new WindowSettings()

[Theory]
[InlineData(100, 100, 400, true)]     // fully inside 0,0,1920,1080
[InlineData(-350, 100, 400, false)]   // only 50px of title bar visible
[InlineData(2000, 100, 400, false)]   // monitor unplugged, window off to the right
[InlineData(100, 1070, 400, false)]   // title bar below bottom edge
[InlineData(100, -5, 400, false)]     // title bar above top edge
public void IsOnScreen_ChecksTitleBarVisibility(double left, double top, double width, bool expected)
    => Assert.Equal(expected, WindowPlacement.IsOnScreen(left, top, width, 0, 0, 1920, 1080));
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Scrapbook.Core.Tests --filter "FullyQualifiedName~SettingsStoreTests"`
Expected: build fails, because `SettingsStore` is not defined.

- [ ] **Step 3: Implement `WindowSettings`, `SettingsStore`, `WindowPlacement`**

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Scrapbook.Core.Tests --filter "FullyQualifiedName~SettingsStoreTests"`
Expected: 8 passed.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: window settings storage and off-screen check"
```

---

### Task 3: Item view model, copy and service contracts

**Files:**
- Create: `src/Scrapbook.Core/Messages.cs`, `src/Scrapbook.Core/Services.cs`, `src/Scrapbook.Core/ViewModels/ItemViewModel.cs`
- Test: `tests/Scrapbook.Core.Tests/Fakes.cs`, `tests/Scrapbook.Core.Tests/ItemViewModelTests.cs`

**Interfaces:**
- Consumes: `ItemData` (Task 1).
- Produces:
  - `public interface IClipboardService { bool TrySetText(string text, bool sensitive); }`. When `sensitive` is true, the implementation keeps the text out of clipboard history and cloud sync.
  - `public interface IDialogService { bool Confirm(string message); void ShowError(string message); }`
  - `public static class Messages`
    - `const string CopyFailed = "Couldn't copy — try again";`
    - `const string EmptyList = "No items yet — turn on Edit to add one.";`
    - `const string LoadFailed = "Scrapbook couldn't read your saved items. To protect them, nothing will be saved until this is fixed.";`
    - `const string SaveFailed = "Scrapbook couldn't save your changes. It will keep trying.";`
    - `static string DeleteConfirm(string description)` → `string.IsNullOrWhiteSpace(description) ? "Delete this item?" : $"Delete \"{description}\"?"`
  - `public partial class ItemViewModel : ObservableObject`
    - `ItemViewModel(ItemData data, IClipboardService clipboard, IDialogService dialogs)`
    - `Guid Id`; observable `string Description`, `string Body`, `bool IsMasked`, `bool IsEditable`, `bool IsCopied`
    - `IsMasked` setter is ignored (no change) when `!IsEditable`
    - `IAsyncRelayCommand CopyCommand` (CanExecute: `Body` non-empty; re-evaluated when `Body` changes)
    - `ItemData ToData()`
  - Test fakes:
    - `FakeClipboard : IClipboardService` with `bool Succeeds = true`, `string? LastText`, `bool? LastSensitive`
    - `FakeDialogs : IDialogService` with `bool ConfirmResult`, `List<string> Confirms`, `List<string> Errors`

- [ ] **Step 1: Add the toolkit**

```bash
dotnet add src/Scrapbook.Core package CommunityToolkit.Mvvm
```

- [ ] **Step 2: Write the failing tests**

```csharp
[Theory, InlineData(true), InlineData(false)]
public async Task Copy_PutsBodyOnClipboard_SensitiveOnlyWhenMasked(bool masked)
// vm from new ItemData(id, "d", "secret", masked); await vm.CopyCommand.ExecuteAsync(null);
// clipboard.LastText == "secret"; clipboard.LastSensitive == masked; vm.IsCopied true

[Fact] public async Task Copy_WhenClipboardFails_ShowsErrorWithoutBody()
// clipboard.Succeeds = false; body "secret"; execute → dialogs.Errors == [Messages.CopyFailed];
// Assert.DoesNotContain("secret", dialogs.Errors.Single()); vm.IsCopied false

[Fact] public void Copy_CannotExecuteWhenBodyEmpty()
// body "" → CanExecute false; vm.IsEditable = true; vm.Body = "x" → CanExecute true

[Fact] public void IsMasked_CannotChangeWhenNotEditable()
// masked=true, IsEditable=false; vm.IsMasked = false → still true; IsEditable=true; set false → false

[Fact] public void ToData_ReflectsEdits()
// IsEditable=true; change Description/Body/IsMasked → ToData() == new ItemData(sameId, newDesc, newBody, newMasked)
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/Scrapbook.Core.Tests --filter "FullyQualifiedName~ItemViewModelTests"`
Expected: build fails, because `ItemViewModel` is not defined.

- [ ] **Step 4: Implement `Messages`, `Services`, `ItemViewModel`**

After a successful copy, set `IsCopied = true`, wait 1.5 seconds (`Task.Delay(1500)`), then set it back to false. The view swaps the button text to `Copied ✓` while `IsCopied` is true.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Scrapbook.Core.Tests --filter "FullyQualifiedName~ItemViewModelTests"`
Expected: 6 passed.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: item view model with masked-aware copy"
```

---

### Task 4: Main view model (edit mode, list changes, saving)

**Files:**
- Create: `src/Scrapbook.Core/ViewModels/MainViewModel.cs`
- Test: `tests/Scrapbook.Core.Tests/MainViewModelTests.cs`

**Interfaces:**
- Consumes: `ItemStore`, `LoadStatus` (Task 1); `WindowSettings` (Task 2); `ItemViewModel`, `IClipboardService`, `IDialogService`, `Messages`, fakes (Task 3).
- Produces: `public partial class MainViewModel : ObservableObject`
  - `MainViewModel(ItemStore store, WindowSettings settings, IClipboardService clipboard, IDialogService dialogs)`. It loads items in the constructor and calls `dialogs.ShowError(Messages.LoadFailed)` on `Failed`.
  - `ObservableCollection<ItemViewModel> Items`
  - observable `bool IsEditing` (always false initially), `bool AlwaysOnTop`, `bool HideFromScreenShare`, `bool IsEmpty`
  - `HideFromScreenShare` setter is ignored when `!IsEditing`. It still raises `PropertyChanged` so a binding re-syncs.
  - `bool HasUnsavedChanges`
  - `IRelayCommand AddItemCommand`, `IRelayCommand<ItemViewModel> DeleteItemCommand` (both CanExecute only while `IsEditing`)
  - `void MoveItem(int fromIndex, int toIndex)` (no-op when `!IsEditing`)
  - `void SaveIfDirty()`, `void Shutdown()` (= `SaveIfDirty()`)
  - `event EventHandler<ItemViewModel>? ItemAdded`

Saving rules:
- Add, delete, move, a Mask toggle, or Edit being turned off marks the list dirty and calls `SaveIfDirty()` immediately.
- A Description or Body change only marks it dirty. The app's 1-second timer (Task 5) flushes it.
- `SaveIfDirty` catches `IOException`/`UnauthorizedAccessException` and leaves the list dirty. It shows `Messages.SaveFailed` only on the first failure after a success.

- [ ] **Step 1: Write the failing tests** (real `ItemStore` in a `TempDir`, with `FakeClipboard`/`FakeDialogs`; seed items by saving through a separate `ItemStore` first)

```csharp
[Fact] public void StartsLocked_WithItemsNotEditable()
[Fact] public void TurningOnEdit_MakesExistingAndNewItemsEditable()
[Fact] public void AddItem_CannotExecuteWhenLocked()
[Fact] public void AddItem_AppendsEmptyUnmaskedItem_RaisesItemAdded_AndSaves()
// Items.Last(): Description "", Body "", IsMasked false; ItemAdded raised with it; new ItemStore(dir).Load().Items.Count == previous + 1
[Fact] public void DeleteItem_AsksUsingDescriptionNotBody_AndOnlyDeletesWhenConfirmed()
// item ("Admin password", "hunter2"); ConfirmResult=false → still present, Confirms[0] == Messages.DeleteConfirm("Admin password"), not containing "hunter2";
// ConfirmResult=true → removed and persisted
[Fact] public void MoveItem_ReordersAndSaves_OnlyWhenEditing()
// locked: MoveItem(0,2) → order unchanged; editing: MoveItem(0,2) → [B,C,A] in VM and on reload
[Fact] public void EditingBody_MarksDirty_AndSaveIfDirtyPersists()
// editing; Items[0].Body = "new" → HasUnsavedChanges true, reload still old; SaveIfDirty() → reload "new", HasUnsavedChanges false
[Fact] public void TurningOffEdit_SavesImmediately()
[Fact] public void Shutdown_SavesPendingEdits()
[Fact] public void HideFromScreenShare_OnlyChangesWhileEditing()
[Fact] public void AlwaysOnTop_ChangesWhileLocked()
[Fact] public void IsEmpty_TracksItemCount()
[Fact] public void LoadFailure_ShowsErrorAndNeverOverwritesFiles()
// junk bytes in items.dat and items.dat.bak; new VM → Errors == [Messages.LoadFailed];
// IsEditing = true; AddItem; Shutdown() → both files' bytes unchanged
[Fact] public void SaveFailure_ShowsErrorOnceUntilASaveSucceeds()
// seed; open items.dat with FileShare.None; edit body + SaveIfDirty() twice → Errors.Count(e => e == Messages.SaveFailed) == 1, HasUnsavedChanges true;
// release lock; SaveIfDirty() → saved; lock again, edit, SaveIfDirty() → second SaveFailed shown
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Scrapbook.Core.Tests --filter "FullyQualifiedName~MainViewModelTests"`
Expected: build fails, because `MainViewModel` is not defined.

- [ ] **Step 3: Implement `MainViewModel`**

Subscribe to each item's `PropertyChanged`, including items added later, and unsubscribe from deleted ones. Propagate `IsEditing` to every item's `IsEditable`, and set it on new items.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Scrapbook.Core.Tests`
Expected: all tests pass (14 here plus 22 from earlier tasks).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: main view model with edit mode and safe saving"
```

---

### Task 5: WPF app — list, edit mode, copy

**Files:**
- Create: `src/Scrapbook/Scrapbook.csproj`, `App.xaml(.cs)`, `MainWindow.xaml(.cs)`, `ScrapbookDialog.xaml(.cs)`, `Services/WpfClipboardService.cs`, `Services/WpfDialogService.cs`, `Behaviors/PasswordBoxAssist.cs`

**Interfaces:**
- Consumes: everything in `Scrapbook.Core`.
- Produces:
  - `WpfClipboardService : IClipboardService`
  - `WpfDialogService(Func<Window> owner) : IDialogService`, which shows `ScrapbookDialog`
  - `ScrapbookDialog` (a `Window`): `static bool ShowConfirm(Window owner, string message)` (Delete/Cancel buttons) and `static void ShowError(Window owner, string message)` (OK). Task 6 applies display affinity to it.
  - `PasswordBoxAssist.BoundPassword` attached property (two-way)

- [ ] **Step 1: Scaffold the app**

```bash
dotnet new wpf -n Scrapbook -o src/Scrapbook
dotnet add src/Scrapbook reference src/Scrapbook.Core
dotnet sln add src/Scrapbook
```
In the csproj: `<TargetFramework>net10.0-windows</TargetFramework>`, `<Nullable>enable</Nullable>`, `<NoWarn>$(NoWarn);WPF0001</NoWarn>`. In `App.xaml`, set `ThemeMode="System"` and remove `StartupUri`.

- [ ] **Step 2: Compose in `App.OnStartup`**

Create `ItemStore(ItemStore.DefaultDirectory)` and `SettingsStore(ItemStore.DefaultDirectory)`, load the settings, then build `MainViewModel` with the WPF services and show `MainWindow`. Start a 1-second `DispatcherTimer` that calls `vm.SaveIfDirty()`. In `MainWindow.Closing`, call `vm.Shutdown()`.

- [ ] **Step 3: Build `MainWindow.xaml` to the spec's mockup**

- **Top bar:** checkboxes `Always on top` and `Hide from screen share` (disabled unless `IsEditing`), plus `Edit` as a `ToggleButton` or checkbox.
- **List:** an `ItemsControl` over `Items`, with `Messages.EmptyList` shown when `IsEmpty`.
- **Each row:**
  - The description sits above the body. It is a `TextBox`, read-only unless `IsEditable`.
  - The body is a single-line `TextBox` (horizontal scroll, `UpdateSourceTrigger=PropertyChanged`, read-only unless editable) when not masked. When masked it is a `PasswordBox` bound via `PasswordBoxAssist`, which is not focusable or hit-testable unless editable.
  - The `Mask` checkbox is enabled only when `IsEditable`.
  - The `Copy` button shows `Copied ✓` while `IsCopied`.
  - The `×` delete button shows only in Edit mode.
- **Bottom:** `+ Add item`, shown only in Edit mode.
- **New items:** on `ItemAdded`, scroll the new row into view and focus its description box.

- [ ] **Step 4: Implement `WpfClipboardService.TrySetText`**

Build a `DataObject` with `DataFormats.UnicodeText`. When `sensitive`, also add:
- `"ExcludeClipboardContentFromMonitorProcessing"` (a `MemoryStream` of 4 zero bytes)
- `"CanIncludeInClipboardHistory"` (`MemoryStream(BitConverter.GetBytes(0))`)
- `"CanUploadToCloudClipboard"` (`MemoryStream(BitConverter.GetBytes(0))`)

Call `Clipboard.SetDataObject(data, copy: true, retryTimes: 10, retryDelay: 50)`. Catch `ExternalException`/`COMException` and return false.

- [ ] **Step 5: Build and verify by hand**

Run: `dotnet build` (expected: 0 errors), then `dotnet run --project src/Scrapbook`.
Check each of these:
1. The app opens locked. Text can't be edited, Mask is disabled, and there's no Add or ×.
2. Turn Edit on. Add three items: a URL, a username, and a password with Mask on. The password box shows dots only.
3. Delete asks for confirmation, quoting the description and never the password.
4. Turn Edit off, then click Copy on each item and paste into Notepad. The text matches exactly, and the button shows `Copied ✓` for about 1.5 s.
5. Press Win+V. The URL and username are in clipboard history; **the password is not**.
6. Close and relaunch. The items are all still there, in order, and the app starts locked.
7. Open `%LOCALAPPDATA%\Scrapbook\items.dat` in Notepad. It is binary with no readable text.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: WPF shell with list, edit mode and sensitive copy"
```

---

### Task 6: Window behaviour — on top, screen-share hiding, placement, single instance

**Files:**
- Create: `src/Scrapbook/Interop/DisplayAffinity.cs`
- Modify: `src/Scrapbook/App.xaml.cs`, `src/Scrapbook/MainWindow.xaml.cs`, `src/Scrapbook/ScrapbookDialog.xaml.cs`, `src/Scrapbook/Services/WpfDialogService.cs`

**Interfaces:**
- Consumes: `MainViewModel.AlwaysOnTop/HideFromScreenShare` (Task 4), `SettingsStore`, `WindowPlacement` (Task 2), `ScrapbookDialog` (Task 5).
- Produces: `static class DisplayAffinity { static void Apply(Window window, bool hide); }`. It calls `SetWindowDisplayAffinity(hwnd, hide ? WDA_EXCLUDEFROMCAPTURE (0x11) : WDA_NONE (0))` from `user32.dll`. When the window has no handle yet, it applies on `SourceInitialized` instead.

- [ ] **Step 1: Always on top**

Bind `MainWindow.Topmost` to `AlwaysOnTop`. Dialogs are owned by the main window, so they also stay above it.

- [ ] **Step 2: Hide from screen share**

Apply `DisplayAffinity` to `MainWindow` at `SourceInitialized` and whenever `HideFromScreenShare` changes. `WpfDialogService` applies the current value to each `ScrapbookDialog` before it is shown.

- [ ] **Step 3: Persist and restore settings**

- **On startup:** if the saved `Left/Top/Width` pass `WindowPlacement.IsOnScreen` against `SystemParameters.VirtualScreenLeft/Top/Width/Height`, restore position and size. Otherwise use the defaults: about 420×560, centred.
- **When saving:** save `WindowSettings` when either checkbox changes and on `Closing`. Use `RestoreBounds` when the window is maximised.

- [ ] **Step 4: Single instance**

In `App.OnStartup`, create a `Mutex` named `Local\Scrapbook.SingleInstance`.
- **If it already exists:** signal the `EventWaitHandle` named `Local\Scrapbook.Activate` and `Shutdown()`.
- **Otherwise:** create that event and register a wait (`ThreadPool.RegisterWaitForSingleObject`). The wait dispatches to the UI thread and restores the window if minimised, then calls `Activate()`.

- [ ] **Step 5: Build and verify by hand**

Run: `dotnet run --project src/Scrapbook`.
Check each of these:
1. With Always on top ticked, click another window. Scrapbook stays above it. Untick it and Scrapbook goes behind. Do both while locked.
2. While locked, Hide from screen share is greyed out. In Edit mode it can be toggled.
3. Start a Teams or Zoom screen share, or a Snipping Tool screen recording, with hiding on. Scrapbook is absent from the shared image, and so is the delete-confirmation dialog. Turn hiding off and Scrapbook appears.
4. Turn hiding off, close, and relaunch. It is still off, and the window is where you left it.
5. Edit `settings.json` and set `"left": 5000`, then relaunch. The window appears centred on screen.
6. Launch the app a second time. No new window opens; the existing one comes to the front.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: always on top, screen-share hiding, placement, single instance"
```

---

### Task 7: Drag-to-reorder and release build

**Files:**
- Modify: `src/Scrapbook/MainWindow.xaml(.cs)`, `src/Scrapbook/Scrapbook.csproj`

**Interfaces:**
- Consumes: `MainViewModel.MoveItem(int fromIndex, int toIndex)` (Task 4).

- [ ] **Step 1: Drag-to-reorder**

In Edit mode each row shows a drag handle, and dropping a row calls `vm.MoveItem(from, to)`. The implementation is the builder's choice. There is no handle or drag while locked.

- [ ] **Step 2: Release settings**

In `Scrapbook.csproj`, set `<PublishSingleFile>true</PublishSingleFile>`, `<SelfContained>false</SelfContained>`, `<RuntimeIdentifier>win-x64</RuntimeIdentifier>` and `<AssemblyName>Scrapbook</AssemblyName>`.

- [ ] **Step 3: Run the full test suite**

Run: `dotnet test`
Expected: all tests pass.

- [ ] **Step 4: Publish and verify by hand**

Run: `dotnet publish src/Scrapbook -c Release -o publish`
Expected: `publish/Scrapbook.exe` exists as a single file.
Check each of these:
1. Run `publish/Scrapbook.exe` from Explorer. Your existing items load.
2. In Edit mode, drag the third item to the top. Close and relaunch; the new order is kept.
3. While locked, rows cannot be dragged.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: drag-to-reorder and single-file release build"
```
