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
- [ ] Open; **Open with** cascade (handlers + Choose another app…); **Open in** (Cursor / VS Code / IntelliJ / etc. when installed)
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

## Regression

- [ ] No return of in-process `IContextMenu` crashes
- [ ] QuickView + JumpDock unaffected
