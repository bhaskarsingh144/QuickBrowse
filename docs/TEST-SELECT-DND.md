# Manual test matrix — Multi-select mode + drag/drop

Run `scripts\build.ps1`, `scripts\test.ps1`, then launch `bin\QuickBrowse.exe`.

## Automated gate

- [ ] `scripts\test.ps1` — BrowseSelectionRulesTests, ShellDragDropTests, ShellFileOpsTests

## Multi-select toggle

- [ ] Header row 2 shows a **Multi-select** chip between Refresh and Cut (tooltip "Multi-select mode (off)")
- [ ] Click chip: chip highlights, tooltip becomes "Multi-select mode (on)"
- [ ] Click chip again: highlight clears, selection is cleared
- [ ] Mode survives navigating into another folder (selection does not)
- [ ] Chip layout OK at 100% / 150% / 200% DPI (no overlap with View chip)

## Mode OFF (default behavior must not regress)

- [ ] Single click on a folder opens it; single click on a file opens it
- [ ] Only one item can ever be selected; Ctrl/Shift+click does not multi-select
- [ ] Click on empty space opens nothing
- [ ] Right-click an item selects only that item and shows the single-item menu
- [ ] Hover tip and hover highlight still work

## Mode ON — selection

- [ ] Click selects one item and does **not** open it
- [ ] Ctrl+click toggles items; Shift+click selects a range
- [ ] Ctrl+A selects everything (not while typing in path/filter box)
- [ ] Status bar shows "N selected" when more than one is selected; returns to "N items" when ≤ 1
- [ ] Esc with a selection clears it (pane stays open); Esc again closes the pane
- [ ] Esc still cancels path edit / clears filter first
- [ ] Double-click a folder/file opens it
- [ ] Enter opens the focused (or first) selected item

## Mode ON — toolbar and shortcuts

- [ ] Cut / Copy / Delete chips enabled with 2+ items selected; disabled if a drive is in the selection
- [ ] Ctrl+C then paste in another folder copies all selected items; status says "Copied N items to clipboard"
- [ ] Ctrl+X then paste moves all selected items
- [ ] Delete / Del key: confirm dialog says "Send N items to the Recycle Bin?" with first names, then "… and K more"
- [ ] After delete, listing reloads and items are gone (Recycle Bin has them)

## Mode ON — context menu with 2+ items

- [ ] Right-click an item **inside** the selection keeps the selection
- [ ] Right-click an item **outside** the selection selects only that item
- [ ] Enabled: Cut / Copy / Paste / Delete, Send to, Compress, Copy path, Duplicate, Create shortcut
- [ ] Hidden or disabled: Open, Open with, Open in, Rename, Pin, Extract, Copy name, Copy as path, Copy SHA-256, Run as administrator, Open in Terminal, Show in Explorer, Properties
- [ ] Copy path puts one path per line on the clipboard
- [ ] Send to Desktop copies every selected item; status says "Sent N items to …"
- [ ] Compress ▸ ZIP creates one archive per selected item
- [ ] With a drive in the selection: Cut / Copy / Delete / Send to / Compress disabled

## Drag out

- [ ] Mode OFF: press an item and drag past the threshold — Explorer / Desktop receives a **copy**; the item is not opened on release
- [ ] Mode ON: drag a multi-selection to Explorer — all items are copied
- [ ] Mode ON: pressing an unselected item and dragging selects it first (without modifiers)
- [ ] Shift while dropping in Explorer moves; QuickBrowse listing reloads after a move
- [ ] A short wiggle (below the system drag size) still counts as a click (opens in mode OFF)
- [ ] Drive entries on This PC cannot be dragged out

## Drop in

- [ ] Drag files from Explorer onto the QuickBrowse list: copied into the current folder; status shows "Copied N item(s)"
- [ ] Shift-drag moves; Ctrl-drag copies; cursor shows the matching effect
- [ ] Drop on **This PC** shows the "not allowed" cursor and does nothing
- [ ] Dropping a folder into itself reports "Cannot copy a folder into itself"
- [ ] Name collisions create unique names ("name (2)")
- [ ] Dragging items out of the list and dropping them back on the same list does nothing (no duplicates)
- [ ] Listing refreshes after the drop and selection is cleared

## Regression

- [ ] Pane still opens without stealing focus (`ShowWithoutActivation`)
- [ ] Owner-draw tiles / content / details still render hover and selection correctly
- [ ] Back / Up / path edit / filter unaffected
- [ ] Pins and Recent flyouts unaffected
- [ ] QuickView + JumpDock unaffected
