# Scrapbook — Design

Date: 2026-11-25
Status: Draft, awaiting approval

## Intent

Scrapbook is a small native Windows app holding a list of text snippets
(URLs, usernames, passwords) that the owner can copy to the clipboard with
one click **during live demos**. The whole point is speed and discretion in
front of an audience: grab the right text instantly without switching
windows, and never expose a password on screen.

It is a personal tool for one user on their own Windows 11 machine.

## Goals

- A list of items, each with a **description**, a **body** (the text), a
  **Copy** button and a **Mask** checkbox.
- Masked bodies display like a password box (dots only).
- One-click copy of an item's body to the system clipboard.
- **Always on top** checkbox so the window floats over the demo.
- **Hide from screen share** checkbox, on by default.
- Items persist between launches, encrypted on disk with the user's Windows
  login.
- A global **Edit** mode, so the list can't be changed by accident while
  presenting.

## Non-goals

- Syncing between machines, import/export, or sharing items with others.
- A master password or any login prompt.
- Global hotkeys, tray icon, or copying by keyboard shortcut.
- Auto-clearing the clipboard after a delay.
- Categories, search, or multiple lists.
- Installer or auto-update.

## Anti-goals (would be a failure even if it "works")

- A password becomes visible to the audience at any point the user didn't
  deliberately choose (e.g. a masked value appearing in clipboard history,
  a reveal-on-hover, a tooltip, an error message, or a log).
- A stray click or keypress during a demo changes or deletes an item.
- Saved items readable in plain text on disk.
- Losing saved items (corrupt or half-written save file).

## Constraints

- Windows 11 (the owner's machine). .NET 10 SDK is installed.
- Native desktop app, single window.

## Approaches considered

| Approach | Trade-off | Verdict |
|---|---|---|
| **WPF on .NET 10** | Mature; built-in Fluent (Windows 11) theme; topmost, DPAPI and clipboard formats are a few lines each; publishes as a single .exe; SDK already installed. | **Chosen** |
| WinUI 3 (Windows App SDK) | Most native Windows 11 look, but needs extra tooling plus packaging/installer or a much larger self-contained bundle — heavy for a one-window tool. | Rejected |
| WinForms | Simplest, but dynamic rows and drag-reorder are clumsy and it looks dated on Windows 11. | Rejected |

The owner handed the "how" to Claude and accepted WPF.

## Decided by the owner

### Window

```
┌ Scrapbook ─────────────────────────────────────── ─ □ × ┐
│ [✓] Always on top  [✓] Hide from screen share   [ ] Edit │
├──────────────────────────────────────────────────────────┤
│ Demo site URL                                            │
│ [https://demo.contoso.com     ]  [ ]Mask  [Copy]         │
│ Admin username                                           │
│ [admin@contoso.com            ]  [ ]Mask  [Copy]         │
│ Admin password                                           │
│ [••••••••••••                 ]  [✓]Mask  [Copy]         │
├──────────────────────────────────────────────────────────┤
│                                    (Edit mode: [+ Add])  │
└──────────────────────────────────────────────────────────┘
```

In Edit mode each row also shows a delete (×) control and a drag handle.

### Edit mode (global)

- A single **Edit** toggle in the top bar.
- **The app always starts locked** (Edit off), regardless of how it was
  closed.
- **Locked:** in the list, only Copy works. Descriptions and bodies are
  read-only, Mask checkboxes are disabled, no Add, no delete, no
  reordering. In the top bar, **Always on top stays clickable**, but
  **Hide from screen share is disabled** (it can only be changed in Edit
  mode), so a stray click mid-demo can't expose the window.
- **Edit on:** descriptions and bodies are editable in place, Mask can be
  toggled, **+ Add item** is shown, each row has × (delete, with
  confirmation) and can be drag-reordered.

### Masking

- A masked body is shown as dots only, in both modes. In Edit mode it is
  typed into like a password box (still dots).
- There is no reveal button, hover-reveal or tooltip. To see a masked value
  the user unticks Mask (only possible in Edit mode).

### Copy

- Copy puts the body on the clipboard as plain text.
- The button briefly shows "Copied ✓" as feedback.
- **For masked items**, the clipboard data is also marked so Windows keeps it
  out of clipboard history (Win+V) and cloud clipboard sync. Unmasked items
  copy normally.

### Always on top

- A checkbox; when on, the window stays above all other windows.

### Hide from screen share

- A checkbox, **on by default** on first launch. After that it **remembers
  the user's last choice** between launches (owner's decision, accepting
  that turning it off once carries into the next session).
- Can only be changed while Edit mode is on (see Edit mode).
- When on, the window is excluded from screen capture: Teams, Zoom, OBS
  etc. show the desktop with Scrapbook absent, while the user still sees it
  locally. Scrapbook's own pop-ups (e.g. delete confirmation, error
  messages) are excluded too.
- Known limit (accepted): a projector or duplicated physical display shows
  exactly what the monitor shows, so masking remains the protection there.

### Storage

- Items are saved automatically on every change (no Save button).
- Items are encrypted on disk with the user's Windows login, so the file is
  unreadable on another machine or account.

## Claude's calls (correctable)

- **Framework detail:** WPF on .NET 10 with `ThemeMode="System"` (Fluent
  theme, follows Windows light/dark). The API is marked experimental, so
  suppress that warning.
- **Encryption:** DPAPI (`ProtectedData`, `DataProtectionScope.CurrentUser`)
  over the whole serialized item list, with a fixed app-specific entropy
  value. The file is encrypted as a whole, not just the masked bodies, so
  descriptions don't leak what the passwords are for either.
- **Files:** `%LOCALAPPDATA%\Scrapbook\`
  - `items.dat` — DPAPI-encrypted JSON: an ordered list of
    `{ id, description, body, masked }`, plus a format version number.
  - `settings.json` — plain JSON, non-sensitive: window position/size,
    Always on top, Hide from screen share.
- **Safe saves:** write to a temp file, then atomically replace
  `items.dat`, keeping the previous version as `items.dat.bak`. If
  `items.dat` can't be decrypted or parsed on launch, try the `.bak`. If
  both fail, show an error, don't overwrite either file, and keep the app
  in a state where nothing gets saved over them.
- **Saving cadence:** save on each committed change (add, delete, reorder,
  Mask toggle, edit box losing focus or Edit mode being turned off), plus a
  short debounce while typing. Always save on exit.
- **Clipboard exclusion:** alongside the text, set the clipboard formats
  `ExcludeClipboardContentFromMonitorProcessing`,
  `CanIncludeInClipboardHistory` (DWORD 0) and `CanUploadToCloudClipboard`
  (DWORD 0). Retry briefly if the clipboard is busy; if the copy still
  fails, show a non-revealing message ("Couldn't copy — try again").
- **Screen-share hiding:** `SetWindowDisplayAffinity(hwnd,
  WDA_EXCLUDEFROMCAPTURE)` when on, `WDA_NONE` when off, applied once the
  window handle exists and whenever the checkbox changes. Apply the same to
  every Scrapbook dialog, which means using custom WPF dialog windows
  rather than `MessageBox` (whose handle the app doesn't control).
- **Window settings** (position, size, Always on top, Hide from screen
  share) are remembered between launches; Edit mode is never remembered. If the saved position is off-screen (e.g. a monitor was
  unplugged), reset to a visible default position.
- **Single instance:** only one copy of Scrapbook runs at a time; launching
  again brings the existing window to the front. Prevents two instances
  overwriting each other's saves.
- **New items** are added at the bottom with focus in the description box,
  unmasked by default.
- **Empty list:** shows "No items yet — turn on Edit to add one."
- **Copy is disabled** for an item with an empty body.
- **Long text:** the body box is single-line, with long values scrolling
  horizontally inside it. Descriptions are single-line.
- **Never log or display body text** anywhere outside its own box (no
  logging, no exception messages containing it).
- **Distribution:** publish as a single framework-dependent `.exe`.
  .NET 10 is already on the machine.
- **Tests:** automated tests for storage (round trip, encryption, `.bak`
  recovery, corrupt file handling) and the list/edit-mode logic.

## Left to the builder

- Project structure and MVVM approach (plain MVVM or a small toolkit such
  as CommunityToolkit.Mvvm).
- Drag-reorder implementation.
- Exact visual styling within the Fluent theme, spacing, and the window's
  default size.
- App icon.
