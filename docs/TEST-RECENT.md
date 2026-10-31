# Manual test matrix — Recent panel

Run `scripts\test.ps1` then `scripts\build.ps1` / launch `bin\QuickBrowse.exe`.

**Source:** Windows Recent Items (`%AppData%\Microsoft\Windows\Recent`). Executables are filtered (OS + QB). Not recent apps.

**UI:** Dedicated pane-slot panel (date sections + icon grid). Pins stay the slim flyout.

## Automated gate

- [ ] `scripts\test.ps1` — includes RecentSourceTests (buckets, relative time, cap) + PinSessionTests

## Rail

- [ ] Top chips are **Pins** then **Recent**, then system places
- [ ] Pins tooltip “Pinned folders”; Recent “Recent files and folders”
- [ ] Pins and Recent are mutually exclusive (opening one closes the other)
- [ ] Opening Recent closes the Explorer pane (panel uses the same slot)
- [ ] Opening a place or Pins closes Recent

## Recent panel

- [ ] Panel fills the browse-pane slot (not the 188 DIP Pins strip)
- [ ] Header shows **Recent** + close; Esc / × closes
- [ ] Sections appear as needed: **Today**, **Yesterday**, **Earlier this week**, **Older**
- [ ] Tiles show shell icon, name, relative time; tooltip = full path
- [ ] Vertical scroll when many items
- [ ] Open a document in Explorer (or in QB) → appears in Recent (may need up to ~5s while panel open, or re-open Recent)
- [ ] Folder from Recent → opens in pane; Recent panel closes
- [ ] File from Recent → opens with default app; Recent closes; pane stays closed unless already open
- [ ] Empty state: muted copy + hint about Explorer / QuickBrowse
- [ ] No `.exe` entries
- [ ] Right-click tile → Open / Show in Explorer / Copy path

## Pins regression

- [ ] Pins chip still opens the slim flyout; Open / Unpin; pin toolbar still works

## Coexistence

- [ ] Idle ~30s closes Recent panel
- [ ] Park hides panel; left-edge reveal still works
- [ ] QuickView / JumpDock unaffected
