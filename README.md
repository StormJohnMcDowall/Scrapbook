# Scrapbook

A small Windows app that keeps the bits of text you need during a live demo (URLs, usernames,
passwords) one click away, without showing your passwords to the audience.

Each item has a **description**, a **body** (the text itself), a **Copy** button and a **Mask**
checkbox. Masked bodies show as dots, like a password box.

## Features

- **One-click copy.** Click **Copy** to put an item's text on the clipboard. The button shows
  *Copied ✓* briefly so you know it worked.
- **Masking.** Masked items are never shown on screen. When you copy a masked item, Scrapbook
  also asks Windows to keep it out of clipboard history (Win+V) and cloud clipboard sync.
- **Hide from screen share** (on by default). The Scrapbook window doesn't appear in screen
  shares, recordings or screenshots, so you can keep it open while presenting. Your choice is
  remembered.
- **Always on top.** Keeps the window floating above whatever you're demoing.
- **Edit mode.** The list is locked by default so a stray click can't change it. Turn on
  **Edit** to add, change, reorder (drag the ⋮⋮ handle) or delete items, mask or unmask them,
  or change *Hide from screen share*.
- **Encrypted on disk.** Items are encrypted with your Windows login, so no one else can read
  them, even with access to the file.
- **Saves automatically.** Changes are saved within a second. Each save also keeps a backup of
  the previous version.
- **Light and dark mode.** Follows your Windows theme and switches live when it changes.
- **One copy at a time.** Launching Scrapbook again brings the open window to the front.

## Running it

Requires Windows 10 (version 2004 or later) or Windows 11, and the
[.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0).

Run `publish\Scrapbook.exe` after building it (see below). It's a single file and can live
anywhere; pin it to the taskbar for quick access.

## Where your data lives

Everything is stored in `%LOCALAPPDATA%\Scrapbook`:

| File | Contents |
|---|---|
| `items.dat` | Your items, encrypted with your Windows login (DPAPI) |
| `items.dat.bak` | The previous save, used automatically if `items.dat` can't be read |
| `settings.json` | Window position and size, *Always on top*, *Hide from screen share* (not secret) |

Because the encryption is tied to your Windows account, **`items.dat` can't be opened on
another PC or by another user**, so copying it to a new machine won't bring your items with it.

If Scrapbook can't read your saved items at startup, it tells you and **won't save anything**
until the problem is fixed, so it never overwrites items it couldn't read.

## Good to know before a demo

- Pasting a masked item puts the real text wherever you paste it. Masking only hides it inside
  Scrapbook.
- *Hide from screen share* uses a Windows feature that capture tools have to respect. Do one test
  share with the tool you'll actually present with before relying on it.
- Tooltips and right-click menus aren't hidden from screen share. They never contain item text.

## Building from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet build Scrapbook.slnx                 # build everything
dotnet test Scrapbook.slnx                  # run the tests
dotnet publish src\Scrapbook\Scrapbook.csproj -c Release -o publish   # -> publish\Scrapbook.exe
```

### Project layout

| Path | What's there |
|---|---|
| `src/Scrapbook.Core` | Storage (encryption, atomic saves, backups), settings and view models. No UI code, so it's fully unit-tested. |
| `src/Scrapbook` | The WPF app: windows, clipboard, screen-share hiding, single-instance handling. |
| `tests/Scrapbook.Core.Tests` | xUnit tests for `Scrapbook.Core`. |
| `tools/make-icon.ps1` | Regenerates `src/Scrapbook/Scrapbook.ico`. Edit the colours or shapes there, then run it. |
| `docs/superpowers` | The original design and implementation plan. |

Built with WPF on .NET 10 (Fluent theme), [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)
and `System.Security.Cryptography.ProtectedData`.
