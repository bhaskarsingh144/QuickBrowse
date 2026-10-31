# Manual test matrix — Recent flyout

Run `scripts\test.ps1` then `scripts\build.ps1` / launch `bin\QuickBrowse.exe`.

**Source:** Windows Recent Items (`%AppData%\Microsoft\Windows\Recent`). Executables are filtered (OS + QB). Not recent apps.

**UI:** Dense list flyout beside the rail (Pins-like), with date section headers, relative time on each row, and scroll when tall. Not a pane-sized icon grid.

## Automated gate

- [ ] `scripts\test.ps1` — RecentSourceTests (buckets, relative time, cap) + PinSessionTests

## Rail

- [ ] Top chips are **Pins** then **Recent**, then system places
- [ ] Pins and Recent are mutually exclusive
- [ ] Opening either closes the Explorer pane first

## Recent list

- [ ] Flyout sits beside the rail (wider than Pins); not the browse-pane slot
- [ ] Sections: **Today** / **Yesterday** / **Earlier this week** / **Older** as needed
- [ ] Each row: shell icon + title + relative time (right); tooltip = full path
- [ ] Scroll works cleanly (no smear / stuck headers)
- [ ] Folder → pane; file → default app; flyout closes
- [ ] Empty state readable; no `.exe` entries
- [ ] Right-click → Open / Show in Explorer / Copy path

## Pins regression

- [ ] Pins chip still slim list; Open / Unpin

## Coexistence

- [ ] Idle ~30s / park / Esc close Recent
- [ ] QuickView / JumpDock unaffected
