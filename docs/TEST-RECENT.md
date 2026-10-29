# Manual test matrix — Recent flyout

Run `scripts\test.ps1` then `scripts\build.ps1` / launch `bin\QuickBrowse.exe`.

**Source:** Windows Recent Items (`%AppData%\Microsoft\Windows\Recent`). Executables are filtered (OS + QB). Not recent apps.

## Automated gate

- [ ] `scripts\test.ps1` — includes RecentSourceTests + updated PinSessionTests

## Rail

- [ ] Top chips are **Pins** then **Recent**, then system places
- [ ] Pins tooltip “Pinned folders”; Recent “Recent files and folders”
- [ ] Pins and Recent are mutually exclusive (opening one closes the other)
- [ ] Opening either closes the Explorer pane first (flyout not buried)

## Recent list

- [ ] Open a document in Explorer (or in QB) → appears in Recent (may need up to ~5s while flyout open, or re-open Recent)
- [ ] Folder from Recent → opens in pane; flyout closes
- [ ] File from Recent → opens with default app; flyout closes; pane stays closed unless already open
- [ ] Empty state readable when nothing recent
- [ ] No `.exe` entries
- [ ] Right-click Recent row → Open / Show in Explorer

## Pins regression

- [ ] Pins chip still lists pins; Open / Unpin; pin toolbar still works

## Coexistence

- [ ] Idle ~30s closes Recent flyout
- [ ] Park hides flyout; left-edge reveal still works
- [ ] QuickView / JumpDock unaffected
