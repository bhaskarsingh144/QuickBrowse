# Architecture (proposed)

Defaults locked — implement against [`OPEN-QUESTIONS.md`](OPEN-QUESTIONS.md).

**Window relationship:** Approach **B** — two forms (`RailForm` + `BrowseForm`) driven by `ShellHost`.

## Process model

```text
QuickBrowse.exe (single instance)
├── RailForm          left-edge host (park/reveal owner)
├── BrowseForm        attached pane (child logically; may be owned HWND)
├── PlacesCatalog     Known Folder + This PC + pins
├── DirectoryModel    enumerate / watch (optional FileSystemWatcher later)
├── ShellChrome       local copy; must support ShellEdge.Left
├── DisplayScale      DPI × Fit (pattern from siblings)
└── IconCache         SHGetFileInfo / ImageList
```

## Window relationship (“same head”)

**Requirement:** rail and browse pane show/hide/park as one unit.

Park algorithm: extend sibling ShellChrome with `Left` edge (mirror of `Right` in QuickView). JumpDock’s vendored ShellChrome currently lacks `Left` — **copy into this repo and extend**; do not break JumpDock.

## Places resolution

Use Windows Known Folder IDs (`FOLDERID_Downloads`, `Desktop`, `Documents`, …) via `SHGetKnownFolderPath` — not hard-coded `C:\Users\...` strings.

This PC: enumerate `DriveInfo.GetDrives()` + optionally shell “This PC” namespace later.

## Directory listing

v0: synchronous enumerate on background thread → marshal to UI; cap or virtualize if >N entries.

Do **not** host `IExplorerBrowser` in v0 unless OPEN-QUESTIONS demands full shell fidelity (heavier, less custom aesthetics).

## Input pipeline

- Mouse: click, wheel, hover reveal.
- Touch: WM_POINTER / Touch events; larger hit rects; see TOUCH.md.
- Keyboard (later): Up, Enter, Alt+Up, Esc close pane.

## Coexistence with siblings

```text
┌─────────────────────────────────────────────┐
│                 JumpDock (top)              │
├──────┬──────────────────────────────┬───────┤
│Quick │                              │Quick  │
│Browse│     user's normal apps       │View   │
│(left)│                              │(right)│
└──────┴──────────────────────────────┴───────┘
```

No IPC required for v0. Optional later: JumpDock “open folder” → QuickBrowse.

## Suggested source files (when coding starts)

```text
src/
  Program.cs
  RailForm.cs
  BrowseForm.cs
  ShellHost.cs         // park/reveal, idle, tray
  PlacesCatalog.cs
  DirectoryModel.cs
  IconCache.cs
  ShellChrome.cs       // local, with Left
  DisplayScale.cs
  UiTheme.cs
```

## Build

Mirror siblings: `scripts/build.ps1` → `bin/QuickBrowse.exe`, `scripts/test.ps1`, standalone bat. No NuGet unless stack decision changes.
