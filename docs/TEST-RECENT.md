# Manual test matrix — Recent flyout

Run `scripts\test.ps1` then `scripts\build.ps1` / launch `bin\QuickBrowse.exe`.

**Source:** Windows Recent Items (`%AppData%\Microsoft\Windows\Recent`). Executables are filtered (OS + QB). Not recent apps.

**Jump lists:** Folder browse also writes Explorer Automatic Destinations (`SHARD_APPIDINFO` + `Microsoft.Windows.Explorer`). Opening a file (default or Open with) writes system Recent plus the handler app’s AppUserModelID destinations (taskbar jump list for Explorer / VLC / PotPlayer / etc. when resolvable).

**UI:** Dense list flyout beside the rail (Pins-like), with date section headers, relative time on each row, and scroll when tall. Not a pane-sized icon grid.

## Automated gate

- [ ] `scripts\test.ps1` — AppIdResolverTests + RecentSourceTests (buckets, relative time, cap, notify guards) + PinSessionTests

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

## Taskbar jump lists

- [ ] Browse into a normal folder in QuickBrowse → pin File Explorer on the taskbar → jump list / Frequent shows that folder (may need a moment / re-open jump list)
- [ ] Open a video via default association (or Open with → VLC / PotPlayer when installed) → that player’s taskbar jump list includes the file
- [ ] Open a document via Open with → editor → that app’s jump list updates
- [ ] Opening `.exe` never adds Recent / jump destinations

## Coexistence

- [ ] Idle ~30s / park / Esc close Recent
- [ ] QuickView / JumpDock unaffected (JumpDock may pick up new AutomaticDestinations on its own Refresh)
