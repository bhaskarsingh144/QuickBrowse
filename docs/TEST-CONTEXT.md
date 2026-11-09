# Manual test matrix — Browse context menus + file ops

Run `scripts\test.ps1` then `scripts\build.ps1` / launch `bin\QuickBrowse.exe`.

**Expectation:** managed dark **rounded** QuickBrowse menu (not shell `IContextMenu`), with Win11-like icon row and curated verbs. Escape hatch: Show in Explorer.

## Automated gate

- [ ] `scripts\test.ps1` — ShellFileOpsTests, FileCopyTests, ZipOpsTests, SendToCatalogTests, OpenInCatalogTests, ArchiveCatalogTests, ChromeLayoutTests (Ctx DPI)

## Scale / DPI (Phase A)

- [ ] At 100% / 125% / 150% / 200%: icon-row buttons and list rows are comfortably clickable; menu font readable
- [ ] Top icon row Cut / Copy / Paste / Rename / Delete still present (tooltips)
- [ ] Same five actions also appear as **list items** under the icon row; both paths work

## Item menu

- [ ] Right-click folder/file — **rounded** corners
- [ ] Open; single **Open with** cascade (no separate Open in) — type-relevant apps + **Choose another app…** on files; folders get editors/IDEs only
- [ ] Pin (folders) / Send to ▸ (Desktop / Documents / Downloads + removable drives) / Compress ▸ / Extract ▸
- [ ] Copy path / Copy name / Copy as path / Copy SHA-256 (files) / Duplicate / Create shortcut
- [ ] Open in Terminal ▸ (folders) / Run as administrator (exe/bat/ps1)
- [ ] Show in Explorer / Properties
- [ ] Compress: ZIP always; WinRAR / 7-Zip / NanaZip when installed (no shell extensions)
- [ ] Extract visible only for archives; built-in ZIP + tool extract here / to folder
- [ ] Paste icon + list enabled when clipboard has files and folder is writable
- [ ] Drive on This PC — Cut/Rename/Delete/Send to disabled; Open / Properties ok

## Background menu

- [ ] Empty area — New folder / New Text Document / Paste / Open in Terminal / Refresh / Explorer / Properties
- [ ] This PC background — New folder / New Text / Paste / Terminal / Properties disabled

## Header organize row

- [ ] Separate **Cut / Copy / Paste / Delete** chips (no clipboard dropdown)
- [ ] Enable rules match selection + clipboard; Ctrl+C / Ctrl+X / Ctrl+V still work

## Open with type matrix

- [ ] `.mp4` / video — VLC / PotPlayer / MPC-HC when installed; not Excel/Word; Choose another app… present
- [ ] `.pdf` — Edge / Acrobat / Sumatra when installed; not VLC unless it is the default assoc
- [ ] `.xlsx` — Excel when installed
- [ ] `.txt` / `.cs` — editors (Cursor / VS Code / Notepad++ / …)
- [ ] Folder — Open with lists editors only (no Choose another app…)
- [ ] Picking the default-assoc row launches that exe (not “Could not open in assoc”)
- [ ] Successful Open with updates Recent + that app’s jump list (see TEST-RECENT.md)

## Regression

- [ ] No return of in-process `IContextMenu` crashes
- [ ] QuickView + JumpDock unaffected
- [ ] No duplicate **Open in** menu item
