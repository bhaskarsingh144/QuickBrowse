# Manual test matrix — Browse context menus + file ops

Run `scripts\test.ps1` then `scripts\build.ps1` / launch `bin\QuickBrowse.exe`.

**Expectation:** managed dark **rounded** QuickBrowse menu (not shell `IContextMenu`), with Win11-like icon row and curated verbs. Escape hatch: Show in Explorer.

## Automated gate

- [ ] `scripts\test.ps1` — ShellFileOpsTests, FileCopyTests, ZipOpsTests, SendToCatalogTests

## Item menu

- [ ] Right-click folder/file — **rounded** corners; top icon row Cut / Copy / Paste / Rename / Delete (tooltips)
- [ ] Open / Open with… (files only) / Pin (folders) / Send to ▸ / Compress or Extract / Copy path / Explorer / Properties
- [ ] Send to Desktop / Documents / Downloads copies the item
- [ ] Compress non-zip → sibling `.zip`; Extract `.zip` → sibling folder
- [ ] Paste icon enabled when clipboard has files and current folder is writable
- [ ] Drive on This PC — Cut/Rename/Delete/Send to disabled; Open / Properties ok

## Background menu

- [ ] Empty area — New folder / Paste / Refresh / Explorer / Properties
- [ ] This PC background — New folder / Paste / Properties disabled

## Header organize row

- [ ] Separate **Cut / Copy / Paste / Delete** chips (no clipboard dropdown)
- [ ] Enable rules match selection + clipboard; Ctrl+C / Ctrl+X / Ctrl+V still work

## Coexistence

- [ ] QuickView + JumpDock unaffected
