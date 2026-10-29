# Manual test matrix — Shell context menus

Run `scripts\test.ps1` then `scripts\build.ps1` / launch `bin\QuickBrowse.exe`.

**Expectation:** menus are the classic/full shell menu (same verbs as Win11 **Show more options**), not Explorer’s compact Fluent icon row. Per-type commands and third-party apps come from Windows.

## Automated gate

- [ ] `scripts\test.ps1` — includes ShellContextMenuTests

## Item menu

- [ ] Right-click a **folder** → Pin/Unpin at top; Open, Cut/Copy, Delete, Rename, Properties present
- [ ] Right-click a **.mp4 / video** → Open / Open with (submenu works); app-specific verbs if installed
- [ ] Right-click a **.jpg / image** → Open with / Edit with / Properties as registered
- [ ] Right-click a **.txt / document** → Open / Open with / Edit as registered
- [ ] Right-click a **drive** on This PC → drive-appropriate verbs (Properties, etc.)
- [ ] **Properties** opens the Windows property sheet
- [ ] **Delete** from shell menu → Recycle Bin; list refreshes
- [ ] **Rename** (if used) → list refreshes after
- [ ] Shift+right-click → extended verbs appear when the shell provides them
- [ ] Pin folder / Unpin folder still works; flyout list updates

## Background menu

- [ ] Right-click **empty area** in a folder → New / Paste / View-style shell verbs
- [ ] New folder (or similar) → list refreshes and shows the new item
- [ ] Right-click empty area on **This PC** → does not crash; shell background menu if available

## Toolbar / keys

- [ ] Toolbar Delete / Delete key → confirm → Recycle Bin (not permanent wipe)
- [ ] Left-click / Enter still open folders in-pane and files via default shell verb

## Fallback / coexistence

- [ ] If shell menu fails for a path, thin fallback (Open / Pin / Copy path / Delete / Show in Explorer) still appears
- [ ] QuickView + JumpDock unaffected while using menus
