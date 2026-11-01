# Manual test matrix — MVP

Run `scripts\test.ps1`, then `scripts\setup-standalone.bat` (or `bin\QuickBrowse.exe` after `build.ps1`).

Pins: see [`TEST-PINS.md`](TEST-PINS.md). Recent: see [`TEST-RECENT.md`](TEST-RECENT.md). Context menus: see [`TEST-CONTEXT.md`](TEST-CONTEXT.md).

## Smoke

- [ ] Single instance: second launch exits quietly
- [ ] Rail floating lower-left (~96 DIP wide, center ~mid/lower work area)
- [ ] **Every rail label fully visible** (This PC, Desktop, Downloads, …) — no clip under next chip
- [ ] **Pins** + **Recent** chips at top of rail; flyouts beside rail
- [ ] Tooltip on place hover
- [ ] Click Downloads → detached pane opens with gap; lists folder
- [ ] Single-click folder enters; single-click file opens with shell
- [ ] Right-click item → shell menu (Open with / Properties / type verbs); empty area → background menu
- [ ] Back / Up / crumbs / Ctrl+L·F4 edit path / Filter (Ctrl+F) / New folder · Refresh · Clipboard / Del / Explorer / Pin / view (+ Show hidden) / ×
- [ ] Esc ladder: cancel path edit → clear filter → blur filter → close pane
- [ ] While Filter/path focused: typing works; Delete/Ctrl+C/X/V do not hit list actions
- [ ] Keys when list focused: F5 · Ctrl+Shift+N · Ctrl+C/X/V · Enter · Alt+Up · Delete
- [ ] View dropdown: Tiles / Content / Details + Show hidden; prefs survive restart
- [ ] This PC: New folder disabled; Paste blocked; Show hidden does not change drive list
- [ ] Access-denied / empty folder show status text **and** a centered in-pane overlay (Retry on errors)
- [ ] Large folders show a **Loading…** overlay (not a blank list) until items appear
- [ ] Filter with zero matches shows **No matching items** overlay; clear filter restores the list
- [ ] New folder / Rename / Duplicate land selection on the new item after reload
- [ ] Delete focuses a neighboring item (not a blank selection) when items remain
- [ ] Tray → Show / Exit
- [ ] Idle ~30s without activity closes pane (+ flyout if open)

## Shell / coexistence

- [ ] Maximize app → rail (+ pane if open) parks fully off-screen
- [ ] Hover left edge → reveals **above** maximized app (no Win+D needed)
- [ ] Open This PC pane → JumpDock/QuickView do **not** flicker (see [`TEST-SHELL.md`](TEST-SHELL.md))
- [ ] QuickView (right) + JumpDock (top) + QuickBrowse (left) together
- [ ] Text remains sharp (no fuzzy ClearType)

## Touch (Phase 5 follow-up)

- [ ] Tap places without missing
- [ ] Scroll long Downloads without accidental opens
- [ ] Reveal while maximized browser is up
