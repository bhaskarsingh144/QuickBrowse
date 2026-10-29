# `src/` — QuickBrowse

WinForms (.NET Framework 4.5+). Flat layout like QuickView / JumpDock.

| File | Role |
| --- | --- |
| `Program.cs` | Mutex, DPI, startup, message loop |
| `ShellHost.cs` | Park/reveal (QuickView Decide→Place), idle, tray |
| `RailForm.cs` | Places rail (measure-driven cells) |
| `BrowseForm.cs` | Detached list/tiles submenu |
| `ShellChrome.cs` | `ShellEdge.Left` + companions |
| `ChromeLayout.cs` | Geometry + `RailCellMetrics` / FontEm |
| `ChromeVisual.cs` | Rounded regions, opaque chrome |
| `DisplayScale.cs` | Fit × TextFit (QuickView-synced) |
| `PlacesCatalog.cs` / `DirectoryModel.cs` / `IconCache.cs` / `Prefs.cs` / `UiTheme.cs` | Data + theme |

Build: `..\scripts\build.ps1` → `..\bin\QuickBrowse.exe`
