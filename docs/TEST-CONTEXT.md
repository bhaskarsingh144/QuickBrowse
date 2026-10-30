# Manual test matrix — Browse context menus

Run `scripts\test.ps1` then `scripts\build.ps1` / launch `bin\QuickBrowse.exe`.

**Expectation:** right-click uses a managed dark QuickBrowse menu (not shell `IContextMenu`). Verbs map to in-app actions; Properties uses the Windows property sheet when available.

## Automated gate

- [ ] `scripts\test.ps1` — includes ShellFileOpsTests

## Item menu

- [ ] Right-click a **folder** → does not crash; Open, Pin/Unpin, Cut/Copy, Copy path, Rename, Delete, Show in Explorer, Properties
- [ ] Right-click a **file** → Open, Cut/Copy, Copy path, Rename, Delete, Show in Explorer, Properties (no Pin)
- [ ] Right-click a **drive** on This PC → Open / Copy path / Explorer / Properties; Cut/Rename/Delete/Pin disabled
- [ ] **Pin folder / Unpin folder** updates flyout and pin chrome
- [ ] **Delete** → confirm → Recycle Bin; list refreshes
- [ ] **Rename** → prompt → list refreshes and selects the new name
- [ ] **Properties** opens the Windows property sheet (or Explorer select fallback)

## Background menu

- [ ] Right-click **empty area** in a folder → New folder / Paste / Refresh / Open in Explorer / Properties
- [ ] Paste enabled only when clipboard has files and folder is writable
- [ ] New folder creates and list refreshes
- [ ] Right-click empty area on **This PC** → does not crash; New folder / Paste / Properties disabled

## Toolbar / keys

- [ ] Toolbar Delete / Delete key → confirm → Recycle Bin
- [ ] Left-click / Enter still open folders in-pane and files via shell execute

## Coexistence

- [ ] QuickView + JumpDock unaffected while using menus
